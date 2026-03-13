namespace MakerPrompt.Shared.Services
{
    /// <summary>
    /// Abstracts camera frame fetching.
    /// Blazor WASM: browser fetches the img src directly (no proxy needed).
    /// MAUI: WebView can't load cross-origin http:// img tags — native HttpClient
    ///       fetches the bytes and returns a base64 data URL instead.
    /// </summary>
    public interface ICameraProxyService
    {
        /// <summary>True on platforms where the WebView blocks direct img src URLs.</summary>
        bool NativeProxyRequired { get; }

        /// <summary>
        /// Fetches a camera snapshot and returns a base64 data URL,
        /// or null if the proxy is not required / fetch failed.
        /// </summary>
        Task<string?> FetchSnapshotAsDataUrlAsync(string url, CancellationToken ct = default);
    }

    /// <summary>
    /// Default (Blazor WASM) implementation — passthrough.
    /// The browser handles img src loading natively.
    /// </summary>
    public sealed class PassthroughCameraProxyService : ICameraProxyService
    {
        public bool NativeProxyRequired => false;

        public Task<string?> FetchSnapshotAsDataUrlAsync(string url, CancellationToken ct = default)
            => Task.FromResult<string?>(null);
    }
}
