using System.Runtime.CompilerServices;

namespace MakerPrompt.Core.Models;

/// <summary>Lightweight wrapper around G-code text content.</summary>
public readonly record struct GCodeDoc(string Content)
{
    /// <summary>Async, streaming enumeration of non-empty, non-comment G-code commands.</summary>
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
