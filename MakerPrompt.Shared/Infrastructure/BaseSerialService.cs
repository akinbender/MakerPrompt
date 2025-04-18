using System.Collections.Concurrent;
using System.Text;
using System.Threading.Tasks.Dataflow;

namespace MakerPrompt.Shared.Infrastructure
{
    public abstract class BaseSerialService : BasePrinterConnectionService
    {
        private readonly object _commandLock = new();
        public BufferBlock<PendingCommand> _commandQueue = new();
        private readonly Regex _tempRegex = new(@"T:([\d.]+)\s/\s*([\d.]+)\sB:([\d.]+)\s/\s*([\d.]+)");
        private readonly Regex _posRegex = new(@"X:([\d.]+)\sY:([\d.]+)\sZ:([\d.]+)");
        private readonly Regex _rateRegex = new(@"(\d+)%");
        public override PrinterConnectionType ConnectionType => PrinterConnectionType.Serial;

        public StringBuilder _receiveBuffer = new();

        public override async Task<PrinterTelemetry> GetPrinterTelemetryAsync()
        {
            await WriteDataAsync(GCodeCommands.GetTemperature);
            await WriteDataAsync(GCodeCommands.GetCurrentPosition);
            //await WriteDataAsync("M123");
            //await WriteDataAsync(GCodeCommands.SetFeedratePercentage.ToString());
            //await WriteDataAsync(GCodeCommands.SetFlowratePercentage.ToString());

            await Task.Delay(200);
            return LastTelemetry;
        }

        public async Task WriteDataAsync(GCodeCommand command, bool isAutomatic = false)
        {
            var pending = new PendingCommand
            {
                Command = command.ToString(),
                ResponseSource = new TaskCompletionSource<string>(),
                ExpectedResponsePattern = GetExpectedResponse(command),
                IsAutomatic = isAutomatic
            };

            await _commandQueue.SendAsync(pending);
        }

        protected void ProcessReceivedData(string data)
        {
            lock (_commandLock)
            {
                if (_commandQueue.TryReceive(out var pending))
                {
                    if (pending.ExpectedResponsePattern.IsMatch(data))
                    {
                        pending.ResponseSource.TrySetResult(data);
                        ParseResponse(data, pending.IsAutomatic);
                        if (!pending.IsAutomatic)
                            RaiseDataRecieved($"> {pending.Command}\n< {data}");
                        return;
                    }
                }

                if (!data.StartsWith("ok") && !data.StartsWith("echo"))
                    RaiseDataRecieved($"SYS: {data}");
                ParseResponse(data, false);
            }
        }

        private Regex GetExpectedResponse(GCodeCommand command)
        {
            return command.Command switch
            {
                "M105" => _tempRegex,
                "M114" => _posRegex,
                "M220" => _rateRegex,
                "M221" => _rateRegex,
                _ => new Regex(".*") // Match anything
            };
        }

        protected virtual void ParseResponse(string data, bool isAutomatic)
        {
            try
            {
                //LastTelemetry.LastResponse = data;

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
                //else if (data.Contains("FR:"))
                //{
                //    var match = _rateRegex.Match(data);
                //    if (match.Success)
                //    {
                //        LastTelemetry.FeedRate = int.Parse(match.Groups[1].Value);
                //    }
                //}
                //else if (data.Contains("Flow:"))
                //{
                //    var match = _rateRegex.Match(data);
                //    if (match.Success)
                //    {
                //        LastTelemetry.FlowRate = int.Parse(match.Groups[1].Value);
                //    }
                //}

                // Only raise event for non-automatic updates
                if (!isAutomatic)
                {
                    RaiseTelemetryUpdated();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing data: {ex.Message}");
            }
        }

        public class PendingCommand
        {
            public string Command { get; set; }
            public TaskCompletionSource<string> ResponseSource { get; set; }
            public Regex ExpectedResponsePattern { get; set; }
            public bool IsAutomatic { get; set; }
        }
    }
}
