namespace MakerPrompt.Infrastructure.Services.Printers
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

        public async Task<bool> ConnectAsync(PrinterConnectionSettings connectionSettings, CancellationToken cancellationToken = default)
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

        public async Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            updateTimer.Stop();
            IsConnected = false;
            LastTelemetry.Status = PrinterStatus.Disconnected;
            RaiseConnectionChanged();
            await Task.Delay(100);
            RaiseTelemetryUpdated();
        }

        public async Task WriteDataAsync(string command, CancellationToken cancellationToken = default)
        {
            LastTelemetry.LastResponse = $"Received command: {command}";
            RaiseTelemetryUpdated();
            await Task.Delay(50);
        }

        public async Task<PrinterTelemetry> GetTelemetryAsync(CancellationToken cancellationToken = default)
        {
            await Task.Delay(50);
            return LastTelemetry;
        }

        public async Task<IReadOnlyList<string>> GetFilesAsync(CancellationToken cancellationToken = default)
        {
            await Task.Delay(100);
            return
            [
                "/gcodes/DemoCube.gcode",
                "/gcodes/Benchy.gcode"
            ];
        }

        public async Task SetHotendTempAsync(int targetTemp = 0, CancellationToken cancellationToken = default)
        {
            _hotendTarget = Math.Clamp(targetTemp, 0, 300);
            LastTelemetry.HotendTarget = _hotendTarget;
            LastTelemetry.LastResponse = $"Set hotend target to {_hotendTarget}�C";
            RaiseTelemetryUpdated();
            await Task.Delay(50);
        }

        public async Task SetBedTempAsync(int targetTemp = 0, CancellationToken cancellationToken = default)
        {
            _bedTarget = Math.Clamp(targetTemp, 0, 120);
            LastTelemetry.BedTarget = _bedTarget;
            LastTelemetry.LastResponse = $"Set bed target to {_bedTarget}�C";
            RaiseTelemetryUpdated();
            await Task.Delay(50);
        }

        public async Task HomeAsync(bool x = true, bool y = true, bool z = true, CancellationToken cancellationToken = default)
        {
            if (x) _position.X = 0;
            if (y) _position.Y = 0;
            if (z) _position.Z = 0;
            LastTelemetry.Position = _position;
            LastTelemetry.LastResponse = "Homed axes";
            RaiseTelemetryUpdated();
            await Task.Delay(100);
        }

        public async Task RelativeMoveAsync(int feedRate, float x = 0.0f, float y = 0.0f, float z = 0.0f, float e = 0.0f, CancellationToken cancellationToken = default)
        {
            _position += new Vector3(x, y, z);
            LastTelemetry.Position = _position;
            LastTelemetry.LastResponse = $"Moved to X:{_position.X:0.0} Y:{_position.Y:0.0} Z:{_position.Z:0.0}";
            RaiseTelemetryUpdated();
            await Task.Delay(100);
        }

        public async Task SetFanSpeedAsync(int fanSpeedPercentage = 0, CancellationToken cancellationToken = default)
        {
            _fanSpeed = Math.Clamp(fanSpeedPercentage, 0, 100);
            LastTelemetry.FanSpeed = _fanSpeed;
            LastTelemetry.LastResponse = $"Set fan speed to {_fanSpeed}%";
            RaiseTelemetryUpdated();
            await Task.Delay(50);
        }

        public async Task SetPrintSpeedAsync(int speed, CancellationToken cancellationToken = default)
        {
            _feedRate = Math.Clamp(speed, 1, 200);
            LastTelemetry.FeedRate = _feedRate;
            LastTelemetry.LastResponse = $"Set print speed to {_feedRate}%";
            RaiseTelemetryUpdated();
            await Task.Delay(50);
        }

        public async Task SetPrintFlowAsync(int flow, CancellationToken cancellationToken = default)
        {
            _flowRate = Math.Clamp(flow, 1, 200);
            LastTelemetry.FlowRate = _flowRate;
            LastTelemetry.LastResponse = $"Set print flow to {_flowRate}%";
            RaiseTelemetryUpdated();
            await Task.Delay(50);
        }

        public async Task SetAxisPerUnit(float x = 0.0f, float y = 0.0f, float z = 0.0f, float e = 0.0f)
        {
            LastTelemetry.LastResponse = $"Set axis per unit: X={x}, Y={y}, Z={z}, E={e}";
            RaiseTelemetryUpdated();
            await Task.Delay(50);
        }

        public async Task RunPidTuning(int cycles, int targetTemp, int extruderIndex)
        {
            LastTelemetry.LastResponse = $"PID tuning started: cycles={cycles}, target={targetTemp}, extruder={extruderIndex}";
            RaiseTelemetryUpdated();
            await Task.Delay(500);
            LastTelemetry.LastResponse = $"PID tuning complete: Kp=22.2 Ki=1.08 Kd=114";
            RaiseTelemetryUpdated();
        }

        public async Task RunThermalModelCalibration(int cycles, int targetTemp)
        {
            LastTelemetry.LastResponse = $"Thermal model calibration started: cycles={cycles}, target={targetTemp}";
            RaiseTelemetryUpdated();
            await Task.Delay(500);
            LastTelemetry.LastResponse = $"Thermal model calibration complete: Model=OK";
            RaiseTelemetryUpdated();
        }

        public async Task SaveEEPROM()
        {
            LastTelemetry.LastResponse = "EEPROM saved";
            RaiseTelemetryUpdated();
            await Task.Delay(100);
        }

        public async Task StartPrintAsync(string fileName, CancellationToken cancellationToken = default)
        {
            // Simulate starting a print job in demo mode
            if (string.IsNullOrEmpty(fileName))
            {
                LastTelemetry.LastResponse = "No file selected to print.";
                RaiseTelemetryUpdated();
                return;
            }

            LastTelemetry.LastResponse = $"Started print job: {fileName}";
            LastTelemetry.Status = PrinterStatus.Printing;
            RaiseTelemetryUpdated();

            // Simulate print duration
            await Task.Delay(1000);

            LastTelemetry.LastResponse = $"Print job completed: {fileName}";
            LastTelemetry.Status = PrinterStatus.Connected;
            RaiseTelemetryUpdated();
        }

        public Task StartPrintAsync(GCodeDoc gcodeDoc, CancellationToken cancellationToken = default)
        {
            // For the demo printer, just log that we would print the provided G-code.
            LastTelemetry.LastResponse = string.IsNullOrWhiteSpace(gcodeDoc.Content)
                ? "No G-code loaded to print."
                : "Simulated print from in-memory G-code document started.";
            RaiseTelemetryUpdated();
            return Task.CompletedTask;
        }

        public Task SaveFileAsync(string fullPath, Stream content)
        {
            using var ms = new MemoryStream();
            content.CopyTo(ms);
            var bytes = ms.ToArray();
            fileContents[fullPath] = bytes;
            var entry = files.FirstOrDefault(f => f.FullPath == fullPath);
            if (entry == null)
            {
                files.Add(new FileEntry
                {
                    FullPath = fullPath,
                    Size = bytes.Length,
                    ModifiedDate = DateTime.Now,
                    IsAvailable = true
                });
            }
            else
            {
                entry.Size = bytes.Length;
                entry.ModifiedDate = DateTime.Now;
            }
            return Task.CompletedTask;
        }

        public Task DeleteFileAsync(string fullPath)
        {
            files.RemoveAll(f => f.FullPath == fullPath);
            fileContents.Remove(fullPath);
            return Task.CompletedTask;
        }

        public Task<Stream?> OpenReadAsync(string fullPath)
        {
            if (fileContents.TryGetValue(fullPath, out var bytes))
            {
                return Task.FromResult<Stream?>(new MemoryStream(bytes));
            }
            return Task.FromResult<Stream?>(null);
        }

        public override ValueTask DisposeAsync()
        {
            updateTimer.Stop();
            files.Clear();
            fileContents.Clear();
            return ValueTask.CompletedTask;
        }

        private void SimulateTelemetry()
        {
            // Simulate hotend heating/cooling
            if (Math.Abs(_hotendTemp - _hotendTarget) > 0.1)
            {
                if (_hotendTemp < _hotendTarget)
                    _hotendTemp += Math.Min(2.0, _hotendTarget - _hotendTemp);
                else
                    _hotendTemp -= Math.Min(1.0, _hotendTemp - _hotendTarget);
            }

            // Simulate bed heating/cooling
            if (Math.Abs(_bedTemp - _bedTarget) > 0.1)
            {
                if (_bedTemp < _bedTarget)
                    _bedTemp += Math.Min(1.0, _bedTarget - _bedTemp);
                else
                    _bedTemp -= Math.Min(0.5, _bedTemp - _bedTarget);
            }

            LastTelemetry.HotendTemp = Math.Round(_hotendTemp, 1);
            LastTelemetry.BedTemp = Math.Round(_bedTemp, 1);
            LastTelemetry.Status = IsConnected ? PrinterStatus.Connected : PrinterStatus.Disconnected;
            RaiseTelemetryUpdated();
        }
    }
}