using MakerPrompt.Shared.Properties;
using Microsoft.Extensions.Localization;

namespace MakerPrompt.Shared.Services
{
    public class LocalizedTitleService : IDisposable
    {
        private readonly IStringLocalizer<Resources> _localizer;
        private string _baseTitleKey = string.Empty;
        private object[] _titleArguments = Array.Empty<object>();

        public event Action? OnTitleChanged;

        public LocalizedTitleService(IStringLocalizer<Resources> localizer)
        {
            _localizer = localizer;
        }

        public string CurrentTitle =>
            string.IsNullOrEmpty(_baseTitleKey)
                ? string.Empty
                : _localizer[_baseTitleKey, _titleArguments];

        public void SetTitle(string titleKey, params object[] arguments)
        {
            _baseTitleKey = titleKey;
            _titleArguments = arguments;
            OnTitleChanged?.Invoke();
        }

        public void Dispose()
        {
            OnTitleChanged = null;
        }
    }
}
