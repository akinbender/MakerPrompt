namespace MakerPrompt.Shared.Utils
{
    public class AppConfiguration
    {
        public Theme Theme { get; set; } = Theme.Auto;
		public string[] SupportedCultures { get; } = new string[] { "en-US", "de-DE", "tr-TR", "es-ES", "fr-FR", "pl-PL" };
        public string Language { get; set; } = "en-US";
        public bool AnalyticsEnabled { get; set; } = true;
        public DateTime? LastUpdated { get; set; }
    }
}
