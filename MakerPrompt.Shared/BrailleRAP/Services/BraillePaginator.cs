using MakerPrompt.Shared.BrailleRAP.Models;

namespace MakerPrompt.Shared.BrailleRAP.Services
{
    /// <summary>
    /// Paginates Braille text into pages based on column and row constraints.
    /// Ported from AccessBrailleRAP's BraillePaginator.js
    /// </summary>
    public class BraillePaginator
    {
        private const char BlankBrailleCell = '\u2800';
        
        private PageConfig _config;
        private List<string> _sourceLines;
        private List<List<string>> _pages;
        private List<string> _currentPage;
        private int _computedRows;

        public BraillePaginator()
        {
            _config = new PageConfig();
            _sourceLines = [];
            _pages = [];
            _currentPage = [];
            _computedRows = _config.GetComputedRows();
        }

        /// <summary>
        /// Sets the page configuration.
        /// </summary>
        public void SetConfig(PageConfig config)
        {
            _config = config;
            ComputeRows();
            Update();
        }

        /// <summary>
        /// Sets the source Braille lines to paginate.
        /// </summary>
        public void SetSourceLines(List<string> lines)
        {
            _sourceLines = lines;
            Update();
        }

        /// <summary>
        /// Gets the paginated layout.
        /// </summary>
        public BraillePageLayout GetLayout()
        {
            return new BraillePageLayout { Pages = new List<List<string>>(_pages) };
        }

        private void ComputeRows()
        {
            _computedRows = _config.GetComputedRows();
        }

        private void AddLine(string line)
        {
            _currentPage.Add(line);
            if (_currentPage.Count >= _computedRows)
            {
                _pages.Add(new List<string>(_currentPage));
                _currentPage.Clear();
            }
        }

        private void FlushLine()
        {
            if (_currentPage.Count > 0)
            {
                _pages.Add(new List<string>(_currentPage));
                _currentPage.Clear();
            }
        }

        private void Update()
        {
            if (_sourceLines == null)
                return;

            _pages.Clear();
            _currentPage.Clear();
            ComputeRows();

            foreach (var srcLine in _sourceLines)
            {
                // Split by blank Braille cells (U+2800 is the blank cell used as space)
                var words = srcLine.Split(BlankBrailleCell);

                var currentLine = "";

                foreach (var word in words)
                {
                    // Check for form feed
                    if (word == "\f")
                    {
                        AddLine(currentLine);
                        currentLine = "";
                        FlushLine();
                        continue;
                    }

                    // Check if adding this word would exceed column limit
                    if (word.Length + currentLine.Length >= _config.Columns)
                    {
                        if (currentLine.Length > 0)
                        {
                            // Add current line and start new one
                            AddLine(currentLine);

                            if (word.Length < _config.Columns)
                            {
                                currentLine = word + BlankBrailleCell;
                            }
                            else
                            {
                                // Word is too long, need to split it
                                currentLine = "";
                                SplitLongWord(word);
                            }
                        }
                        else
                        {
                            // Need to split a long word
                            SplitLongWord(word);
                            currentLine = "";
                        }
                    }
                    else
                    {
                        currentLine += word;
                        currentLine += BlankBrailleCell;
                    }
                }

                if (currentLine.Length > 0)
                {
                    AddLine(currentLine);
                }
            }

            FlushLine();
        }

        private void SplitLongWord(string word)
        {
            int start = 0;
            while (start < word.Length)
            {
                int chunkSize = Math.Min(_config.Columns, word.Length - start);
                string chunk = word.Substring(start, chunkSize);
                AddLine(chunk);
                start += chunkSize;
            }
        }
    }
}
