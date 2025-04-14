using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MakerPrompt.Shared.Models;
using MakerPrompt.Shared.Utils;

namespace MakerPrompt.Shared.Infrastructure
{
    public abstract class BaseSerialService : BasePrinterConnectionService
    {
        private readonly Regex _tempRegex = new(@"T:([\d.]+)\s/\s*([\d.]+)\sB:([\d.]+)\s/\s*([\d.]+)");
        private readonly Regex _posRegex = new(@"X:([\d.]+)\sY:([\d.]+)\sZ:([\d.]+)");
        public override Enums.PrinterConnectionType ConnectionType => Enums.PrinterConnectionType.Serial;

        public override async Task<PrinterTelemetry> GetPrinterTelemetryAsync()
        {
            // Request fresh data
            await WriteDataAsync("M105"); // Temperature
            await WriteDataAsync("M114"); // Position

            // Small delay to allow responses to come in
            await Task.Delay(200);
            return LastTelemetry;
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
