namespace MakerPrompt.Shared.Models
{
    /// <summary>
    /// Neutral printer camera description consumed by UI components.
    /// </summary>
    public sealed class PrinterCamera
    {
        public string Id { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Optional MJPEG or similar stream URL. When present, the UI prefers this.
        /// </summary>
        public string? StreamUrl { get; set; }

        /// <summary>
        /// Optional snapshot URL used for periodic still-image refresh when no stream is available.
        /// </summary>
        public string? SnapshotUrl { get; set; }

        public bool IsEnabled { get; set; }

        public string? Location { get; set; }
    }
}
