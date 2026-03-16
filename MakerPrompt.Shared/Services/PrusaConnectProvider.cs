namespace MakerPrompt.Shared.Services;

/// <summary>
/// Discovers printers registered to a PrusaConnect account via the mobile API.
///
/// Base URL: https://connect-mobile-api.prusa3d.com
/// Auth: Authorization: Bearer {token}
///
/// Usage:
///   provider.Configure(bearerToken);
///   var printers = await provider.GetPrintersAsync();  // GET /api/v1/printers
/// </summary>
public sealed class PrusaConnectProvider : IPrinterProvider
{
    private const string BaseUrl = "https://connect-mobile-api.prusa3d.com";

    private readonly HttpClient _httpClient;

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString,
    };

    public PrusaConnectProvider()
    {
        _httpClient = new HttpClient { BaseAddress = new Uri(BaseUrl) };
    }

    // Constructor for testing with a custom handler.
    public PrusaConnectProvider(HttpMessageHandler handler)
    {
        _httpClient = new HttpClient(handler, false) { BaseAddress = new Uri(BaseUrl) };
    }

    /// <summary>Sets the Bearer token used for all subsequent requests.</summary>
    public void Configure(string bearerToken)
    {
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", bearerToken);
    }

    /// <summary>
    /// Returns all printers associated with the configured account.
    /// Returns an empty list on auth failure or network error.
    /// </summary>
    public async Task<IReadOnlyList<RemotePrinterInfo>> GetPrintersAsync()
    {
        try
        {
            using var response = await _httpClient.GetAsync("/api/v1/printers?page=1&itemsPerPage=100");
            if (!response.IsSuccessStatusCode) return [];

            await using var stream = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);

            var root = doc.RootElement;
            JsonElement printerArray;
            if (root.ValueKind == JsonValueKind.Array)
                printerArray = root;
            else if (root.TryGetProperty("printers", out var nested))
                printerArray = nested;
            else
                return [];

            var result = new List<RemotePrinterInfo>();
            foreach (var item in printerArray.EnumerateArray())
            {
                var id     = item.TryGetProperty("uuid",         out var p1) ? p1.GetString() ?? string.Empty : string.Empty;
                var name   = item.TryGetProperty("name",         out var p2) ? p2.GetString() ?? string.Empty : string.Empty;
                var model  = item.TryGetProperty("printer_type", out var p3) ? p3.GetString() ?? string.Empty : string.Empty;
                var status = item.TryGetProperty("state",        out var p4) ? p4.GetString() ?? string.Empty : string.Empty;

                if (string.IsNullOrEmpty(id)) continue;

                result.Add(new RemotePrinterInfo { Id = id, Name = name, Model = model, Status = status });
            }

            return result;
        }
        catch
        {
            return [];
        }
    }
}
