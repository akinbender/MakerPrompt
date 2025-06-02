using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace MakerPrompt.Shared.Infrastructure
{
    public abstract class ConnectionComponentBase : ComponentBase, IAsyncDisposable
    {
        [Inject]
        public required MakerPromptJsInterop JS { get; set; }

        [Inject]
        public required IStringLocalizer<Resources> Localizer { get; set; }

        [Inject]
        public required PrinterCommunicationServiceFactory PrinterServiceFactory { get; set; }

        protected bool IsConnected { get; set; }
        protected string ConnectionCssClass => IsConnected ? string.Empty : "disabled";

        protected override void OnInitialized()
        {
            IsConnected = PrinterServiceFactory.IsConnected;
            PrinterServiceFactory.ConnectionStateChanged += HandleConnectionChanged;
            if (PrinterServiceFactory.Current != null)
            {
                PrinterServiceFactory.Current.TelemetryUpdated += HandleTelemetryUpdated;
            }
        }

        protected virtual void HandleTelemetryUpdated(object? sender, PrinterTelemetry printerTelemetry) { }

        protected virtual void HandleConnectionChanged(object? sender, bool connected)
        {
            IsConnected = connected;
            if (PrinterServiceFactory.Current != null)
            {
                if (IsConnected)
                {
                    PrinterServiceFactory.Current.TelemetryUpdated += HandleTelemetryUpdated;
                }
                else
                {
                    PrinterServiceFactory.Current.TelemetryUpdated -= HandleTelemetryUpdated;
                }
            }
            StateHasChanged();
        }

        public async ValueTask DisposeAsync()
        {
            PrinterServiceFactory.ConnectionStateChanged -= HandleConnectionChanged;
            if (PrinterServiceFactory.Current != null)
            {
                PrinterServiceFactory.Current.TelemetryUpdated -= HandleTelemetryUpdated;
            }
        }
    }
}