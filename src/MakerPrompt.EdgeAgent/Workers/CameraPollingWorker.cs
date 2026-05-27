using MakerPrompt.Core.Abstractions;
using MakerPrompt.Core.Models;
using MakerPrompt.Infrastructure.Camera;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;

namespace MakerPrompt.EdgeAgent.Workers;

/// <summary>
/// Background worker that captures periodic snapshots from all configured camera
/// feeds and persists them via <see cref="ICameraSnapshotStore"/>.
///
/// Cameras are configured in <c>appsettings.json</c> (or environment variables)
/// under the <c>EdgeAgent:Cameras</c> array:
///
/// <code>
/// "EdgeAgent": {
///   "CameraIntervalSeconds": 10,
///   "Cameras": [
///     { "CameraId": "printer-1", "Label": "Ender-3 Cam", "MjpegUrl": "http://192.168.1.10:8080/?action=snapshot" },
///     { "CameraId": "printer-2", "Label": "Voron Cam",   "MjpegUrl": "http://192.168.1.11/webcam/?action=snapshot" }
///   ]
/// }
/// </code>
///
/// Each camera entry must supply a <c>CameraId</c> (matching the printer it monitors),
/// a <c>Label</c>, and an MJPEG snapshot or stream <c>MjpegUrl</c>.
/// </summary>
public sealed class CameraPollingWorker : BackgroundService
{
    private readonly ICameraSnapshotStore _store;
    private readonly ILogger<CameraPollingWorker> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly TimeSpan _captureInterval;
    private readonly List<MjpegCameraProvider> _cameras = [];

    public CameraPollingWorker(
        ICameraSnapshotStore store,
        ILoggerFactory loggerFactory,
        IConfiguration configuration)
    {
        _store = store;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<CameraPollingWorker>();

        var intervalSeconds = configuration.GetValue("EdgeAgent:CameraIntervalSeconds", 10);
        _captureInterval = TimeSpan.FromSeconds(Math.Max(1, intervalSeconds));

        // Build providers from EdgeAgent:Printers entries that have a CameraUrl.
        var printersSection = configuration.GetSection("EdgeAgent:Printers");
        foreach (var printer in printersSection.GetChildren())
        {
            var cameraId = printer["PrinterId"] ?? string.Empty;
            var label = printer["Label"] ?? cameraId;
            var url = printer["CameraUrl"] ?? string.Empty;

            if (string.IsNullOrWhiteSpace(cameraId) || string.IsNullOrWhiteSpace(url))
                continue;

            _cameras.Add(new MjpegCameraProvider(
                cameraId, label, url,
                loggerFactory.CreateLogger<MjpegCameraProvider>()));
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_cameras.Count == 0)
        {
            _logger.LogInformation(
                "CameraPollingWorker: no cameras configured — worker idle");
            return;
        }

        _logger.LogInformation(
            "CameraPollingWorker started — {Count} camera(s), interval {Interval}",
            _cameras.Count, _captureInterval);

        // Verify availability before first capture cycle.
        foreach (var cam in _cameras)
            await cam.CheckAvailabilityAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await CaptureAllAsync(stoppingToken);
            await Task.Delay(_captureInterval, stoppingToken).ConfigureAwait(false);
        }

        _logger.LogInformation("CameraPollingWorker stopped");
    }

    private async Task CaptureAllAsync(CancellationToken ct)
    {
        foreach (var cam in _cameras)
        {
            try
            {
                var jpeg = await cam.CaptureSnapshotAsync(ct);
                if (jpeg.Length == 0) continue;

                var snapshot = new CameraSnapshot
                {
                    CameraId = cam.CameraId,
                    Label = cam.Label,
                    JpegData = jpeg,
                    CapturedAt = DateTimeOffset.UtcNow
                };

                await _store.SaveAsync(snapshot, ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                // Swallow per-camera errors — log at Debug to avoid spam.
                _logger.LogDebug(ex,
                    "Capture error for camera {CameraId}", cam.CameraId);
            }
        }
    }

    public override void Dispose()
    {
        // DisposeAsync() is not available as an override on BackgroundService in .NET 10.
        // Use synchronous dispose here and rely on the host's graceful shutdown for cleanup.
        foreach (var cam in _cameras)
        {
            var disposeTask = cam.DisposeAsync();
            if (!disposeTask.IsCompleted)
                disposeTask.AsTask().GetAwaiter().GetResult();
        }

        base.Dispose();
    }
}
