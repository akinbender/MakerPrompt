using MakerPrompt.Shared.Infrastructure;
using Microsoft.JSInterop;
using static MakerPrompt.Shared.Utils.Enums;

namespace MakerPrompt.Shared.Services
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
                "import", "./_content/MakerPrompt.Shared/js/themeJsInterop.js").AsTask());

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
            UpdateNativeTheme(effectiveTheme);
        }

        private async Task<Theme> GetSystemTheme()
        {
            var isDark = await _jsModule!.InvokeAsync<bool>("isSystemDark");
            return isDark ? Theme.Dark : Theme.Light;
        }

        private async Task SetTheme(Theme theme)
        {
            await _jsModule!.InvokeVoidAsync("setTheme", theme.ToString().ToLower());
            _configService.Configuration.Theme = theme;
            await _configService.SaveConfigurationAsync();
        }

        private void UpdateNativeTheme(Theme theme)
        {
            //TODO
            //try
            //{
            //    if (Application.Current != null)
            //    {
            //        Application.Current.Dispatcher.Dispatch(() =>
            //        {
            //            Application.Current.UserAppTheme = theme switch
            //            {
            //                Theme.Light => AppTheme.Light,
            //                Theme.Dark => AppTheme.Dark,
            //                _ => Application.Current.RequestedTheme
            //            };
            //        });
            //    }
            //}
            //catch
            //{
            //    // Web platform - ignore native theme updates
            //}
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
                    await _jsModule.InvokeVoidAsync("dispose");
                    await _jsModule.DisposeAsync();
                }
            }

            _dotNetRef?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
