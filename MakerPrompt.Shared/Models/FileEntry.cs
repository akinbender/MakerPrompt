namespace MakerPrompt.Shared.Models
{
    public class FileEntry
    {
        public string FullPath { get; set; } = string.Empty;
        public long Size { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public bool IsAvailable { get; set; } = true;
    }
}
