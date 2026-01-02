namespace MakerPrompt.Shared.BrailleRAP.Models
{
    /// <summary>
    /// Represents the position of a dot in a Braille cell (0-7 for 8-dot Braille).
    /// Standard 6-dot positions: 0-5
    /// Extended 8-dot positions: 0-7
    /// </summary>
    public enum BrailleDotPosition
    {
        Dot1 = 0,  // Top-left
        Dot2 = 1,  // Middle-left
        Dot3 = 2,  // Bottom-left
        Dot4 = 3,  // Top-right
        Dot5 = 4,  // Middle-right
        Dot6 = 5,  // Bottom-right
        Dot7 = 6,  // Extended bottom-left
        Dot8 = 7   // Extended bottom-right
    }
}
