namespace MakerPrompt.Shared.Models
{
    public partial class GCodeParameter(char label, string description)
    {
        public char Label { get; } = label;
        public string Description { get; } = description;
        public string Value { get; set; } = string.Empty;

        // public bool ValueRequired { get; set; } = false;
    }
}
