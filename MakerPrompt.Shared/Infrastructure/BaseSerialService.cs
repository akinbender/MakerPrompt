using System.Text;

namespace MakerPrompt.Shared.Infrastructure
{
    public abstract class BaseSerialService : BasePrinterConnectionService
    {
        private readonly Regex _tempRegex = new(@"T:([\d.]+)\s/\s*([\d.]+)\sB:([\d.]+)\s/\s*([\d.]+)");
        private readonly Regex _posRegex = new(@"X:([\d.]+)\sY:([\d.]+)\sZ:([\d.]+)");
        public override Enums.PrinterConnectionType ConnectionType => Enums.PrinterConnectionType.Serial;
        
        StringBuilder _receiveBuffer = new();
        public override async Task<PrinterTelemetry> GetPrinterTelemetryAsync()
        {
            await WriteDataAsync(GCodeCommands.GetTemperature.ToString());
            await WriteDataAsync(GCodeCommands.GetCurrentPosition.ToString());
            //await WriteDataAsync("M123");
            //await WriteDataAsync(GCodeCommands.SetFeedratePercentage.ToString());
            //await WriteDataAsync(GCodeCommands.SetFlowratePercentage.ToString());

            await Task.Delay(200);
            return LastTelemetry;
        }

        public override async Task<List<FileEntry>> GetFilesAsync()
        {
            // await WriteDataAsync("M20 L T");
            // await Task.Delay(500); // Wait for response
            
            return new List<FileEntry>();
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
                        Name = parts[0].Contains('/') ? parts[0].Substring(parts[0].LastIndexOf('/') + 1) : parts[0],
                        FullPath = parts[0],
                        IsDirectory = false,
                        Type = "G-code"
                    };

                    // Parse size
                    if (long.TryParse(parts[1], out var size))
                    {
                        file.Size = size;
                    }

                    // Parse timestamp if available (hex format)
                    if (parts.Length > 2 && parts[2].StartsWith("0x") && 
                        long.TryParse(parts[2].Substring(2), NumberStyles.HexNumber, null, out var timestamp))
                    {
                        // Convert Unix hex timestamp to DateTime
                        file.ModifiedDate = DateTimeOffset.FromUnixTimeSeconds(timestamp).DateTime;
                    }

                    // Use long filename if available
                    if (parts.Length > 3)
                    {
                        file.Name = parts[3].Trim();
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
