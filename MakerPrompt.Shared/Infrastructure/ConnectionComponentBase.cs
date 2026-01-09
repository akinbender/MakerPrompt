using MakerPrompt.Shared.Models;
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
            if (PrinterServiceFactory.Current != null)
            {
                PrinterServiceFactory.Current.TelemetryUpdated += OnTelemetryUpdated;
            }

            base.OnInitialized();
        }

        protected virtual void HandleTelemetryUpdated(object? sender, PrinterTelemetry printerTelemetry) { }

        protected virtual void HandleConnectionChanged(object? sender, bool connected)
        {
            IsConnected = connected;
            if (PrinterServiceFactory.Current != null)
            {
                if (IsConnected)
                {
                    PrinterServiceFactory.Current.TelemetryUpdated += OnTelemetryUpdated;
                }
                else
                {
                    PrinterServiceFactory.Current.TelemetryUpdated -= OnTelemetryUpdated;
                }
            }
            StateHasChanged();
        }

        public async ValueTask DisposeAsync()
        {
            PrinterServiceFactory.ConnectionStateChanged -= HandleConnectionChanged;
            if (PrinterServiceFactory.Current != null)
            {
                PrinterServiceFactory.Current.TelemetryUpdated -= OnTelemetryUpdated;
            }
            GC.SuppressFinalize(this);
        }
    }
}