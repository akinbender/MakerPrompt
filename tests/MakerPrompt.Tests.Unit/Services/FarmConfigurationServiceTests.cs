using MakerPrompt.Tests.Unit.Helpers;
using Microsoft.Extensions.Logging.Abstractions;

namespace MakerPrompt.Tests.Unit.Services;

public sealed class FarmConfigurationServiceTests
{
    [Fact]
    public async Task DeletingActiveFarm_LoadsNextFarmThenClearsLastFarm()
    {
        var storage = new InMemoryAppLocalStorageProvider();
        var config = new TestAppConfigurationService();
        var persistence = CreatePersistence();
        await using var manager = CreateManager(storage, persistence, config);
        var service = new FarmConfigurationService(
            storage,
            config,
            manager,
            persistence,
            NullLogger<FarmConfigurationService>.Instance);
        await service.InitializeAsync();

        var first = await service.CreateFarmAsync("First");
        first.Printers.Add(CreateDefinition("First printer", "first-secret"));
        var second = await service.CreateFarmAsync("Second");
        second.Printers.Add(CreateDefinition("Second printer", "second-secret"));

        await service.SwitchFarmAsync(first.Id);
        Assert.Equal(first.Id, config.Configuration.ActiveFarmId);
        Assert.Equal(
            "First printer",
            Assert.Single(manager.Printers).Definition.Name);

        await service.DeleteFarmAsync(first.Id);

        Assert.Equal(second.Id, config.Configuration.ActiveFarmId);
        Assert.Equal("Second", config.Configuration.FarmName);
        Assert.Equal(
            "Second printer",
            Assert.Single(manager.Printers).Definition.Name);

        await service.DeleteFarmAsync(second.Id);

        Assert.Null(config.Configuration.ActiveFarmId);
        Assert.Equal(string.Empty, config.Configuration.FarmName);
        Assert.Empty(manager.Printers);
    }

    [Fact]
    public async Task FarmStoreIsProtectedAndExportIsRedacted()
    {
        var storage = new InMemoryAppLocalStorageProvider();
        var config = new TestAppConfigurationService();
        var persistence = CreatePersistence();
        await using var manager = CreateManager(storage, persistence, config);
        var service = new FarmConfigurationService(
            storage,
            config,
            manager,
            persistence,
            NullLogger<FarmConfigurationService>.Instance);
        await service.InitializeAsync();

        var farm = await service.CreateFarmAsync("Secure");
        farm.Printers.Add(CreateDefinition("Printer", "do-not-leak"));
        await service.SwitchFarmAsync(farm.Id);

        var stored = await ReadTextAsync(
            storage,
            "MakerPrompt.FarmConfigurations");
        Assert.DoesNotContain("do-not-leak", stored, StringComparison.Ordinal);
        Assert.Contains(
            PrinterConnectionDefinitionProtector.ProtectedPrefix,
            stored,
            StringComparison.Ordinal);

        var exported = service.ExportFarm(farm.Id);
        Assert.DoesNotContain("do-not-leak", exported, StringComparison.Ordinal);
        var imported = persistence.DeserializeFarmExport(exported);
        var exportedPrinter = Assert.Single(imported.Printers);
        Assert.Equal(string.Empty, exportedPrinter.Settings.UserName);
        Assert.Equal(string.Empty, exportedPrinter.Settings.Password);
    }

    private static PrinterConnectionManager CreateManager(
        IAppLocalStorageProvider storage,
        PrinterConfigurationPersistence persistence,
        IAppConfigurationService config)
    {
        var legacyFactory = new PrinterCommunicationServiceFactory(
            null!,
            null!,
            null!,
            null!,
            null!,
            null!);
        return new PrinterConnectionManager(
            storage,
            persistence,
            new NeverBackendFactory(),
            legacyFactory,
            NullLogger<PrinterConnectionManager>.Instance,
            null!,
            null!,
            null!,
            config);
    }

    private static PrinterConfigurationPersistence CreatePersistence() => new(
        new PrinterConnectionDefinitionProtector(
            new Base64ConnectionEncryptionService()));

    private static PrinterConnectionDefinition CreateDefinition(
        string name,
        string password) => new()
    {
        Name = name,
        ConnectionType = PrinterConnectionType.OctoPrint,
        Settings = new PrinterConnectionSettings
        {
            ConnectionType = PrinterConnectionType.OctoPrint,
            ApiUrl = "http://printer.local",
            UserName = "operator",
            Password = password
        }
    };

    private static async Task<string> ReadTextAsync(
        IAppLocalStorageProvider storage,
        string path)
    {
        using var stream = await storage.OpenReadAsync(path)
            ?? throw new InvalidOperationException($"Missing test file {path}.");
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    private sealed class NeverBackendFactory : IPrinterBackendFactory
    {
        public IPrinterCommunicationService Create(
            PrinterConnectionType connectionType) =>
            throw new InvalidOperationException("This test should not create a backend.");
    }

    private sealed class TestAppConfigurationService : IAppConfigurationService
    {
        public AppConfiguration Configuration { get; } = new();

        public Task InitializeAsync() => Task.CompletedTask;
        public Task SaveConfigurationAsync() => Task.CompletedTask;
        public Task ResetToDefaultsAsync() => Task.CompletedTask;
    }
}
