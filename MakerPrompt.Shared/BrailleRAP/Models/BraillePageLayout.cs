namespace MakerPrompt.Shared.BrailleRAP.Models
{
    /// <summary>
    /// Represents a paginated layout of Braille text.
    /// </summary>
    public class BraillePageLayout
    {
        /// <summary>
        /// Pages of Braille lines.
        /// Each page contains multiple lines, each line is a string of Braille characters.
        /// </summary>
        public List<List<string>> Pages { get; set; } = new();

        /// <summary>
        /// Gets the total number of pages.
        /// </summary>
        public int PageCount => Pages.Count;

        /// <summary>
        /// Gets a specific page by index.
        /// </summary>
        public List<string> GetPage(int pageIndex)
        {
            if (pageIndex < 0 || pageIndex >= Pages.Count)
                return new List<string>();
            return Pages[pageIndex];
        }
    }
}
