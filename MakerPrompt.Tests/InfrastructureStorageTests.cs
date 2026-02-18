using System.Text;
using System.Text.Json;
using MakerPrompt.Shared.Infrastructure;
using MakerPrompt.Shared.Models;
using MakerPrompt.Shared.Services;
using MakerPrompt.Shared.Utils;
using static MakerPrompt.Shared.Utils.Enums;

namespace MakerPrompt.Tests;

public sealed class InfrastructureStorageTests
{
    [Fact]
    public async Task DataProtection_EncryptDecrypt_RoundTrips()
    {
        var keyStore = new FixedKeyStore();
        var protector = new AesGcmAppDataProtectionService(keyStore);

        const string plaintext = "{\"apiKey\":\"super-secret\"}";

        var ciphertext = await protector.EncryptAsync(plaintext);
        var decrypted = await protector.DecryptAsync(ciphertext);

        Assert.NotEqual(plaintext, ciphertext);
        Assert.DoesNotContain("super-secret", ciphertext, StringComparison.Ordinal);
        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public async Task DataProtection_Decrypt_ReturnsNull_ForTamperedCiphertext()
    {
        var keyStore = new FixedKeyStore();
        var protector = new AesGcmAppDataProtectionService(keyStore);

        var ciphertext = await protector.EncryptAsync("payload");

        using var doc = JsonDocument.Parse(ciphertext);
        var root = doc.RootElement;
        var iv = root.GetProperty("Iv").GetString()!;
        var mac = root.GetProperty("Mac").GetString()!;
        var cipher = root.GetProperty("CipherText").GetString()!;

        // Flip a base64 character but keep valid base64 payload.
        var tamperedCipher = (cipher[0] == 'A' ? 'B' : 'A') + cipher[1..];
        var tampered = JsonSerializer.Serialize(new
        {
            Version = 1,
            Iv = iv,
            Mac = mac,
            CipherText = tamperedCipher
        });

        var decrypted = await protector.DecryptAsync(tampered);

        Assert.Null(decrypted);
    }

    [Fact]
    public async Task LocalEncryptedStorage_SetGetRemove_Works_AndStoresEncryptedPayload()
    {
        var provider = new InMemoryAppLocalStorageProvider();
        var appConfig = new FakeAppConfigurationService();
        var dataProtection = new PrefixDataProtectionService();

        var storage = new LocalEncryptedAppStorageService(provider, dataProtection, appConfig);

        var value = new PrinterConnectionDefinition
        {
            Name = "Moonraker A",
            PrinterType = PrinterConnectionType.Moonraker,
            Address = "http://moonraker.local",
            UserName = "user",
            Password = "secret"
        };

        await storage.SetItemAsync("printer-connections", new List<PrinterConnectionDefinition> { value });

        Assert.Single(provider.StoredFiles);
        var storedPayload = Encoding.UTF8.GetString(provider.StoredFiles.Single().Value);
        Assert.StartsWith("enc::", storedPayload, StringComparison.Ordinal);
        Assert.DoesNotContain("moonraker.local", storedPayload, StringComparison.OrdinalIgnoreCase);

        var loaded = await storage.GetItemAsync<List<PrinterConnectionDefinition>>("printer-connections");
        Assert.NotNull(loaded);
        Assert.Single(loaded!);
        Assert.Equal("Moonraker A", loaded[0].Name);
        Assert.Equal("secret", loaded[0].Password);

        await storage.RemoveItemAsync("printer-connections");
        Assert.Empty(provider.StoredFiles);
    }

    [Fact]
    public async Task LocalEncryptedStorage_MigratesLegacyConfigurationStorage()
    {
        var provider = new InMemoryAppLocalStorageProvider();
        var appConfig = new FakeAppConfigurationService
        {
            Configuration = new AppConfiguration
            {
                StorageItems = new Dictionary<string, string>
                {
                    ["printer-connections"] = JsonSerializer.Serialize(new List<PrinterConnectionDefinition>
                    {
                        new() { Name = "Migrated", PrinterType = PrinterConnectionType.Demo }
                    })
                },
                LegacyPrinterConnectionSettings = new PrinterConnectionSettings(
                    new ApiConnectionSettings("http://prusa.local", "api", "token"),
                    PrinterConnectionType.PrusaLink)
            }
        };

        var dataProtection = new PrefixDataProtectionService();
        var storage = new LocalEncryptedAppStorageService(provider, dataProtection, appConfig);

        var migratedDefinitions = await storage.GetItemAsync<List<PrinterConnectionDefinition>>("printer-connections");
        var migratedLegacy = await storage.GetItemAsync<PrinterConnectionSettings>("printer-connection-settings");

        Assert.NotNull(migratedDefinitions);
        Assert.Single(migratedDefinitions!);
        Assert.Equal("Migrated", migratedDefinitions[0].Name);

        Assert.NotNull(migratedLegacy);
        Assert.Equal(PrinterConnectionType.PrusaLink, migratedLegacy!.ConnectionType);
        Assert.Equal("http://prusa.local", migratedLegacy.Api?.Url);

        Assert.Empty(appConfig.Configuration.StorageItems);
        Assert.Null(appConfig.Configuration.LegacyPrinterConnectionSettings);
        Assert.True(appConfig.InitializeCallCount >= 1);
        Assert.True(appConfig.SaveCallCount >= 1);

        // Ensure migrated values persisted as encrypted payloads.
        Assert.True(provider.StoredFiles.Count >= 2);
        Assert.All(provider.StoredFiles.Values, bytes =>
        {
            var payload = Encoding.UTF8.GetString(bytes);
            Assert.StartsWith("enc::", payload, StringComparison.Ordinal);
        });
    }

    private sealed class FixedKeyStore : IAppEncryptionKeyStore
    {
        private static readonly byte[] Key = Enumerable.Range(1, 32).Select(i => (byte)i).ToArray();

        public Task<byte[]> GetOrCreateKeyAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Key);
    }

    private sealed class PrefixDataProtectionService : IAppDataProtectionService
    {
        public Task<string> EncryptAsync(string plaintext, CancellationToken cancellationToken = default)
        {
            var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(plaintext));
            return Task.FromResult($"enc::{encoded}");
        }

        public Task<string?> DecryptAsync(string ciphertext, CancellationToken cancellationToken = default)
        {
            if (!ciphertext.StartsWith("enc::", StringComparison.Ordinal))
            {
                return Task.FromResult<string?>(null);
            }

            var encoded = ciphertext[5..];
            var bytes = Convert.FromBase64String(encoded);
            return Task.FromResult<string?>(Encoding.UTF8.GetString(bytes));
        }
    }

    private sealed class FakeAppConfigurationService : IAppConfigurationService
    {
        public AppConfiguration Configuration { get; set; } = new();

        public int InitializeCallCount { get; private set; }
        public int SaveCallCount { get; private set; }

        AppConfiguration IAppConfigurationService.Configuration => Configuration;

        public Task InitializeAsync()
        {
            InitializeCallCount++;
            return Task.CompletedTask;
        }

        public Task SaveConfigurationAsync()
        {
            SaveCallCount++;
            return Task.CompletedTask;
        }

        public Task ResetToDefaultsAsync()
        {
            Configuration = new AppConfiguration();
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryAppLocalStorageProvider : IAppLocalStorageProvider
    {
        public Dictionary<string, byte[]> StoredFiles { get; } = new(StringComparer.Ordinal);

        public string DisplayName => "In-memory";
        public string Key => "in-memory";
        public string RootPath => "/";

        public Task<List<FileEntry>> ListFilesAsync(CancellationToken cancellationToken = default)
        {
            var files = StoredFiles.Select(kv => new FileEntry
            {
                FullPath = kv.Key,
                Size = kv.Value.Length,
                ModifiedDate = DateTime.UtcNow,
                IsAvailable = true
            }).ToList();

            return Task.FromResult(files);
        }

        public Task<Stream?> OpenReadAsync(string fullPath, CancellationToken cancellationToken = default)
        {
            if (!StoredFiles.TryGetValue(fullPath, out var bytes))
            {
                return Task.FromResult<Stream?>(null);
            }

            return Task.FromResult<Stream?>(new MemoryStream(bytes, writable: false));
        }

        public async Task SaveFileAsync(string fullPath, Stream content, CancellationToken cancellationToken = default)
        {
            using var ms = new MemoryStream();
            await content.CopyToAsync(ms, cancellationToken);
            StoredFiles[fullPath] = ms.ToArray();
        }

        public Task DeleteFileAsync(string fullPath, CancellationToken cancellationToken = default)
        {
            StoredFiles.Remove(fullPath);
            return Task.CompletedTask;
        }
    }
}
