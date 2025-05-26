namespace MakerPrompt.Shared.Services
{
    public class PrinterStateService : IDisposable
    {
        private readonly PrinterCommunicationServiceFactory factory;
        public PrinterTelemetry Telemetry { get; } = new();

        public PrinterStateService(PrinterCommunicationServiceFactory factory)
        {
            this.factory = factory;
            this.factory.ConnectionStateChanged += HandleConnectionStateChanged;
        }

        private void HandleConnectionStateChanged(object? sender, bool isConnected)
        {
            if (isConnected && factory.Current != null)
            {
                factory.Current.TelemetryUpdated += HandleTelemetryUpdated;
            }
            else {
                if (factory.Current != null)
                {
                    factory.Current.TelemetryUpdated -= HandleTelemetryUpdated;
                }
            }
        }

        private void HandleTelemetryUpdated(object? sender, PrinterTelemetry newTelemetry)
        {
            if (factory.Current == null) return;
            Telemetry.HotendTemp = newTelemetry.HotendTemp;
            Telemetry.HotendTarget = newTelemetry.HotendTarget;
            Telemetry.BedTemp = newTelemetry.BedTemp;
            Telemetry.BedTarget = newTelemetry.BedTarget;
            Telemetry.Position = newTelemetry.Position;
            Telemetry.Status = newTelemetry.Status;
            Telemetry.FeedRate = newTelemetry.FeedRate;
            Telemetry.FlowRate = newTelemetry.FlowRate;
            //Telemetry.SDCard = newTelemetry.SDCard;
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
