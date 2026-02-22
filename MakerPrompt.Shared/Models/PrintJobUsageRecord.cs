namespace MakerPrompt.Shared.Models
{
    public class PrintJobUsageRecord
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid PrinterId { get; set; }
        public Guid FilamentSpoolId { get; set; }
        public string JobName { get; set; } = string.Empty;
        public TimeSpan Duration { get; set; }
        public double EstimatedFilamentUsedGrams { get; set; }
        public double ActualFilamentUsedGrams { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
