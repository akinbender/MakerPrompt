namespace MakerPrompt.Shared.Utils
{
    public class AppConfiguration
    {
        public Theme Theme { get; set; } = Theme.Auto;
		public string[] SupportedCultures { get; } = new string[] { "en-US", "de-DE", "tr-TR", "es-ES", "fr-FR", "pl-PL" };
        public string Language { get; set; } = "en-US";
        public bool AnalyticsEnabled { get; set; } = true;
        public DateTime? LastUpdated { get; set; }

        // Generic JSON key/value app storage used by higher-level services.
        public Dictionary<string, string> StorageItems { get; set; } = new();

        // Legacy single-printer configuration slot kept for migration only.
        public PrinterConnectionSettings? LegacyPrinterConnectionSettings { get; set; }
    }
}
