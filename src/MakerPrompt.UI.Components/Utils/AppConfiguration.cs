namespace MakerPrompt.UI.Components.Utils
{
    public class AppConfiguration
    {
        public Theme Theme { get; set; } = Theme.Auto;
		public string[] SupportedCultures { get; } = ["en-US", "de-DE", "tr-TR", "es-ES", "fr-FR", "it-IT", "pl-PL", "he-IL", "zh-CN"];
        public string Language { get; set; } = "en-US";
        public string FarmName { get; set; } = string.Empty;
        public bool FarmModeEnabled { get; set; } = false;
        public Guid? ActiveFarmId { get; set; }
        public bool AnalyticsEnabled { get; set; } = true;
        public bool EnableFilamentInventory { get; set; } = false;
        public bool EnablePrintAnalytics { get; set; } = false;
        public DateTime? LastUpdated { get; set; }

        // ── Deployment mode ─────────────────────────────────────────────────
        // Sourced from appsettings.json (MakerPrompt:DeploymentMode) at startup.
        // NOT persisted to localStorage — this is a deployment-time decision.
        // Default: Standalone (no auth, direct printer connections).
        [System.Text.Json.Serialization.JsonIgnore]
        public AppDeploymentMode DeploymentMode { get; set; } = AppDeploymentMode.Standalone;

        /// <summary>
        /// Base URL of the MakerPrompt Cloud API.
        /// Required when DeploymentMode == CloudMakerspace.
        /// Sourced from appsettings.json (MakerPrompt:CloudApiBaseUrl).
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public string CloudApiBaseUrl { get; set; } = string.Empty;
    }
}
