namespace MakerPrompt.Shared.Models
{
    /// <summary>
    /// Represents a saved farm profile. Each farm bundles a set of printer connections
    /// and a display name so users can switch between different physical setups.
    /// </summary>
    public class FarmConfiguration
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public List<PrinterConnectionDefinition> Printers { get; set; } = [];
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
