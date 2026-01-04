namespace MakerPrompt.Shared.Services
{
    public class GCodeDocumentService
    {
        private string? _current;
        public string? CurrentGCode => _current;
        public event Action? Changed;

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
}
