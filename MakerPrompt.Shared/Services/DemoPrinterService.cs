namespace MakerPrompt.Shared.Services
{
    public class DemoPrinterService : BasePrinterConnectionService, IPrinterCommunicationService
    {
        private readonly Random _random = new();
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
            return new List<FileEntry>
            {
                new() { FullPath = "/gcodes/DemoCube.gcode", Size = 123456, ModifiedDate = DateTime.Now.AddDays(-1), IsAvailable = true },
                new() { FullPath = "/gcodes/Benchy.gcode", Size = 654321, ModifiedDate = DateTime.Now.AddDays(-2), IsAvailable = true }
            };
        }

        public async Task SetHotendTemp(int targetTemp = 0)
        {
            _hotendTarget = Math.Clamp(targetTemp, 0, 300);
            LastTelemetry.HotendTarget = _hotendTarget;
            LastTelemetry.LastResponse = $"Set hotend target to {_hotendTarget}°C";
            RaiseTelemetryUpdated();
            await Task.Delay(50);
        }

        public async Task SetBedTemp(int targetTemp = 0)
        {
            _bedTarget = Math.Clamp(targetTemp, 0, 120);
            LastTelemetry.BedTarget = _bedTarget;
            LastTelemetry.LastResponse = $"Set bed target to {_bedTarget}°C";
            RaiseTelemetryUpdated();
            await Task.Delay(50);
        }

        public async Task Home(bool x = true, bool y = true, bool z = true)
        {
            if (x) _position.X = 0;
            if (y) _position.Y = 0;
            if (z) _position.Z = 0;
            LastTelemetry.Position = _position;
            LastTelemetry.LastResponse = "Homed axes";
            RaiseTelemetryUpdated();
            await Task.Delay(100);
        }

        public async Task RelativeMove(int feedRate, float x = 0.0f, float y = 0.0f, float z = 0.0f, float e = 0.0f)
        {
            _position += new Vector3(x, y, z);
            LastTelemetry.Position = _position;
            LastTelemetry.LastResponse = $"Moved to X:{_position.X:0.0} Y:{_position.Y:0.0} Z:{_position.Z:0.0}";
            RaiseTelemetryUpdated();
            await Task.Delay(100);
        }

        public async Task SetFanSpeed(int fanSpeedPercentage = 0)
        {
            _fanSpeed = Math.Clamp(fanSpeedPercentage, 0, 100);
            LastTelemetry.FanSpeed = _fanSpeed;
            LastTelemetry.LastResponse = $"Set fan speed to {_fanSpeed}%";
            RaiseTelemetryUpdated();
            await Task.Delay(50);
        }

        public async Task SetPrintSpeed(int speed)
        {
            _feedRate = Math.Clamp(speed, 1, 200);
            LastTelemetry.FeedRate = _feedRate;
            LastTelemetry.LastResponse = $"Set print speed to {_feedRate}%";
            RaiseTelemetryUpdated();
            await Task.Delay(50);
        }

        public async Task SetPrintFlow(int flow)
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

        public async Task StartPrint(FileEntry file)
        {
            // Simulate starting a print job in demo mode
            if (file == null)
            {
                LastTelemetry.LastResponse = "No file selected to print.";
                RaiseTelemetryUpdated();
                return;
            }

            LastTelemetry.LastResponse = $"Started print job: {file.FullPath}";
            LastTelemetry.Status = PrinterStatus.Printing;
            RaiseTelemetryUpdated();

            // Simulate print duration
            await Task.Delay(1000);

            LastTelemetry.LastResponse = $"Print job completed: {file.FullPath}";
            LastTelemetry.Status = PrinterStatus.Connected;
            RaiseTelemetryUpdated();
        }

        public override ValueTask DisposeAsync()
        {
            updateTimer.Stop();
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