namespace MakerPrompt.UI.Components.Utils
{
    /// <summary>
    /// Controls which deployment mode the app is running in.
    /// The value is read from appsettings.json (MakerPrompt:DeploymentMode) at startup
    /// and cannot be changed at runtime — it is a deployment-time decision.
    /// </summary>
    public enum AppDeploymentMode
    {
        /// <summary>
        /// Default single-user / local mode.
        /// Direct printer connections, local fleet management, no authentication required.
        /// All existing backends (Moonraker, PrusaLink, Bambu, WebSerial, etc.) are available.
        /// </summary>
        Standalone = 0,

        /// <summary>
        /// Makerspace / cloud-hosted mode.
        /// OIDC authentication is required. Fleet configuration is served from the cloud API
        /// and cannot be modified by the user. Farm mode is automatically enabled.
        /// </summary>
        CloudMakerspace = 1
    }
}
