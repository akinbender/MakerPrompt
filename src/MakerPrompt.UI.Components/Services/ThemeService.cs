namespace MakerPrompt.UI.Components.Services
{
    public sealed class ThemeService : IAsyncDisposable
    {
        private readonly Lazy<Task<IJSObjectReference>> _moduleTask;
        private readonly IAppConfigurationService _configService;
        private DotNetObjectReference<ThemeService> _dotNetRef;
        private IJSObjectReference? _jsModule;

        public Theme CurrentTheme { get; private set; }
        public event Action? OnThemeChanged;

        public ThemeService(IJSRuntime jsRuntime, IAppConfigurationService configService)
        {
            _configService = configService;
            _moduleTask = new(() => jsRuntime.InvokeAsync<IJSObjectReference>(
                "import", "./_content/MakerPrompt.UI.Components/js/themeJsInterop.js").AsTask());

            _dotNetRef = DotNetObjectReference.Create(this);
        }

        public async Task InitializeAsync()
        {
            await _configService.InitializeAsync();
            CurrentTheme = _configService.Configuration.Theme;

            _jsModule = await _moduleTask.Value;
            await _jsModule.InvokeVoidAsync("watchSystemTheme", _dotNetRef);

            await ApplyTheme();
        }

        public async Task SetThemeAsync(Theme theme)
        {
            CurrentTheme = theme;
            _configService.Configuration.Theme = theme;
            await _configService.SaveConfigurationAsync();

            await ApplyTheme();
            OnThemeChanged?.Invoke();
        }

        private async Task ApplyTheme()
        {
            var effectiveTheme = CurrentTheme == Theme.Auto
                ? await GetSystemTheme()
                : CurrentTheme;

            await SetTheme(effectiveTheme);
        }

        private async Task<Theme> GetSystemTheme()
        {
            var isDark = await _jsModule!.InvokeAsync<bool>("isSystemDark");
            return isDark ? Theme.Dark : Theme.Light;
        }

        private async Task SetTheme(Theme theme)
        {
            await _jsModule!.InvokeVoidAsync("setTheme", theme.ToString().ToLower());
        }

        [JSInvokable]
        public async Task HandleSystemThemeChange(bool isDark)
        {
            if (CurrentTheme == Theme.Auto)
            {
                await ApplyTheme();
                OnThemeChanged?.Invoke();
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_moduleTask.IsValueCreated)
            {
                if (_jsModule != null)
                {
                    try
                    {
                        await _jsModule.InvokeVoidAsync("dispose");
                    }
                    catch (JSDisconnectedException) { }
                    catch (JSException) { }

                    await _jsModule.DisposeAsync();
                }
            }

            _dotNetRef?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
