using System.Text.Json.Serialization;

namespace MakerPrompt.UI.Components.Services;

/// <summary>
/// Serializes printer and farm configuration stores without leaking persistence metadata
/// into the domain model. It also reads the unversioned flat and legacy nested formats.
/// </summary>
public sealed class PrinterConfigurationPersistence(
    PrinterConnectionDefinitionProtector protector)
{
    public const int CurrentSchemaVersion = 1;
    public const int CurrentCredentialProtectionVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public string SerializeConnections(IEnumerable<PrinterConnectionDefinition> definitions)
    {
        var document = new ConnectionStoreDocument
        {
            SchemaVersion = CurrentSchemaVersion,
            CredentialProtectionVersion = CurrentCredentialProtectionVersion,
            Printers = definitions.Select(ToProtectedStore).ToList()
        };
        return JsonSerializer.Serialize(document, JsonOptions);
    }

    public PersistenceReadResult<PrinterConnectionDefinition> DeserializeConnections(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new([], false);

        using var parsed = JsonDocument.Parse(json);
        if (parsed.RootElement.ValueKind == JsonValueKind.Array)
        {
            var legacy = JsonSerializer.Deserialize<List<StoredPrinterConnection>>(json, JsonOptions) ?? [];
            var definitions = legacy.Select(item => item.ToDomain()).ToList();
            foreach (var definition in definitions)
                protector.Unprotect(definition, allowLegacyUnmarked: true);
            return new(definitions, true);
        }

        EnsureObject(parsed.RootElement, "printer connection");
        var document = JsonSerializer.Deserialize<ConnectionStoreDocument>(json, JsonOptions)
            ?? throw new JsonException("Printer connection store is empty.");
        ValidateVersions(document.SchemaVersion, document.CredentialProtectionVersion);

        var storedPrinters = document.Printers
            ?? throw new JsonException("Printer connection store has no printer collection.");
        var current = storedPrinters.Select(item => item.ToDomain()).ToList();
        foreach (var definition in current)
            protector.Unprotect(definition);
        return new(current, false);
    }

    public string SerializeFarms(IEnumerable<FarmConfiguration> farms)
    {
        var document = new FarmStoreDocument
        {
            SchemaVersion = CurrentSchemaVersion,
            CredentialProtectionVersion = CurrentCredentialProtectionVersion,
            Farms = farms.Select(ToProtectedStore).ToList()
        };
        return JsonSerializer.Serialize(document, JsonOptions);
    }

    public PersistenceReadResult<FarmConfiguration> DeserializeFarms(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new([], false);

        using var parsed = JsonDocument.Parse(json);
        if (parsed.RootElement.ValueKind == JsonValueKind.Array)
        {
            var legacy = JsonSerializer.Deserialize<List<StoredFarmConfiguration>>(json, JsonOptions) ?? [];
            var farms = legacy.Select(item => item.ToDomain()).ToList();
            for (var index = 0; index < legacy.Count; index++)
            {
                if (legacy[index].CredentialsProtected != true)
                    continue;

                foreach (var definition in farms[index].Printers)
                    protector.Unprotect(definition, allowLegacyUnmarked: true);
            }
            return new(farms, true);
        }

        EnsureObject(parsed.RootElement, "farm");
        var document = JsonSerializer.Deserialize<FarmStoreDocument>(json, JsonOptions)
            ?? throw new JsonException("Farm store is empty.");
        ValidateVersions(document.SchemaVersion, document.CredentialProtectionVersion);

        var storedFarms = document.Farms
            ?? throw new JsonException("Farm store has no farm collection.");
        var current = storedFarms.Select(item => item.ToDomain()).ToList();
        foreach (var farm in current)
        {
            foreach (var definition in farm.Printers)
                protector.Unprotect(definition);
        }
        return new(current, false);
    }

    public FarmConfiguration DeserializeFarmExport(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidOperationException("Invalid farm configuration data.");

        using var parsed = JsonDocument.Parse(json);
        EnsureObject(parsed.RootElement, "farm export");

        var stored = JsonSerializer.Deserialize<StoredFarmConfiguration>(json, JsonOptions)
            ?? throw new InvalidOperationException("Invalid farm configuration data.");
        var farm = stored.ToDomain();
        if (stored.CredentialsProtected == true)
        {
            foreach (var definition in farm.Printers)
                protector.Unprotect(definition, allowLegacyUnmarked: true);
        }
        return farm;
    }

    public PrinterConnectionDefinition CloneDefinition(PrinterConnectionDefinition definition) =>
        StoredPrinterConnection.FromDomain(definition).ToDomain();

    public FarmConfiguration CloneFarm(FarmConfiguration farm) =>
        StoredFarmConfiguration.FromDomain(farm).ToDomain();

    public FarmConfiguration CreateRedactedExport(FarmConfiguration farm)
    {
        var export = CloneFarm(farm);
        foreach (var definition in export.Printers)
            PrinterConnectionDefinitionProtector.Redact(definition);
        return export;
    }

    public string SerializeFarmExport(FarmConfiguration farm) =>
        JsonSerializer.Serialize(
            StoredFarmConfiguration.FromDomain(CreateRedactedExport(farm)),
            JsonOptions);

    private StoredPrinterConnection ToProtectedStore(PrinterConnectionDefinition definition)
    {
        var clone = CloneDefinition(definition);
        protector.Protect(clone);
        return StoredPrinterConnection.FromDomain(clone);
    }

    private StoredFarmConfiguration ToProtectedStore(FarmConfiguration farm)
    {
        var clone = CloneFarm(farm);
        foreach (var definition in clone.Printers)
            protector.Protect(definition);
        return StoredFarmConfiguration.FromDomain(clone);
    }

    private static void ValidateVersions(int schemaVersion, int credentialProtectionVersion)
    {
        if (schemaVersion != CurrentSchemaVersion)
        {
            throw new NotSupportedException(
                $"Unsupported configuration schema version {schemaVersion}.");
        }

        if (credentialProtectionVersion != CurrentCredentialProtectionVersion)
        {
            throw new NotSupportedException(
                $"Unsupported credential protection version {credentialProtectionVersion}.");
        }
    }

    private static void EnsureObject(JsonElement element, string description)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw new JsonException($"Expected a {description} JSON object.");
    }

    private sealed class ConnectionStoreDocument
    {
        public int SchemaVersion { get; set; }
        public int CredentialProtectionVersion { get; set; }
        public List<StoredPrinterConnection>? Printers { get; set; }
    }

    private sealed class FarmStoreDocument
    {
        public int SchemaVersion { get; set; }
        public int CredentialProtectionVersion { get; set; }
        public List<StoredFarmConfiguration>? Farms { get; set; }
    }

    private sealed class StoredFarmConfiguration
    {
        public Guid? Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime? CreatedAt { get; set; }
        public List<StoredPrinterConnection> Printers { get; set; } = [];

        // Desktop hardening used this marker before the versioned envelope existed.
        public bool? CredentialsProtected { get; set; }

        public static StoredFarmConfiguration FromDomain(FarmConfiguration farm) => new()
        {
            Id = farm.Id,
            Name = farm.Name,
            CreatedAt = farm.CreatedAt,
            Printers = farm.Printers.Select(StoredPrinterConnection.FromDomain).ToList()
        };

        public FarmConfiguration ToDomain() => new()
        {
            Id = Id is { } id && id != Guid.Empty ? id : Guid.NewGuid(),
            Name = Name,
            CreatedAt = CreatedAt ?? DateTime.UtcNow,
            Printers = Printers.Select(item => item.ToDomain()).ToList()
        };
    }

    private sealed class StoredPrinterConnection
    {
        public Guid? Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public PrinterConnectionType? ConnectionType { get; set; }
        public StoredPrinterConnectionSettings? Settings { get; set; }
        public bool AutoConnect { get; set; }
        public string? Color { get; set; }
        public string? Notes { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? LastConnectedAt { get; set; }
        public Guid? AssignedFilamentSpoolId { get; set; }

        public static StoredPrinterConnection FromDomain(
            PrinterConnectionDefinition definition) => new()
        {
            Id = definition.Id,
            Name = definition.Name,
            ConnectionType = definition.ConnectionType,
            Settings = StoredPrinterConnectionSettings.FromDomain(definition.Settings),
            AutoConnect = definition.AutoConnect,
            Color = definition.Color,
            Notes = definition.Notes,
            CreatedAt = definition.CreatedAt,
            LastConnectedAt = definition.LastConnectedAt,
            AssignedFilamentSpoolId = definition.AssignedFilamentSpoolId
        };

        public PrinterConnectionDefinition ToDomain()
        {
            var settings = Settings?.ToDomain(ConnectionType) ?? new PrinterConnectionSettings();
            var connectionType = ConnectionType ?? settings.ConnectionType;
            settings.ConnectionType = connectionType;

            return new PrinterConnectionDefinition
            {
                Id = Id is { } id && id != Guid.Empty ? id : Guid.NewGuid(),
                Name = Name,
                ConnectionType = connectionType,
                Settings = settings,
                AutoConnect = AutoConnect,
                Color = Color,
                Notes = Notes,
                CreatedAt = CreatedAt ?? DateTime.UtcNow,
                LastConnectedAt = LastConnectedAt,
                AssignedFilamentSpoolId = AssignedFilamentSpoolId
            };
        }
    }

    private sealed class StoredPrinterConnectionSettings
    {
        public PrinterConnectionType? ConnectionType { get; set; }
        public string? ApiUrl { get; set; }
        public string? UserName { get; set; }
        public string? Password { get; set; }
        public string? PortName { get; set; }
        public int? BaudRate { get; set; }
        public string? ProviderId { get; set; }

        // Legacy main stored API and serial settings in nested objects.
        public LegacyApiSettings? Api { get; set; }
        public LegacySerialSettings? Serial { get; set; }

        public static StoredPrinterConnectionSettings FromDomain(
            PrinterConnectionSettings settings) => new()
        {
            ConnectionType = settings.ConnectionType,
            ApiUrl = settings.ApiUrl,
            UserName = settings.UserName,
            Password = settings.Password,
            PortName = settings.PortName,
            BaudRate = settings.BaudRate,
            ProviderId = settings.ProviderId
        };

        public PrinterConnectionSettings ToDomain(
            PrinterConnectionType? definitionConnectionType) => new()
        {
            ConnectionType = ConnectionType ?? definitionConnectionType
                ?? PrinterConnectionType.Demo,
            ApiUrl = ApiUrl ?? Api?.Url,
            UserName = UserName ?? Api?.UserName,
            Password = Password ?? Api?.Password,
            PortName = PortName ?? Serial?.PortName,
            BaudRate = BaudRate ?? Serial?.BaudRate ?? 115_200,
            ProviderId = ProviderId
        };
    }

    private sealed class LegacyApiSettings
    {
        public string? Url { get; set; }
        public string? UserName { get; set; }
        public string? Password { get; set; }
    }

    private sealed class LegacySerialSettings
    {
        public string? PortName { get; set; }
        public int? BaudRate { get; set; }
    }
}

public sealed record PersistenceReadResult<T>(
    IReadOnlyList<T> Items,
    bool RequiresMigration);
