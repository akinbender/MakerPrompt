using System.Collections.Concurrent;
using MakerPrompt.Infrastructure.Services.Printers;
using MakerPrompt.Tests.Unit.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace MakerPrompt.Tests.Unit.Services;

public sealed class PrinterConnectionManagerTests
{
    [Fact]
    public async Task Initialize_MigratesLegacyStoreWithoutLosingCredentials()
    {
        var storage = new InMemoryAppLocalStorageProvider();
        await SaveTextAsync(
            storage,
            "MakerPrompt.PrinterConnections",
            """
            [
              {
                "Name": "Legacy",
                "ConnectionType": "OctoPrint",
                "Settings": {
                  "ConnectionType": "OctoPrint",
                  "Api": {
                    "Url": "http://legacy.local",
                    "UserName": "b3BlcmF0b3I=",
                    "Password": "c2VjcmV0"
                  }
                }
              }
            ]
            """);
        var backendFactory = new TrackingBackendFactory(
            () => throw new InvalidOperationException("No connection expected."));
        await using var manager = CreateManager(storage, backendFactory);

        await manager.InitializeAsync();

        var definition = Assert.Single(manager.Printers).Definition;
        Assert.Equal("operator", definition.Settings.UserName);
        Assert.Equal("secret", definition.Settings.Password);

        var migrated = await ReadTextAsync(
            storage,
            "MakerPrompt.PrinterConnections");
        Assert.StartsWith("{", migrated.TrimStart(), StringComparison.Ordinal);
        Assert.Contains("\"SchemaVersion\"", migrated, StringComparison.Ordinal);
        Assert.DoesNotContain("\"secret\"", migrated, StringComparison.Ordinal);
        Assert.Contains(
            PrinterConnectionDefinitionProtector.ProtectedPrefix,
            migrated,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task DefaultBackendFactory_CreatesIndependentSerialInstances()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var factory = new PrinterBackendFactory<TestSerialService>(services);

        var first = factory.Create(PrinterConnectionType.Serial);
        var second = factory.Create(PrinterConnectionType.Serial);

        Assert.IsType<TestSerialService>(first);
        Assert.IsType<TestSerialService>(second);
        Assert.NotSame(first, second);

        await first.DisposeAsync();
        await second.DisposeAsync();
    }

    [Fact]
    public async Task ConcurrentConnects_CreateOnlyOneBackend()
    {
        var storage = new InMemoryAppLocalStorageProvider();
        var service = new TrackingPrinterService
        {
            ConnectDelay = TimeSpan.FromMilliseconds(50)
        };
        var backendFactory = new TrackingBackendFactory(() => service);
        await using var manager = CreateManager(storage, backendFactory);
        var definition = await manager.AddPrinterAsync(CreateDefinition());

        await Task.WhenAll(
            manager.ConnectPrinterAsync(definition.Id),
            manager.ConnectPrinterAsync(definition.Id));

        Assert.Equal(1, backendFactory.CreateCount);
        Assert.Equal(1, service.ConnectCount);
        Assert.Same(service, Assert.Single(manager.Printers).Service);
    }

    [Fact]
    public async Task FailedConnection_IsDisposedAndNotRetained()
    {
        var storage = new InMemoryAppLocalStorageProvider();
        var service = new TrackingPrinterService { ConnectShouldSucceed = false };
        var backendFactory = new TrackingBackendFactory(() => service);
        await using var manager = CreateManager(storage, backendFactory);
        var definition = await manager.AddPrinterAsync(CreateDefinition());

        await manager.ConnectPrinterAsync(definition.Id);

        var state = Assert.Single(manager.Printers);
        Assert.Null(state.Service);
        Assert.Equal(PrinterStatus.Disconnected, state.Status);
        Assert.Equal("Connection failed", state.LastError);
        Assert.Equal(1, service.DisposeCount);
    }

    [Fact]
    public async Task ThrowingConnection_IsDisposedAndReportedAsError()
    {
        var storage = new InMemoryAppLocalStorageProvider();
        var service = new TrackingPrinterService
        {
            ConnectException = new InvalidOperationException("port unavailable")
        };
        var backendFactory = new TrackingBackendFactory(() => service);
        await using var manager = CreateManager(storage, backendFactory);
        var definition = await manager.AddPrinterAsync(CreateDefinition());

        await manager.ConnectPrinterAsync(definition.Id);

        var state = Assert.Single(manager.Printers);
        Assert.Null(state.Service);
        Assert.Equal(PrinterStatus.Error, state.Status);
        Assert.Equal("port unavailable", state.LastError);
        Assert.Equal(1, service.DisposeCount);
    }

    [Fact]
    public async Task Reconnect_DisposesBackendRetainedAfterRemoteDisconnect()
    {
        var storage = new InMemoryAppLocalStorageProvider();
        var first = new TrackingPrinterService();
        var second = new TrackingPrinterService();
        var pending = new ConcurrentQueue<TrackingPrinterService>([first, second]);
        var backendFactory = new TrackingBackendFactory(
            () => pending.TryDequeue(out var service)
                ? service
                : throw new InvalidOperationException("No backend queued."));
        await using var manager = CreateManager(storage, backendFactory);
        var definition = await manager.AddPrinterAsync(CreateDefinition());

        await manager.ConnectPrinterAsync(definition.Id);
        first.RaiseRemoteDisconnect();

        var state = Assert.Single(manager.Printers);
        Assert.Same(first, state.Service);
        Assert.Equal(PrinterStatus.Disconnected, state.Status);

        await manager.ConnectPrinterAsync(definition.Id);

        Assert.Equal(1, first.DisposeCount);
        Assert.Same(second, state.Service);
        Assert.True(second.IsConnected);
        Assert.Equal(2, backendFactory.CreateCount);
    }

    [Fact]
    public async Task Disconnect_DisposesEvenWhenGracefulDisconnectThrows()
    {
        var storage = new InMemoryAppLocalStorageProvider();
        var service = new TrackingPrinterService
        {
            DisconnectException = new IOException("transport failed")
        };
        var backendFactory = new TrackingBackendFactory(() => service);
        await using var manager = CreateManager(storage, backendFactory);
        var definition = await manager.AddPrinterAsync(CreateDefinition());
        await manager.ConnectPrinterAsync(definition.Id);

        await manager.DisconnectPrinterAsync(definition.Id);

        var state = Assert.Single(manager.Printers);
        Assert.Null(state.Service);
        Assert.Equal(PrinterStatus.Disconnected, state.Status);
        Assert.Equal(1, service.DisposeCount);
    }

    private static PrinterConnectionManager CreateManager(
        IAppLocalStorageProvider storage,
        IPrinterBackendFactory backendFactory)
    {
        var protector = new PrinterConnectionDefinitionProtector(
            new Base64ConnectionEncryptionService());
        var persistence = new PrinterConfigurationPersistence(protector);
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
            backendFactory,
            legacyFactory,
            NullLogger<PrinterConnectionManager>.Instance,
            null!,
            null!,
            null!,
            new TestAppConfigurationService());
    }

    private static PrinterConnectionDefinition CreateDefinition() => new()
    {
        Name = "Test printer",
        ConnectionType = PrinterConnectionType.Demo,
        Settings = new PrinterConnectionSettings
        {
            ConnectionType = PrinterConnectionType.Demo
        }
    };

    private static async Task SaveTextAsync(
        IAppLocalStorageProvider storage,
        string path,
        string text)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text), writable: false);
        await storage.SaveFileAsync(path, stream);
    }

    private static async Task<string> ReadTextAsync(
        IAppLocalStorageProvider storage,
        string path)
    {
        using var stream = await storage.OpenReadAsync(path)
            ?? throw new InvalidOperationException($"Missing test file {path}.");
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    private sealed class TrackingBackendFactory(
        Func<TrackingPrinterService> create) : IPrinterBackendFactory
    {
        public int CreateCount { get; private set; }

        public IPrinterCommunicationService Create(PrinterConnectionType connectionType)
        {
            CreateCount++;
            return create();
        }
    }

    private sealed class TrackingPrinterService
        : DemoPrinterService, IPrinterCommunicationService
    {
        public bool ConnectShouldSucceed { get; init; } = true;
        public Exception? ConnectException { get; init; }
        public Exception? DisconnectException { get; init; }
        public TimeSpan ConnectDelay { get; init; }
        public int ConnectCount { get; private set; }
        public int DisconnectCount { get; private set; }
        public int DisposeCount { get; private set; }

        public new async Task<bool> ConnectAsync(
            PrinterConnectionSettings settings,
            CancellationToken cancellationToken = default)
        {
            ConnectCount++;
            if (ConnectDelay > TimeSpan.Zero)
                await Task.Delay(ConnectDelay, cancellationToken);
            if (ConnectException != null)
                throw ConnectException;

            IsConnected = ConnectShouldSucceed;
            RaiseConnectionChanged();
            return IsConnected;
        }

        public new Task DisconnectAsync(
            CancellationToken cancellationToken = default)
        {
            DisconnectCount++;
            IsConnected = false;
            RaiseConnectionChanged();
            return DisconnectException == null
                ? Task.CompletedTask
                : Task.FromException(DisconnectException);
        }

        public void RaiseRemoteDisconnect()
        {
            IsConnected = false;
            RaiseConnectionChanged();
        }

        public override ValueTask DisposeAsync()
        {
            DisposeCount++;
            return base.DisposeAsync();
        }
    }

    private sealed class TestSerialService : DemoPrinterService, ISerialService
    {
        public bool IsSupported => true;

        public Task<IEnumerable<string>> GetAvailablePortsAsync() =>
            Task.FromResult<IEnumerable<string>>([]);

        public Task<bool> CheckSupportedAsync() => Task.FromResult(true);

        public Task RequestPortAsync() => Task.CompletedTask;
    }

    internal sealed class TestAppConfigurationService : IAppConfigurationService
    {
        public AppConfiguration Configuration { get; } = new();
        public int SaveCount { get; private set; }

        public Task InitializeAsync() => Task.CompletedTask;

        public Task SaveConfigurationAsync()
        {
            SaveCount++;
            return Task.CompletedTask;
        }

        public Task ResetToDefaultsAsync() => Task.CompletedTask;
    }
}
