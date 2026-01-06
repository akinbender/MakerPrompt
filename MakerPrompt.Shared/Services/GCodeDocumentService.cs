namespace MakerPrompt.Shared.Services
{
    // Simple wrapper around the current G-code text; can be extended later to expose parsed structures
    public class GCodeDocumentService
    {
        private string? _current;
        public string? CurrentGCode => _current;
        public event Action? Changed;

        // Expose a lightweight document wrapper for higher-level APIs
        public GCodeDoc Document => new(_current ?? string.Empty);

        public void SetGCode(string? gcode)
        {
            _current = gcode ?? string.Empty;
            Changed?.Invoke();
        }

        public void Clear()
        {
            _current = string.Empty;
            Changed?.Invoke();
        }
    }

    public readonly record struct GCodeDoc(string Content);
}
