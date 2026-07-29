using System.Text.Json;

namespace MakerPrompt.Tests.Unit.Services;

public sealed class PrinterConfigurationPersistenceTests
{
    private readonly PrinterConnectionDefinitionProtector _protector;
    private readonly PrinterConfigurationPersistence _persistence;

    public PrinterConfigurationPersistenceTests()
    {
        _protector = new PrinterConnectionDefinitionProtector(
            new Base64ConnectionEncryptionService());
        _persistence = new PrinterConfigurationPersistence(_protector);
    }

    [Fact]
    public void Connections_RoundTripThroughVersionedProtectedEnvelope()
    {
        var definition = CreateDefinition("alice", "correct horse battery staple");

        var json = _persistence.SerializeConnections([definition]);

        Assert.DoesNotContain("alice", json, StringComparison.Ordinal);
        Assert.DoesNotContain("correct horse battery staple", json, StringComparison.Ordinal);
        Assert.Contains(PrinterConnectionDefinitionProtector.ProtectedPrefix, json);

        using var document = JsonDocument.Parse(json);
        Assert.Equal(
            PrinterConfigurationPersistence.CurrentSchemaVersion,
            document.RootElement.GetProperty("SchemaVersion").GetInt32());
        Assert.Equal(
            PrinterConfigurationPersistence.CurrentCredentialProtectionVersion,
            document.RootElement.GetProperty("CredentialProtectionVersion").GetInt32());

        var read = _persistence.DeserializeConnections(json);

        Assert.False(read.RequiresMigration);
        var restored = Assert.Single(read.Items);
        Assert.Equal("alice", restored.Settings.UserName);
        Assert.Equal("correct horse battery staple", restored.Settings.Password);
    }

    [Fact]
    public void Connections_ReadLegacyNestedApiAndSerialLayouts()
    {
        const string json =
            """
            [
              {
                "Name": "Legacy API",
                "ConnectionType": "PrusaLink",
                "Settings": {
                  "ConnectionType": "PrusaLink",
                  "Api": {
                    "Url": "http://printer.local",
                    "UserName": "YWxpY2U=",
                    "Password": "c2VjcmV0"
                  }
                }
              },
              {
                "Name": "Legacy Serial",
                "ConnectionType": "Serial",
                "Settings": {
                  "Serial": {
                    "PortName": "/dev/ttyUSB0",
                    "BaudRate": 250000
                  }
                }
              }
            ]
            """;

        var read = _persistence.DeserializeConnections(json);

        Assert.True(read.RequiresMigration);
        Assert.Collection(
            read.Items,
            api =>
            {
                Assert.Equal(PrinterConnectionType.PrusaLink, api.ConnectionType);
                Assert.Equal("http://printer.local", api.Settings.ApiUrl);
                Assert.Equal("alice", api.Settings.UserName);
                Assert.Equal("secret", api.Settings.Password);
            },
            serial =>
            {
                Assert.Equal(PrinterConnectionType.Serial, serial.ConnectionType);
                Assert.Equal("/dev/ttyUSB0", serial.Settings.PortName);
                Assert.Equal(250_000, serial.Settings.BaudRate);
            });

        var migrated = _persistence.SerializeConnections(read.Items);
        Assert.StartsWith("{", migrated.TrimStart(), StringComparison.Ordinal);
        Assert.Contains("\"SchemaVersion\"", migrated, StringComparison.Ordinal);
        using var migratedDocument = JsonDocument.Parse(migrated);
        foreach (var printer in migratedDocument.RootElement
                     .GetProperty("Printers")
                     .EnumerateArray())
        {
            var settings = printer.GetProperty("Settings");
            Assert.False(settings.TryGetProperty("Api", out _));
            Assert.False(settings.TryGetProperty("Serial", out _));
        }
    }

    [Fact]
    public void Farms_DistinguishLegacyPlaintextFromProtectedDesktopData()
    {
        const string json =
            """
            [
              {
                "Name": "Original main",
                "Printers": [
                  {
                    "Name": "Plaintext",
                    "ConnectionType": "OctoPrint",
                    "Settings": {
                      "ApiUrl": "http://plain.local",
                      "UserName": "dGVzdA==",
                      "Password": "plain-password"
                    }
                  }
                ]
              },
              {
                "Name": "Desktop hardened",
                "CredentialsProtected": true,
                "Printers": [
                  {
                    "Name": "Protected",
                    "ConnectionType": "Moonraker",
                    "Settings": {
                      "ApiUrl": "http://protected.local",
                      "UserName": "YWxpY2U=",
                      "Password": "c2VjcmV0"
                    }
                  }
                ]
              }
            ]
            """;

        var read = _persistence.DeserializeFarms(json);

        Assert.True(read.RequiresMigration);
        Assert.Equal("dGVzdA==", read.Items[0].Printers[0].Settings.UserName);
        Assert.Equal("plain-password", read.Items[0].Printers[0].Settings.Password);
        Assert.Equal("alice", read.Items[1].Printers[0].Settings.UserName);
        Assert.Equal("secret", read.Items[1].Printers[0].Settings.Password);

        var migrated = _persistence.SerializeFarms(read.Items);
        Assert.DoesNotContain("plain-password", migrated, StringComparison.Ordinal);
        Assert.DoesNotContain("\"CredentialsProtected\"", migrated, StringComparison.Ordinal);
        Assert.Contains(PrinterConnectionDefinitionProtector.ProtectedPrefix, migrated);
    }

    [Fact]
    public void Protector_IsIdempotentAndPreservesMalformedValues()
    {
        var definition = CreateDefinition("alice", "secret");

        _protector.Protect(definition);
        var protectedUserName = definition.Settings.UserName;
        var protectedPassword = definition.Settings.Password;
        _protector.Protect(definition);

        Assert.Equal(protectedUserName, definition.Settings.UserName);
        Assert.Equal(protectedPassword, definition.Settings.Password);

        definition.Settings.UserName = "enc:v1:not-valid-base64";
        definition.Settings.Password = "not-valid-base64";
        _protector.Unprotect(definition, allowLegacyUnmarked: true);

        Assert.Equal("enc:v1:not-valid-base64", definition.Settings.UserName);
        Assert.Equal("not-valid-base64", definition.Settings.Password);
    }

    [Fact]
    public void UnsupportedSchemaVersion_IsRejected()
    {
        const string json =
            """
            {
              "SchemaVersion": 99,
              "CredentialProtectionVersion": 1,
              "Printers": []
            }
            """;

        Assert.Throws<NotSupportedException>(
            () => _persistence.DeserializeConnections(json));
    }

    private static PrinterConnectionDefinition CreateDefinition(
        string userName,
        string password) => new()
    {
        Name = "Workshop",
        ConnectionType = PrinterConnectionType.OctoPrint,
        Settings = new PrinterConnectionSettings
        {
            ConnectionType = PrinterConnectionType.OctoPrint,
            ApiUrl = "http://printer.local",
            UserName = userName,
            Password = password
        }
    };
}
