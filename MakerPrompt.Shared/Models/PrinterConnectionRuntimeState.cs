namespace MakerPrompt.Shared.Models
{
    public sealed class PrinterConnectionRuntimeState
    {
        public Guid ConnectionId { get; set; }
        public string Name { get; set; } = "Printer";
        public PrinterConnectionType PrinterType { get; set; } = PrinterConnectionType.Demo;
        public bool Enabled { get; set; }
        public bool IsConnected { get; set; }
        public bool IsPrinting { get; set; }
        public PrinterTelemetry Telemetry { get; set; } = new();
        public string? LastError { get; set; }
        public DateTimeOffset LastUpdatedUtc { get; set; } = DateTimeOffset.UtcNow;
    }
}
