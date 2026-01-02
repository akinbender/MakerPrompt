namespace MakerPrompt.Shared.BrailleRAP.Models
{
    /// <summary>
    /// Represents a single Braille cell using Unicode Braille Patterns (U+2800 to U+28FF).
    /// </summary>
    public class BrailleCell
    {
        /// <summary>
        /// The Unicode character representing this Braille cell.
        /// </summary>
        public char Character { get; set; }

        /// <summary>
        /// Creates a Braille cell from a Unicode Braille character.
        /// </summary>
        public BrailleCell(char character)
        {
            Character = character;
        }

        /// <summary>
        /// Gets the value (0-255) representing the dot pattern.
        /// </summary>
        public int GetValue()
        {
            return Character - 0x2800;
        }

        /// <summary>
        /// Checks if a specific dot is raised in this cell.
        /// </summary>
        public bool HasDot(BrailleDotPosition position)
        {
            int value = GetValue();
            return (value & (1 << (int)position)) != 0;
        }

        public override string ToString()
        {
            return Character.ToString();
        }
    }
}
