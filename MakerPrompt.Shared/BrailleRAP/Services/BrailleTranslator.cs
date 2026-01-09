using MakerPrompt.Shared.BrailleRAP.Models;

namespace MakerPrompt.Shared.BrailleRAP.Services
{
    /// <summary>
    /// Supported Braille languages and translation tables.
    /// </summary>
    public enum BrailleLanguage
    {
        EnglishGrade1,
        FrenchGrade1,
        GermanGrade1,
        SpanishGrade1
    }

    /// <summary>
    /// Translates text to Braille using character mapping for multiple languages.
    /// </summary>
    public class BrailleTranslator
    {
        // Braille character constants
        private const char CapitalIndicator = '\u2820';
        private const char BlankCell = '\u2800';
        private const char NumberIndicator = '\u283C';

        private BrailleLanguage _currentLanguage = BrailleLanguage.EnglishGrade1;

        /// <summary>
        /// Sets the translation language.
        /// </summary>
        public void SetLanguage(BrailleLanguage language)
        {
            _currentLanguage = language;
        }

        /// <summary>
        /// Gets the current language.
        /// </summary>
        public BrailleLanguage GetLanguage() => _currentLanguage;

        // Basic English Grade 1 Braille mapping (simplified)
        // Unicode Braille patterns start at U+2800
        private static readonly Dictionary<char, char> EnglishGrade1Map = new()
        {
            // Letters (a-z)
            { 'a', '\u2801' }, { 'b', '\u2803' }, { 'c', '\u2809' }, { 'd', '\u2819' },
            { 'e', '\u2811' }, { 'f', '\u280B' }, { 'g', '\u281B' }, { 'h', '\u2813' },
            { 'i', '\u280A' }, { 'j', '\u281A' }, { 'k', '\u2805' }, { 'l', '\u2807' },
            { 'm', '\u280D' }, { 'n', '\u281D' }, { 'o', '\u2815' }, { 'p', '\u280F' },
            { 'q', '\u281F' }, { 'r', '\u2817' }, { 's', '\u280E' }, { 't', '\u281E' },
            { 'u', '\u2825' }, { 'v', '\u2827' }, { 'w', '\u283A' }, { 'x', '\u282D' },
            { 'y', '\u283D' }, { 'z', '\u2835' },
            
            // Capital letter indicator
            { 'A', CapitalIndicator }, { 'B', CapitalIndicator }, { 'C', CapitalIndicator }, { 'D', CapitalIndicator },
            { 'E', CapitalIndicator }, { 'F', CapitalIndicator }, { 'G', CapitalIndicator }, { 'H', CapitalIndicator },
            { 'I', CapitalIndicator }, { 'J', CapitalIndicator }, { 'K', CapitalIndicator }, { 'L', CapitalIndicator },
            { 'M', CapitalIndicator }, { 'N', CapitalIndicator }, { 'O', CapitalIndicator }, { 'P', CapitalIndicator },
            { 'Q', CapitalIndicator }, { 'R', CapitalIndicator }, { 'S', CapitalIndicator }, { 'T', CapitalIndicator },
            { 'U', CapitalIndicator }, { 'V', CapitalIndicator }, { 'W', CapitalIndicator }, { 'X', CapitalIndicator },
            { 'Y', CapitalIndicator }, { 'Z', CapitalIndicator },
            
            // Numbers
            { '1', '\u2801' }, { '2', '\u2803' }, { '3', '\u2809' }, { '4', '\u2819' },
            { '5', '\u2811' }, { '6', '\u280B' }, { '7', '\u281B' }, { '8', '\u2813' },
            { '9', '\u280A' }, { '0', '\u281A' },
            
            // Common punctuation
            { ' ', BlankCell },  // Blank cell for space
            { ',', '\u2802' },
            { '.', '\u2832' },
            { '?', '\u2826' },
            { '!', '\u2816' },
            { '\'', '\u2804' },
            { '-', '\u2824' },
            { ':', '\u2812' },
            { ';', '\u2822' },
            { '(', '\u2823' },
            { ')', '\u281C' },
            { '\n', '\n' },     // Preserve newlines
            { '\f', '\f' },     // Preserve form feeds
        };

        // French Grade 1 Braille mapping
        private static readonly Dictionary<char, char> FrenchGrade1Map = new()
        {
            // Letters (a-z) - same as English
            { 'a', '\u2801' }, { 'b', '\u2803' }, { 'c', '\u2809' }, { 'd', '\u2819' },
            { 'e', '\u2811' }, { 'f', '\u280B' }, { 'g', '\u281B' }, { 'h', '\u2813' },
            { 'i', '\u280A' }, { 'j', '\u281A' }, { 'k', '\u2805' }, { 'l', '\u2807' },
            { 'm', '\u280D' }, { 'n', '\u281D' }, { 'o', '\u2815' }, { 'p', '\u280F' },
            { 'q', '\u281F' }, { 'r', '\u2817' }, { 's', '\u280E' }, { 't', '\u281E' },
            { 'u', '\u2825' }, { 'v', '\u2827' }, { 'w', '\u283A' }, { 'x', '\u282D' },
            { 'y', '\u283D' }, { 'z', '\u2835' },
            
            // Capital letters
            { 'A', CapitalIndicator }, { 'B', CapitalIndicator }, { 'C', CapitalIndicator }, { 'D', CapitalIndicator },
            { 'E', CapitalIndicator }, { 'F', CapitalIndicator }, { 'G', CapitalIndicator }, { 'H', CapitalIndicator },
            { 'I', CapitalIndicator }, { 'J', CapitalIndicator }, { 'K', CapitalIndicator }, { 'L', CapitalIndicator },
            { 'M', CapitalIndicator }, { 'N', CapitalIndicator }, { 'O', CapitalIndicator }, { 'P', CapitalIndicator },
            { 'Q', CapitalIndicator }, { 'R', CapitalIndicator }, { 'S', CapitalIndicator }, { 'T', CapitalIndicator },
            { 'U', CapitalIndicator }, { 'V', CapitalIndicator }, { 'W', CapitalIndicator }, { 'X', CapitalIndicator },
            { 'Y', CapitalIndicator }, { 'Z', CapitalIndicator },
            
            // Numbers
            { '1', '\u2801' }, { '2', '\u2803' }, { '3', '\u2809' }, { '4', '\u2819' },
            { '5', '\u2811' }, { '6', '\u280B' }, { '7', '\u281B' }, { '8', '\u2813' },
            { '9', '\u280A' }, { '0', '\u281A' },
            
            // French accented characters
            { 'à', '\u282F' }, { 'â', '\u2801' }, { 'ç', '\u280F' },
            { 'é', '\u283F' }, { 'è', '\u282E' }, { 'ê', '\u2811' },
            { 'ë', '\u283B' }, { 'î', '\u280A' }, { 'ï', '\u283B' },
            { 'ô', '\u2815' }, { 'ù', '\u283D' }, { 'û', '\u2825' },
            { 'ü', '\u283B' }, { 'œ', '\u283B' },
            
            // Common punctuation
            { ' ', BlankCell },
            { ',', '\u2802' },
            { '.', '\u2832' },
            { '?', '\u2826' },
            { '!', '\u2816' },
            { '\'', '\u2804' },
            { '-', '\u2824' },
            { ':', '\u2812' },
            { ';', '\u2822' },
            { '(', '\u2823' },
            { ')', '\u281C' },
            { '\n', '\n' },
            { '\f', '\f' },
        };

        // German Grade 1 Braille mapping
        private static readonly Dictionary<char, char> GermanGrade1Map = new()
        {
            // Letters (a-z) - same as English
            { 'a', '\u2801' }, { 'b', '\u2803' }, { 'c', '\u2809' }, { 'd', '\u2819' },
            { 'e', '\u2811' }, { 'f', '\u280B' }, { 'g', '\u281B' }, { 'h', '\u2813' },
            { 'i', '\u280A' }, { 'j', '\u281A' }, { 'k', '\u2805' }, { 'l', '\u2807' },
            { 'm', '\u280D' }, { 'n', '\u281D' }, { 'o', '\u2815' }, { 'p', '\u280F' },
            { 'q', '\u281F' }, { 'r', '\u2817' }, { 's', '\u280E' }, { 't', '\u281E' },
            { 'u', '\u2825' }, { 'v', '\u2827' }, { 'w', '\u283A' }, { 'x', '\u282D' },
            { 'y', '\u283D' }, { 'z', '\u2835' },
            
            // Capital letters
            { 'A', CapitalIndicator }, { 'B', CapitalIndicator }, { 'C', CapitalIndicator }, { 'D', CapitalIndicator },
            { 'E', CapitalIndicator }, { 'F', CapitalIndicator }, { 'G', CapitalIndicator }, { 'H', CapitalIndicator },
            { 'I', CapitalIndicator }, { 'J', CapitalIndicator }, { 'K', CapitalIndicator }, { 'L', CapitalIndicator },
            { 'M', CapitalIndicator }, { 'N', CapitalIndicator }, { 'O', CapitalIndicator }, { 'P', CapitalIndicator },
            { 'Q', CapitalIndicator }, { 'R', CapitalIndicator }, { 'S', CapitalIndicator }, { 'T', CapitalIndicator },
            { 'U', CapitalIndicator }, { 'V', CapitalIndicator }, { 'W', CapitalIndicator }, { 'X', CapitalIndicator },
            { 'Y', CapitalIndicator }, { 'Z', CapitalIndicator },
            
            // Numbers
            { '1', '\u2801' }, { '2', '\u2803' }, { '3', '\u2809' }, { '4', '\u2819' },
            { '5', '\u2811' }, { '6', '\u280B' }, { '7', '\u281B' }, { '8', '\u2813' },
            { '9', '\u280A' }, { '0', '\u281A' },
            
            // German umlauts and special characters
            { 'ä', '\u2831' }, { 'Ä', '\u2831' },
            { 'ö', '\u283B' }, { 'Ö', '\u283B' },
            { 'ü', '\u283C' }, { 'Ü', '\u283C' },
            { 'ß', '\u282E' },
            
            // Common punctuation
            { ' ', BlankCell },
            { ',', '\u2802' },
            { '.', '\u2832' },
            { '?', '\u2826' },
            { '!', '\u2816' },
            { '\'', '\u2804' },
            { '-', '\u2824' },
            { ':', '\u2812' },
            { ';', '\u2822' },
            { '(', '\u2823' },
            { ')', '\u281C' },
            { '\n', '\n' },
            { '\f', '\f' },
        };

        // Spanish Grade 1 Braille mapping
        private static readonly Dictionary<char, char> SpanishGrade1Map = new()
        {
            // Letters (a-z) - same as English
            { 'a', '\u2801' }, { 'b', '\u2803' }, { 'c', '\u2809' }, { 'd', '\u2819' },
            { 'e', '\u2811' }, { 'f', '\u280B' }, { 'g', '\u281B' }, { 'h', '\u2813' },
            { 'i', '\u280A' }, { 'j', '\u281A' }, { 'k', '\u2805' }, { 'l', '\u2807' },
            { 'm', '\u280D' }, { 'n', '\u281D' }, { 'o', '\u2815' }, { 'p', '\u280F' },
            { 'q', '\u281F' }, { 'r', '\u2817' }, { 's', '\u280E' }, { 't', '\u281E' },
            { 'u', '\u2825' }, { 'v', '\u2827' }, { 'w', '\u283A' }, { 'x', '\u282D' },
            { 'y', '\u283D' }, { 'z', '\u2835' },
            
            // Capital letters
            { 'A', CapitalIndicator }, { 'B', CapitalIndicator }, { 'C', CapitalIndicator }, { 'D', CapitalIndicator },
            { 'E', CapitalIndicator }, { 'F', CapitalIndicator }, { 'G', CapitalIndicator }, { 'H', CapitalIndicator },
            { 'I', CapitalIndicator }, { 'J', CapitalIndicator }, { 'K', CapitalIndicator }, { 'L', CapitalIndicator },
            { 'M', CapitalIndicator }, { 'N', CapitalIndicator }, { 'O', CapitalIndicator }, { 'P', CapitalIndicator },
            { 'Q', CapitalIndicator }, { 'R', CapitalIndicator }, { 'S', CapitalIndicator }, { 'T', CapitalIndicator },
            { 'U', CapitalIndicator }, { 'V', CapitalIndicator }, { 'W', CapitalIndicator }, { 'X', CapitalIndicator },
            { 'Y', CapitalIndicator }, { 'Z', CapitalIndicator },
            
            // Numbers
            { '1', '\u2801' }, { '2', '\u2803' }, { '3', '\u2809' }, { '4', '\u2819' },
            { '5', '\u2811' }, { '6', '\u280B' }, { '7', '\u281B' }, { '8', '\u2813' },
            { '9', '\u280A' }, { '0', '\u281A' },
            
            // Spanish accented characters
            { 'á', '\u2831' }, { 'é', '\u283F' }, { 'í', '\u280C' },
            { 'ó', '\u283B' }, { 'ú', '\u283C' },
            { 'ñ', '\u283B' }, { 'ü', '\u283C' },
            { '¿', '\u2826' }, { '¡', '\u2816' },
            
            // Common punctuation
            { ' ', BlankCell },
            { ',', '\u2802' },
            { '.', '\u2832' },
            { '?', '\u2826' },
            { '!', '\u2816' },
            { '\'', '\u2804' },
            { '-', '\u2824' },
            { ':', '\u2812' },
            { ';', '\u2822' },
            { '(', '\u2823' },
            { ')', '\u281C' },
            { '\n', '\n' },
            { '\f', '\f' },
        };

        /// <summary>
        /// Gets the appropriate translation map for the current language.
        /// </summary>
        private Dictionary<char, char> GetTranslationMap()
        {
            return _currentLanguage switch
            {
                BrailleLanguage.FrenchGrade1 => FrenchGrade1Map,
                BrailleLanguage.GermanGrade1 => GermanGrade1Map,
                BrailleLanguage.SpanishGrade1 => SpanishGrade1Map,
                _ => EnglishGrade1Map
            };
        }

        /// <summary>
        /// Translates plain text to Braille.
        /// </summary>
        /// <param name="text">The text to translate.</param>
        /// <returns>List of Braille lines.</returns>
        public List<string> Translate(string text)
        {
            if (string.IsNullOrEmpty(text))
                return [];

            var translationMap = GetTranslationMap();

            // Split by newlines but preserve form feeds
            var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.None)
                .Where(line => !string.IsNullOrEmpty(line) || line == string.Empty)
                .ToList();

            var brailleLines = new List<string>();

            foreach (var line in lines)
            {
                if (line.Contains('\f'))
                {
                    brailleLines.Add("\f");
                    continue;
                }

                var brailleLine = new System.Text.StringBuilder();

                foreach (var ch in line)
                {
                    if (char.IsUpper(ch))
                    {
                        // Add capital indicator before the letter
                        brailleLine.Append(CapitalIndicator);
                        // Then add the lowercase version
                        var lower = char.ToLower(ch);
                        if (translationMap.TryGetValue(lower, out var brailleChar))
                        {
                            brailleLine.Append(brailleChar);
                        }
                        else
                        {
                            // Unknown character, use blank
                            brailleLine.Append(BlankCell);
                        }
                    }
                    else if (translationMap.TryGetValue(ch, out var brailleChar))
                    {
                        if (brailleChar != '\n' && brailleChar != '\f')
                            brailleLine.Append(brailleChar);
                    }
                    else
                    {
                        // Unknown character, use blank cell
                        brailleLine.Append(BlankCell);
                    }
                }

                brailleLines.Add(brailleLine.ToString());
            }

            return brailleLines;
        }
    }
}
