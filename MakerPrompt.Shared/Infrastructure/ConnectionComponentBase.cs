using Microsoft.AspNetCore.Components;

namespace MakerPrompt.Shared.Infrastructure
{
    public class ConnectionComponentBase : ComponentBase, IAsyncDisposable
    {
        private readonly PrinterCommunicationServiceFactory factory;
        protected bool IsConnected { get; set; }

        public ConnectionComponentBase(PrinterCommunicationServiceFactory factory)
        {
            this.factory = factory;
        }

        protected override void OnInitialized()
        {
            factory.ConnectionStateChanged += HandleConnectionChanged;
            if (factory.Current != null)
            {
                factory.Current.TelemetryUpdated += HandleTelemetryUpdated;
            }
        }

        private void HandleConnectionChanged(object sender, bool connected)
        {
            isConnected = connected;
            if (factory.Current != null)
            {
                if (isConnected)
                {
                    factory.Current.TelemetryUpdated += HandleTelemetryUpdated;
                }
                else
                {
                    factory.Current.TelemetryUpdated -= HandleTelemetryUpdated;
                }
            }
            var message = connected ? string.Format(localizer[Resources.CommandPrompt_ConnectedMessage], factory.Current.ConnectionName) 
                                    : string.Format(localizer[Resources.CommandPrompt_DisconnectedMessage], factory.Current.ConnectionName);
            AddSystemMessage(message);
            InvokeAsync(StateHasChanged);
        }

        public async ValueTask DisposeAsync()
        {
            factory.ConnectionStateChanged -= HandleConnectionChanged;
            if (factory.Current != null)
            {
                factory.Current.TelemetryUpdated -= HandleTelemetryUpdated;
            }
        }
    }
}