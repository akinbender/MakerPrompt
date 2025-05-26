using System.Text;

namespace MakerPrompt.Shared.Infrastructure
{
    public abstract class BaseSerialService : BasePrinterConnectionService
    {
        private readonly Regex _tempRegex = new(@"T:([\d.]+)\s/\s*([\d.]+)\sB:([\d.]+)\s/\s*([\d.]+)");
        private readonly Regex _posRegex = new(@"X:([\d.]+)\sY:([\d.]+)\sZ:([\d.]+)");
        public override Enums.PrinterConnectionType ConnectionType => Enums.PrinterConnectionType.Serial;
        
        StringBuilder _receiveBuffer = new();

        public abstract Task WriteDataAsync(string command);

        public async Task<PrinterTelemetry> GetPrinterTelemetryAsync()
        {
            await WriteDataAsync(GCodeCommands.GetTemperature.ToString());
            await WriteDataAsync(GCodeCommands.GetCurrentPosition.ToString());
            //await WriteDataAsync("M123");
            //await WriteDataAsync(GCodeCommands.SetFeedratePercentage.ToString());
            //await WriteDataAsync(GCodeCommands.SetFlowratePercentage.ToString());

            await Task.Delay(200);
            return LastTelemetry;
        }

        public async Task<List<FileEntry>> GetFilesAsync()
        {
            // await WriteDataAsync("M20 L T");
            // await Task.Delay(500); // Wait for response
            
            return new List<FileEntry>();
        }

        public async Task SetHotendTemp(int targetTemp = 0)
        {
            if (!IsConnected || (targetTemp < 0 || targetTemp > 300)) return;
            var command = GCodeCommands.SetTemp
                .SetParameterValue(GCodeParameters.TargetTemp.Label, targetTemp.ToString())
                .ToString();
            await WriteDataAsync(command);
        }

        public async Task SetBedTemp(int targetTemp = 0)
        {
            if (!IsConnected || (targetTemp < 0 || targetTemp > 120)) return;
            var command = GCodeCommands.SetBedTemp
                .SetParameterValue(GCodeParameters.TargetTemp.Label, targetTemp.ToString())
                .ToString();
            await WriteDataAsync(command);
        }

        public async Task Home(bool x = true, bool y = true, bool z = true)
        {
            if (!IsConnected) return;
            var command = GCodeCommands.Home;
            if (!(x && y && z))
            {
                if (x) command.SetParameterValue(GCodeParameters.HomeX.Label);
                if (y) command.SetParameterValue(GCodeParameters.HomeY.Label);
                if (z) command.SetParameterValue(GCodeParameters.HomeZ.Label);
            }

            await WriteDataAsync(command.ToString());
        }

        public async Task RelativeMove(int feedRate, float x = 0.0f, float y = 0.0f, float z = 0.0f, float e = 0.0f)
        {
            if (!IsConnected) return;
            var command = GCodeCommands.MoveLinear;

            if (!IsEqual(x, 0.0f)) command.SetParameterValue(GCodeParameters.PositionX.Label, x.ToString("0.0"));
            if (!IsEqual(y, 0.0f)) command.SetParameterValue(GCodeParameters.PositionY.Label, y.ToString("0.0"));
            if (!IsEqual(z, 0.0f)) command.SetParameterValue(GCodeParameters.PositionZ.Label, z.ToString("0.0"));
            if (!IsEqual(e, 0.0f)) command.SetParameterValue(GCodeParameters.PositionE.Label, e.ToString("0.0"));
            command.SetParameterValue(GCodeParameters.Feedrate.Label, feedRate.ToString());

            await WriteDataAsync(GCodeCommands.RelativePositioning.ToString());
            await WriteDataAsync(command.ToString());
            await WriteDataAsync(GCodeCommands.AbsolutePositioning.ToString());
        }

        public async Task SetFanSpeed(int fanSpeedPercentage = 0)
        {
            if (!IsConnected || (fanSpeedPercentage < 0 || fanSpeedPercentage > 100)) return;
            var command = fanSpeedPercentage == 0 ? GCodeCommands.FanOff
                :GCodeCommands.SetFanSpeed.SetParameterValue(GCodeParameters.FanSpeed.Label, ((int)(fanSpeedPercentage * 2.55)).ToString());
            await WriteDataAsync(command.ToString());
        }

        public async Task SetPrintSpeed(int speed)
        {
            if (!IsConnected || (speed < 1 || speed > 200)) return;
            var command = GCodeCommands.SetFeedratePercentage.SetParameterValue(GCodeParameters.RatePercentage.Label, speed.ToString());
            await WriteDataAsync(command.ToString());
        }

        public async Task SetPrintFlow(int flow)
        {
            if (!IsConnected || (flow < 1 || flow > 200)) return;
            var command = GCodeCommands.SetFlowratePercentage.SetParameterValue(GCodeParameters.RatePercentage.Label, flow.ToString());
            await WriteDataAsync(command.ToString());
        }

        public async Task SetAxisPerUnit(float x = 0.0f, float y = 0.0f, float z = 0.0f, float e = 0.0f)
        {
            if (!IsConnected) return;
            var command = GCodeCommands.MoveLinear;

            if (!IsEqual(x, 0.0f)) command.SetParameterValue(GCodeParameters.PositionX.Label, x.ToString("0.0"));
            if (!IsEqual(y, 0.0f)) command.SetParameterValue(GCodeParameters.PositionY.Label, y.ToString("0.0"));
            if (!IsEqual(z, 0.0f)) command.SetParameterValue(GCodeParameters.PositionZ.Label, z.ToString("0.0"));
            if (!IsEqual(e, 0.0f)) command.SetParameterValue(GCodeParameters.PositionE.Label, e.ToString("0.0"));
            await WriteDataAsync(command.ToString());
        }

        public async Task SaveEEPROM()
        {
            if (!IsConnected) return;
            await WriteDataAsync(GCodeCommands.StoreEEPROM.ToString());
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
                        LastTelemetry.HotendTemp = double.Parse(match.Groups[1].Value);
                        LastTelemetry.HotendTarget = double.Parse(match.Groups[2].Value);
                        LastTelemetry.BedTemp = double.Parse(match.Groups[3].Value);
                        LastTelemetry.BedTarget = double.Parse(match.Groups[4].Value);
                    }
                }
                else if (data.StartsWith("X:"))
                {
                    var match = _posRegex.Match(data);
                    if (match.Success)
                    {
                        LastTelemetry.Position = new Vector3(
                            double.Parse(match.Groups[1].Value),
                            double.Parse(match.Groups[2].Value),
                            double.Parse(match.Groups[3].Value)
                        );
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
                    var parts = line.Split(new[] {' '}, 4, StringSplitOptions.RemoveEmptyEntries);
                    
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
