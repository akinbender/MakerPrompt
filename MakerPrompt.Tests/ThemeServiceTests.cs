using Microsoft.JSInterop;
using MakerPrompt.Shared.Infrastructure;
using MakerPrompt.Shared.Models;
using MakerPrompt.Shared.Services;
using MakerPrompt.Shared.Utils;
using static MakerPrompt.Shared.Utils.Enums;

namespace MakerPrompt.Tests;

public class ThemeServiceTests
{
    // ── Disposal ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task DisposeAsync_DoesNotThrow_WhenJSModuleThrowsJSException()
    {
        // Reproduces: switching language triggers page reload which disposes
        // ThemeService while the JS side is already gone.
        var module = new FakeJSModule(throwsOnDispose: true);
        var service = BuildService(module);
        await service.InitializeAsync();

        var ex = await Record.ExceptionAsync(() => service.DisposeAsync().AsTask());

        Assert.Null(ex);
    }

    [Fact]
    public async Task DisposeAsync_DoesNotThrow_WhenModuleNotYetInitialized()
    {
        var service = BuildService(new FakeJSModule());
        // InitializeAsync NOT called — _moduleTask.IsValueCreated == false

        var ex = await Record.ExceptionAsync(() => service.DisposeAsync().AsTask());

        Assert.Null(ex);
    }

    [Fact]
    public async Task DisposeAsync_DoesNotThrow_OnSecondCall()
    {
        var service = BuildService(new FakeJSModule());
        await service.InitializeAsync();
        await service.DisposeAsync();

        var ex = await Record.ExceptionAsync(() => service.DisposeAsync().AsTask());

        Assert.Null(ex);
    }

    // ── Theme state ───────────────────────────────────────────────────────────

    [Fact]
    public async Task InitializeAsync_SetsCurrentThemeFromConfig()
    {
        var config = new FakeConfigService { InitialTheme = Theme.Dark };
        var service = new ThemeService(new FakeJSRuntime(new FakeJSModule()), config);

        await service.InitializeAsync();

        Assert.Equal(Theme.Dark, service.CurrentTheme);
    }

    [Fact]
    public async Task SetThemeAsync_UpdatesCurrentTheme()
    {
        var service = BuildService(new FakeJSModule());
        await service.InitializeAsync();

        await service.SetThemeAsync(Theme.Dark);

        Assert.Equal(Theme.Dark, service.CurrentTheme);
    }

    [Fact]
    public async Task SetThemeAsync_RaisesOnThemeChanged()
    {
        var service = BuildService(new FakeJSModule());
        await service.InitializeAsync();
        var raised = false;
        service.OnThemeChanged += () => raised = true;

        await service.SetThemeAsync(Theme.Light);

        Assert.True(raised);
    }

    // ── System theme change ───────────────────────────────────────────────────

    [Fact]
    public async Task HandleSystemThemeChange_RaisesOnThemeChanged_WhenModeIsAuto()
    {
        var service = BuildService(new FakeJSModule());
        await service.InitializeAsync();
        await service.SetThemeAsync(Theme.Auto);
        var raised = false;
        service.OnThemeChanged += () => raised = true;

        await service.HandleSystemThemeChange(isDark: true);

        Assert.True(raised);
    }

    [Fact]
    public async Task HandleSystemThemeChange_DoesNotRaiseOnThemeChanged_WhenModeIsNotAuto()
    {
        var service = BuildService(new FakeJSModule());
        await service.InitializeAsync();
        await service.SetThemeAsync(Theme.Dark);
        var raised = false;
        service.OnThemeChanged += () => raised = true;

        await service.HandleSystemThemeChange(isDark: true);

        Assert.False(raised);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ThemeService BuildService(FakeJSModule module)
        => new(new FakeJSRuntime(module), new FakeConfigService());

    private sealed class FakeJSModule : IJSObjectReference
    {
        private readonly bool _throwsOnDispose;

        public FakeJSModule(bool throwsOnDispose = false) => _throwsOnDispose = throwsOnDispose;

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            if (_throwsOnDispose && identifier == "dispose")
                return ValueTask.FromException<TValue>(
                    new JSException("JS object instance with ID 1 does not exist (has it been disposed?)."));

            if (typeof(TValue) == typeof(bool))
                return ValueTask.FromResult((TValue)(object)false);

            return ValueTask.FromResult(default(TValue)!);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            => InvokeAsync<TValue>(identifier, args);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeJSRuntime : IJSRuntime
    {
        private readonly FakeJSModule _module;

        public FakeJSRuntime(FakeJSModule module) => _module = module;

        public ValueTask<TValue> InvokeAsync<[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
            System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors |
            System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicFields |
            System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties)] TValue>(
            string identifier, object?[]? args)
            => ValueTask.FromResult((TValue)(object)_module);

        public ValueTask<TValue> InvokeAsync<[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
            System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors |
            System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicFields |
            System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties)] TValue>(
            string identifier, CancellationToken cancellationToken, object?[]? args)
            => InvokeAsync<TValue>(identifier, args);
    }

    private sealed class FakeConfigService : IAppConfigurationService
    {
        public Theme InitialTheme { get; init; } = Theme.Auto;
        public AppConfiguration Configuration => _config ??= new AppConfiguration { Theme = InitialTheme };
        private AppConfiguration? _config;

        public Task InitializeAsync() => Task.CompletedTask;
        public Task SaveConfigurationAsync() => Task.CompletedTask;
        public Task ResetToDefaultsAsync() => Task.CompletedTask;
    }
}
