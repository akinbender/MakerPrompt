using MakerPrompt.Core.Abstractions;
using MakerPrompt.Core.Models;

namespace MakerPrompt.UI.Components.Services;

/// <summary>
/// Discovers printers registered to a PrusaConnect account via the mobile API.
///
/// Base URL: https://connect-mobile-api.prusa3d.com
/// Auth: Authorization: Bearer {token}
///
/// Usage:
///   await provider.ConfigureAsync(bearerToken);
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

    public PrinterConnectionType ProviderType => PrinterConnectionType.PrusaConnect;

    /// <summary>Sets the Bearer token used for all subsequent requests.</summary>
    public Task ConfigureAsync(string bearerToken, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", bearerToken);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Returns all printers associated with the configured account.
    /// Returns an empty list on auth failure or network error.
    /// </summary>
    public async Task<IReadOnlyList<PrinterInfo>> GetPrintersAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync("/api/v1/printers?page=1&itemsPerPage=100", cancellationToken);
            if (!response.IsSuccessStatusCode) return [];

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            var root = doc.RootElement;
            JsonElement printerArray;
            if (root.ValueKind == JsonValueKind.Array)
                printerArray = root;
            else if (root.TryGetProperty("printers", out var nested))
                printerArray = nested;
            else
                return [];

            var result = new List<PrinterInfo>();
            foreach (var item in printerArray.EnumerateArray())
            {
                var id     = item.TryGetProperty("uuid",         out var p1) ? p1.GetString() ?? string.Empty : string.Empty;
                var name   = item.TryGetProperty("name",         out var p2) ? p2.GetString() ?? string.Empty : string.Empty;
                var model  = item.TryGetProperty("printer_type", out var p3) ? p3.GetString() ?? string.Empty : string.Empty;
                var status = GetState(item);

                if (string.IsNullOrEmpty(id)) continue;

                result.Add(new PrinterInfo
                {
                    Id = id,
                    Name = name,
                    Model = model,
                    RawStatus = status,
                    Status = MapState(status),
                    ProviderType = PrinterConnectionType.PrusaConnect
                });
            }

            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return [];
        }
    }

    private static string GetState(JsonElement printer)
    {
        if (printer.TryGetProperty("state", out var state) &&
            state.ValueKind == JsonValueKind.String)
        {
            return state.GetString() ?? string.Empty;
        }

        if (printer.TryGetProperty("printer_state", out var printerState) &&
            printerState.ValueKind == JsonValueKind.Object)
        {
            if (printerState.TryGetProperty("text", out var text) &&
                text.ValueKind == JsonValueKind.String)
                return text.GetString() ?? string.Empty;

            if (printerState.TryGetProperty("state", out var nestedState) &&
                nestedState.ValueKind == JsonValueKind.String)
                return nestedState.GetString() ?? string.Empty;
        }

        return string.Empty;
    }

    private static PrinterStatus MapState(string? state) => state?.ToUpperInvariant() switch
    {
        "PRINTING" => PrinterStatus.Printing,
        "PAUSED" => PrinterStatus.Paused,
        "ERROR" or "ATTENTION" => PrinterStatus.Error,
        "IDLE" or "READY" or "FINISHED" or "STOPPED" => PrinterStatus.Connected,
        _ => PrinterStatus.Disconnected
    };
}
