using MakerPrompt.Shared.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace MakerPrompt.Shared.Infrastructure
{
    public abstract class ConnectionComponentBase : ComponentBase, IAsyncDisposable
    {
        private IPrinterCommunicationService? _telemetrySource;

        [Inject]
        public required MakerPromptJsInterop JS { get; set; }

        [Inject]
        public required IStringLocalizer<Resources> Localizer { get; set; }

        [Inject]
        public required PrinterCommunicationServiceFactory PrinterServiceFactory { get; set; }

        protected bool IsConnected { get; set; }
        protected string ConnectionDisabledAttribute => IsConnected ? string.Empty : "disabled";

        private void OnTelemetryUpdated(object? sender, PrinterTelemetry e)
        {
            HandleTelemetryUpdated(sender, e);
            InvokeAsync(StateHasChanged);
        }

        protected override void OnInitialized()
        {
            IsConnected = PrinterServiceFactory.IsConnected;
            PrinterServiceFactory.ConnectionStateChanged += HandleConnectionChanged;
            UpdateTelemetrySubscription();

            base.OnInitialized();
        }

        protected virtual void HandleTelemetryUpdated(object? sender, PrinterTelemetry printerTelemetry) { }

        protected virtual void HandleConnectionChanged(object? sender, bool connected)
        {
            IsConnected = connected;
            UpdateTelemetrySubscription();
            StateHasChanged();
        }

        private void UpdateTelemetrySubscription()
        {
            if (_telemetrySource != null)
            {
                _telemetrySource.TelemetryUpdated -= OnTelemetryUpdated;
                _telemetrySource = null;
            }

            if (!IsConnected)
            {
                return;
            }

            var current = PrinterServiceFactory.Current;
            if (current != null)
            {
                current.TelemetryUpdated += OnTelemetryUpdated;
                _telemetrySource = current;
            }
        }

        public async ValueTask DisposeAsync()
        {
            PrinterServiceFactory.ConnectionStateChanged -= HandleConnectionChanged;
            if (_telemetrySource != null)
            {
                _telemetrySource.TelemetryUpdated -= OnTelemetryUpdated;
            }
            GC.SuppressFinalize(this);
        }
    }
}