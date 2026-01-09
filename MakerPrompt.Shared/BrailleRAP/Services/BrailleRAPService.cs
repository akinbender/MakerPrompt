using MakerPrompt.Shared.BrailleRAP.Models;

namespace MakerPrompt.Shared.BrailleRAP.Services
{
    /// <summary>
    /// Main service for BrailleRAP operations.
    /// Coordinates translation, pagination, and G-code generation.
    /// </summary>
    public class BrailleRAPService
    {
        private readonly BrailleTranslator _translator;
        private readonly BraillePaginator _paginator;
        private PageConfig _pageConfig;
        private MachineConfig _machineConfig;
        private BrailleLanguage _currentLanguage;

        public BrailleRAPService()
        {
            _translator = new BrailleTranslator();
            _paginator = new BraillePaginator();
            _pageConfig = new PageConfig();
            _machineConfig = new MachineConfig();
            _currentLanguage = BrailleLanguage.EnglishGrade1;
        }

        /// <summary>
        /// Sets the Braille translation language.
        /// </summary>
        public void SetLanguage(BrailleLanguage language)
        {
            _currentLanguage = language;
            _translator.SetLanguage(language);
        }

        /// <summary>
        /// Gets the current Braille language.
        /// </summary>
        public BrailleLanguage GetLanguage() => _currentLanguage;

        /// <summary>
        /// Sets the page configuration.
        /// </summary>
        public void SetPageConfig(PageConfig config)
        {
            _pageConfig = config;
            _paginator.SetConfig(config);
        }

        /// <summary>
        /// Sets the machine configuration.
        /// </summary>
        public void SetMachineConfig(MachineConfig config)
        {
            _machineConfig = config;
        }

        /// <summary>
        /// Gets the current page configuration.
        /// </summary>
        public PageConfig GetPageConfig() => _pageConfig;

        /// <summary>
        /// Gets the current machine configuration.
        /// </summary>
        public MachineConfig GetMachineConfig() => _machineConfig;

        /// <summary>
        /// Translates text to Braille and returns the layout.
        /// </summary>
        public BraillePageLayout TranslateAndLayout(string text)
        {
            // Translate text to Braille
            var brailleLines = _translator.Translate(text);

            // Paginate
            _paginator.SetSourceLines(brailleLines);

            return _paginator.GetLayout();
        }

        /// <summary>
        /// Generates G-code from text for a specific page.
        /// </summary>
        public string GenerateGCode(string text, int pageIndex = 0)
        {
            var layout = TranslateAndLayout(text);

            if (layout.PageCount == 0 || pageIndex >= layout.PageCount)
                return string.Empty;

            var generator = new BrailleGCodeGenerator(_machineConfig);
            return generator.GenerateGCodeFromLayout(layout, pageIndex);
        }

        /// <summary>
        /// Gets a preview of the Braille text for a specific page.
        /// </summary>
        public List<string> GetBraillePreview(string text, int pageIndex = 0)
        {
            var layout = TranslateAndLayout(text);

            if (layout.PageCount == 0 || pageIndex >= layout.PageCount)
                return [];

            return layout.GetPage(pageIndex);
        }

        /// <summary>
        /// Gets statistics about the translated and paginated text.
        /// </summary>
        public (int PageCount, int TotalLines, int TotalCharacters) GetStatistics(string text)
        {
            var layout = TranslateAndLayout(text);

            int totalLines = 0;
            int totalChars = 0;

            foreach (var page in layout.Pages)
            {
                totalLines += page.Count;
                foreach (var line in page)
                {
                    totalChars += line.Length;
                }
            }

            return (layout.PageCount, totalLines, totalChars);
        }
    }
}
