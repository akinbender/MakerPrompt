using MakerPrompt.Shared.Services;

namespace MakerPrompt.MAUI.Services
{
    /// <summary>
    /// MAUI camera proxy: fetches snapshot images via the native HttpClient,
    /// bypassing the WebView's cross-origin restrictions, and returns them
    /// as base64 data URLs so the img tag renders without CORS issues.
    /// </summary>
    public sealed class MauiCameraProxyService : ICameraProxyService, IDisposable
    {
        private readonly HttpClient _http;

        public MauiCameraProxyService()
        {
            _http = new HttpClient(new HttpClientHandler
            {
                // Allow HTTP (non-TLS) camera endpoints on local network
                ServerCertificateCustomValidationCallback = null
            })
            {
                Timeout = TimeSpan.FromSeconds(5)
            };
        }

        public bool NativeProxyRequired => true;

        public async Task<string?> FetchSnapshotAsDataUrlAsync(string url, CancellationToken ct = default)
        {
            try
            {
                var bytes = await _http.GetByteArrayAsync(url, ct).ConfigureAwait(false);
                return "data:image/jpeg;base64," + Convert.ToBase64String(bytes);
            }
            catch
            {
                return null;
            }
        }

        public void Dispose() => _http.Dispose();
    }
}
