namespace MakerPrompt.Shared.BrailleRAP.Models
{
    /// <summary>
    /// Configuration for BrailleRAP machine parameters.
    /// </summary>
    public class MachineConfig
    {
        /// <summary>
        /// Horizontal spacing between dots in a cell (mm).
        /// </summary>
        public double DotPaddingX { get; set; } = 2.2;

        /// <summary>
        /// Vertical spacing between dots in a cell (mm).
        /// </summary>
        public double DotPaddingY { get; set; } = 2.2;

        /// <summary>
        /// Horizontal spacing between cells (mm).
        /// </summary>
        public double CellPaddingX { get; set; } = 6.0;

        /// <summary>
        /// Vertical spacing between lines (mm).
        /// </summary>
        public double CellPaddingY { get; set; } = 12.0;

        /// <summary>
        /// Feed rate for movement (mm/min).
        /// </summary>
        public int FeedRate { get; set; } = 6000;

        /// <summary>
        /// X-axis offset from origin (mm).
        /// </summary>
        public double OffsetX { get; set; } = 0.0;

        /// <summary>
        /// Y-axis offset from origin (mm).
        /// </summary>
        public double OffsetY { get; set; } = 0.0;
    }
}
