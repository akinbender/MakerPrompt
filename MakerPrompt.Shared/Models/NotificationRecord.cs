namespace MakerPrompt.Shared.Models
{
    public enum NotificationLevel
    {
        Info,
        Warning,
        Error,
        Critical
    }

    public class NotificationRecord
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public NotificationLevel Level { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public Guid? PrinterId { get; set; }
        public Guid? FilamentSpoolId { get; set; }
        public bool IsRead { get; set; }
    }
}
