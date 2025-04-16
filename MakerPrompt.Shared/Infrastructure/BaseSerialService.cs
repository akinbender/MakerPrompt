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
    }
}
