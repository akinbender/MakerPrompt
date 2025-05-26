using Microsoft.AspNetCore.Components;

namespace MakerPrompt.Shared.Infrastructure
{
    public class ConnectionComponentBase : ComponentBase, IAsyncDisposable
    {
        [Inject]
        protected PrinterCommunicationServiceFactory PrinterServiceFactory { get; set; }
        protected bool IsConnected { get; set; }

        protected override void OnInitialized()
        {
            PrinterServiceFactory.ConnectionStateChanged += HandleConnectionChanged;
            if (PrinterServiceFactory.Current != null)
            {
                PrinterServiceFactory.Current.TelemetryUpdated += HandleTelemetryUpdated;
            }
        }

        private void HandleConnectionChanged(object sender, bool connected)
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
            InvokeAsync(StateHasChanged);
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