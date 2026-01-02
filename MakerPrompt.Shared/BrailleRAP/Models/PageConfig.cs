namespace MakerPrompt.Shared.BrailleRAP.Models
{
    /// <summary>
    /// Configuration for Braille page layout.
    /// </summary>
    public class PageConfig
    {
        /// <summary>
        /// Number of Braille cells per line.
        /// </summary>
        public int Columns { get; set; } = 28;

        /// <summary>
        /// Maximum number of lines per page.
        /// </summary>
        public int Rows { get; set; } = 21;

        /// <summary>
        /// Line spacing multiplier (0 = normal, 1 = double space, etc.).
        /// </summary>
        public int LineSpacing { get; set; } = 0;

        /// <summary>
        /// Gets the computed number of rows based on spacing.
        /// </summary>
        public int GetComputedRows()
        {
            return (int)Math.Floor(Rows / ((LineSpacing * 0.5) + 1));
        }
    }
}
