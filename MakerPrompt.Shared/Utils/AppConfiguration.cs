using static MakerPrompt.Shared.Utils.Enums;

namespace MakerPrompt.Shared.Utils
{
    public class AppConfiguration
    {
        public Theme Theme { get; set; } = Theme.Auto;
        public string[] SupportedCultures { get; } = ["en-US", "de-DE"];
        public string Language { get; set; } = "en-US";
        public bool AnalyticsEnabled { get; set; } = true;
        public DateTime? LastUpdated { get; set; }
    }
}
