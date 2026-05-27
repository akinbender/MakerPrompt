namespace MakerPrompt.Infrastructure.Printers
{
    public class DemoPrinterService : BasePrinterConnectionService, IPrinterCommunicationService
    {
        private readonly List<FileEntry> files = [];
        private readonly Dictionary<string, byte[]> fileContents = [];
        private double _hotendTarget = 0;
        private double _bedTarget = 0;
        private double _hotendTemp = 25;
        private double _bedTemp = 25;
        private int _fanSpeed = 0;
        private int _feedRate = 100;
        private int _flowRate = 100;
        private Vector3 _position = new(0, 0, 0);

        public override PrinterConnectionType ConnectionType => PrinterConnectionType.Demo;

        public DemoPrinterService()
        {
            ConnectionName = "Demo 3D Printer";
            updateTimer.Elapsed += (s, e) => SimulateTelemetry();
        }

        public async Task<bool> ConnectAsync(PrinterConnectionSettings connectionSettings)
        {
            IsConnected = true;
            LastTelemetry = new PrinterTelemetry
            {
                PrinterName = "Demo 3D Printer",
                ConnectionTime = DateTime.Now,
                HotendTemp = _hotendTemp,
                HotendTarget = _hotendTarget,
                BedTemp = _bedTemp,
                BedTarget = _bedTarget,
                Position = _position,
                Status = PrinterStatus.Connected,
                FeedRate = _feedRate,
                FlowRate = _flowRate,
                FanSpeed = _fanSpeed
            };
            RaiseConnectionChanged();
            updateTimer.Start();
            await Task.Delay(300);
            RaiseTelemetryUpdated();
            return true;
        }

        public async Task DisconnectAsync()
        {
            updateTimer.Stop();
            IsConnected = false;
            LastTelemetry.Status = PrinterStatus.Disconnected;
            RaiseConnectionChanged();
            await Task.Delay(100);
            RaiseTelemetryUpdated();
        }

        public async Task WriteDataAsync(string command)
        {
            LastTelemetry.LastResponse = $"Received command: {command}";
            RaiseTelemetryUpdated();
            await Task.Delay(50);
        }

        public async Task<PrinterTelemetry> GetPrinterTelemetryAsync()
        {
            await Task.Delay(50);
            return LastTelemetry;
        }

        public async Task<List<FileEntry>> GetFilesAsync()
        {
            await Task.Delay(100);
            return
            [
                new() { FullPath = "/gcodes/DemoCube.gcode", Size = 123456, ModifiedDate = DateTime.Now.AddDays(-1), IsAvailable = true },
                new() { FullPath = "/gcodes/Benchy.gcode", Size = 654321, ModifiedDate = DateTime.Now.AddDays(-2), IsAvailable = true }
            ];
        }

        public async Task SetHotendTemp(int targetTemp = 0)
        {
            _hotendTarget = Math.Clamp(targetTemp, 0, 300);
            LastTelemetry.HotendTarget = _hotendTarget;
