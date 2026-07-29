using MakerPrompt.Infrastructure.Services.Printers;

namespace MakerPrompt.UI.Components.Infrastructure
{
    public abstract class BaseSerialService : BasePrinterConnectionService
    {
        private readonly Regex _tempRegex = new(@"T:([\d.]+)\s/\s*([\d.]+)\sB:([\d.]+)\s/\s*([\d.]+)");
        private readonly Regex _posRegex = new(@"X:([\d.]+)\sY:([\d.]+)\sZ:([\d.]+)");
        public override PrinterConnectionType ConnectionType => PrinterConnectionType.Serial;
        
        StringBuilder _receiveBuffer = new();

        // Core write entry point used by higher-level services. Implementations should
        // enqueue commands with appropriate metadata where available.
        public abstract Task WriteDataAsync(string command, CancellationToken cancellationToken = default);

        // Convenience helpers for tagging commands with their intent. Implementations
        // that support a queued sender can use this classification to prioritise work.
        public virtual Task WriteUserCommandAsync(
            string command, CancellationToken cancellationToken = default) =>
            WriteDataAsync(command, cancellationToken);

        public virtual Task WriteTelemetryCommandAsync(
            string command, CancellationToken cancellationToken = default) =>
            WriteDataAsync(command, cancellationToken);

        public virtual Task WritePrintCommandAsync(
            string command, CancellationToken cancellationToken = default) =>
            WriteDataAsync(command, cancellationToken);

        public async Task<PrinterTelemetry> GetTelemetryAsync(CancellationToken cancellationToken = default)
        {
            // Treat telemetry polling distinctly so queueing code can prioritise
            // active print commands when necessary.
            await WriteTelemetryCommandAsync(GCodeCommands.GetTemperature.ToString(), cancellationToken);
            await WriteTelemetryCommandAsync(GCodeCommands.GetCurrentPosition.ToString(), cancellationToken);
            //await WriteDataAsync("M123");
            //await WriteDataAsync(GCodeCommands.SetFeedratePercentage.ToString());
            //await WriteDataAsync(GCodeCommands.SetFlowratePercentage.ToString());

            await Task.Delay(200, cancellationToken);
            return LastTelemetry;
        }

        public Task<IReadOnlyList<FileEntry>> GetFilesAsync(CancellationToken cancellationToken = default)
        {
            // await WriteDataAsync("M20 L T");
            // await Task.Delay(500); // Wait for response
            
            return Task.FromResult<IReadOnlyList<FileEntry>>([]);
        }

        public async Task SetHotendTempAsync(int targetTemp = 0, CancellationToken cancellationToken = default)
        {
            if (!IsConnected || (targetTemp < 0 || targetTemp > 300)) return;
            var command = GCodeCommands.SetTemp
                .SetParameterValue(GCodeParameters.TargetTemp.Label, targetTemp.ToString())
                .ToString();
            await WriteUserCommandAsync(command, cancellationToken);
        }

        public async Task SetBedTempAsync(int targetTemp = 0, CancellationToken cancellationToken = default)
        {
            if (!IsConnected || (targetTemp < 0 || targetTemp > 120)) return;
            var command = GCodeCommands.SetBedTemp
                .SetParameterValue(GCodeParameters.TargetTemp.Label, targetTemp.ToString())
                .ToString();
            await WriteUserCommandAsync(command, cancellationToken);
        }

        public async Task HomeAsync(bool x = true, bool y = true, bool z = true, CancellationToken cancellationToken = default)
        {
            if (!IsConnected) return;
            var command = GCodeCommands.Home;
            if (!(x && y && z))
            {
                if (x) command.SetParameterValue(GCodeParameters.HomeX.Label);
                if (y) command.SetParameterValue(GCodeParameters.HomeY.Label);
                if (z) command.SetParameterValue(GCodeParameters.HomeZ.Label);
            }

            await WriteUserCommandAsync(command.ToString(), cancellationToken);
        }

        public async Task RelativeMoveAsync(int feedRate, float x = 0.0f, float y = 0.0f, float z = 0.0f, float e = 0.0f, CancellationToken cancellationToken = default)
        {
            if (!IsConnected) return;
            var command = GCodeCommands.MoveLinear;

            if (!IsEqual(x, 0.0f)) command.SetParameterValue(GCodeParameters.PositionX.Label, x.ToString("0.0"));
            if (!IsEqual(y, 0.0f)) command.SetParameterValue(GCodeParameters.PositionY.Label, y.ToString("0.0"));
            if (!IsEqual(z, 0.0f)) command.SetParameterValue(GCodeParameters.PositionZ.Label, z.ToString("0.0"));
            if (!IsEqual(e, 0.0f)) command.SetParameterValue(GCodeParameters.PositionE.Label, e.ToString("0.0"));
            command.SetParameterValue(GCodeParameters.Feedrate.Label, feedRate.ToString());

            await WriteUserCommandAsync(GCodeCommands.RelativePositioning.ToString(), cancellationToken);
            await WriteUserCommandAsync(command.ToString(), cancellationToken);
            await WriteUserCommandAsync(GCodeCommands.AbsolutePositioning.ToString(), cancellationToken);
        }

        public async Task SetFanSpeedAsync(int fanSpeedPercentage = 0, CancellationToken cancellationToken = default)
        {
            if (!IsConnected || (fanSpeedPercentage < 0 || fanSpeedPercentage > 100)) return;
            var command = fanSpeedPercentage == 0 ? GCodeCommands.FanOff
                :GCodeCommands.SetFanSpeed.SetParameterValue(GCodeParameters.FanSpeed.Label, ((int)(fanSpeedPercentage * 2.55)).ToString());
            await WriteUserCommandAsync(command.ToString(), cancellationToken);
        }

        public async Task SetPrintSpeedAsync(int speed, CancellationToken cancellationToken = default)
        {
            if (!IsConnected || (speed < 1 || speed > 200)) return;
            var command = GCodeCommands.SetFeedratePercentage.SetParameterValue(GCodeParameters.RatePercentage.Label, speed.ToString());
            await WriteUserCommandAsync(command.ToString(), cancellationToken);
        }

        public async Task SetPrintFlowAsync(int flow, CancellationToken cancellationToken = default)
        {
            if (!IsConnected || (flow < 1 || flow > 200)) return;
            var command = GCodeCommands.SetFlowratePercentage.SetParameterValue(GCodeParameters.RatePercentage.Label, flow.ToString());
            await WriteUserCommandAsync(command.ToString(), cancellationToken);
        }

        public async Task SetAxisPerUnitAsync(
            float x = 0.0f, float y = 0.0f, float z = 0.0f, float e = 0.0f,
            CancellationToken cancellationToken = default)
        {
            if (!IsConnected) return;
            var command = GCodeCommands.SetAxisSteps;

            if (!IsEqual(x, 0.0f)) command.SetParameterValue(GCodeParameters.PositionX.Label, x.ToString("0.0"));
            if (!IsEqual(y, 0.0f)) command.SetParameterValue(GCodeParameters.PositionY.Label, y.ToString("0.0"));
            if (!IsEqual(z, 0.0f)) command.SetParameterValue(GCodeParameters.PositionZ.Label, z.ToString("0.0"));
            if (!IsEqual(e, 0.0f)) command.SetParameterValue(GCodeParameters.PositionE.Label, e.ToString("0.0"));
            await WriteUserCommandAsync(command.ToString(), cancellationToken);
        }

        public async Task RunPidTuningAsync(
            int cycles, int targetTemp, int extruderIndex,
            CancellationToken cancellationToken = default)
        {
            if (!IsConnected) return;
            var command = GCodeCommands.PidAutotune
                .SetParameterValue(GCodeParameters.CalibrationCycle.Label, cycles.ToString())
                .SetParameterValue(GCodeParameters.TargetTemp.Label, targetTemp.ToString())
                .SetParameterValue(GCodeParameters.PositionE.Label, extruderIndex.ToString())
            .ToString();
            await WriteUserCommandAsync(command, cancellationToken);
        }

        public async Task RunThermalModelCalibrationAsync(
            int cycles, int targetTemp, CancellationToken cancellationToken = default)
        {
            if (!IsConnected) return;
            var command = GCodeCommands.ThermalModelCalibration
                .SetParameterValue(GCodeParameters.CalibrationCycle.Label, cycles.ToString())
                .SetParameterValue(GCodeParameters.TargetTemp.Label, targetTemp.ToString())
            .ToString();
            await WriteUserCommandAsync(command, cancellationToken);
        }

        public Task StartPrintAsync(string fileName, CancellationToken cancellationToken = default)
        {
            if (!IsConnected) return Task.CompletedTask;
            IsPrinting = true;
            return WriteDataAsync($"M23 {fileName}", cancellationToken);
        }

        public async Task StartPrintAsync(GCodeDoc gcode, CancellationToken cancellationToken = default)
        {
            if (!IsConnected || string.IsNullOrWhiteSpace(gcode.Content)) return;
            IsPrinting = true;
            await foreach (var command in gcode.EnumerateCommandsAsync(cancellationToken))
                await WriteDataAsync(command, cancellationToken);
        }

        public async Task SaveEepromAsync(CancellationToken cancellationToken = default)
        {
            if (!IsConnected) return;
            await WriteUserCommandAsync(GCodeCommands.StoreEEPROM.ToString(), cancellationToken);
        }

        private static bool IsEqual(float a, float b, float tolerance = 0.001f)
        {
            return Math.Abs(a - b) < tolerance;
        }

        public void ProcessReceivedData(string data)
        {
            _receiveBuffer.Append(data);

            while (true)
            {
                var bufferStr = _receiveBuffer.ToString();
                var newlineIndex = bufferStr.IndexOf('\n');

                if (newlineIndex < 0) break;

                var line = bufferStr.Substring(0, newlineIndex + 1)
                    .Trim('\r', '\n', ' ');

                if (!string.IsNullOrEmpty(line))
                {
                    ParseResponse(line);
                }

                _receiveBuffer = _receiveBuffer.Remove(0, newlineIndex + 1);
            }
        }

        public PrinterTelemetry ParseResponse(string data)
        {
            try
            {
                LastTelemetry.LastResponse = data;
                if (data.StartsWith("ok T:"))
                {
                    var match = _tempRegex.Match(data);
                    if (match.Success)
                    {
                        LastTelemetry.HotendTemp = float.Parse(match.Groups[1].Value);
                        LastTelemetry.HotendTarget = float.Parse(match.Groups[2].Value);
                        LastTelemetry.BedTemp = float.Parse(match.Groups[3].Value);
                        LastTelemetry.BedTarget = float.Parse(match.Groups[4].Value);
                    }
                }
                else if (data.StartsWith("X:"))
                {
                    var match = _posRegex.Match(data);
                    if (match.Success)
                    {
                        LastTelemetry.Position = new Vector3(
                            float.Parse(match.Groups[1].Value),
                            float.Parse(match.Groups[2].Value),
                            float.Parse(match.Groups[3].Value));
                    }
                }
                else if (data.Contains("SD printing byte"))
                {
                    LastTelemetry.SDCard.Printing = true;
                }

                RaiseTelemetryUpdated();
                return LastTelemetry;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing data: {ex.Message}");
            }

            RaiseTelemetryUpdated();
            return LastTelemetry;
        }

        private List<FileEntry> ParseM20Response(string response)
        {
            var files = new List<FileEntry>();
            
            if (string.IsNullOrEmpty(response))
                return files;

            // Split into lines and skip non-file lines
            var lines = response.Split('\n')
                .Where(line => line.Contains(".G", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var line in lines)
            {
                try
                {
                    // Split into parts - format: "filename size timestamp longname"
                    var parts = line.Split([' '], 4, StringSplitOptions.RemoveEmptyEntries);
                    
                    if (parts.Length < 2) // Need at least filename and size
                        continue;

                    var file = new FileEntry
                    {
                        FullPath = parts[0]
                    };

                    // Parse size
                    if (long.TryParse(parts[1], out var size))
                    {
                        file.Size = size;
                    }

                    // Parse timestamp if available (hex format)
                    if (parts.Length > 2 && parts[2].StartsWith("0x") && 
                        long.TryParse(parts[2].Substring(2), System.Globalization.NumberStyles.HexNumber, null, out var timestamp))
                    {
                        // Convert Unix hex timestamp to DateTime
                        file.ModifiedDate = DateTimeOffset.FromUnixTimeSeconds(timestamp).DateTime;
                    }

                    // Use long filename if available
                    if (parts.Length > 3)
                    {
                        file.FullPath = parts[3].Trim();
                    }

                    files.Add(file);
                }
                catch
                {
                    // Skip malformed entries
                    continue;
                }
            }

            return files;
        }
    }
}
