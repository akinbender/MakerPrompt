using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.IO;

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

    public readonly record struct GCodeDoc(string Content)
    {
        // Async, streaming enumeration of non-empty, non-comment commands.
        public async IAsyncEnumerable<string> EnumerateCommandsAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(Content)) yield break;

            using var reader = new StringReader(Content);
            string? line;

            while (!cancellationToken.IsCancellationRequested &&
                   (line = await reader.ReadLineAsync().ConfigureAwait(false)) != null)
            {
                line = line.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith(";", StringComparison.Ordinal))
                    continue;

                yield return line;
            }
        }
    }
}
