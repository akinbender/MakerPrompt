using MakerPrompt.Shared.Infrastructure;
using MakerPrompt.Shared.Models;
using MakerPrompt.Shared.Utils;

namespace MakerPrompt.Shared.Services
{
    public class PrinterStateService : IDisposable
    {
        private readonly PrinterCommunicationServiceFactory factory;
        public PrinterTelemetry Telemetry { get; } = new();

        public PrinterStateService(PrinterCommunicationServiceFactory factory)
        {
            this.factory = factory;
            this.factory.ConnectionStateChanged += HandleConnectionStateChanged;
        }

        private void HandleConnectionStateChanged(object? sender, bool isConnected)
        {
            if (isConnected && factory.Current != null)
            {
                factory.Current.TelemetryUpdated += HandleTelemetryUpdated;
            }
            else {
                if (factory.Current != null)
                {
                    factory.Current.TelemetryUpdated -= HandleTelemetryUpdated;
                }
            }
        }

        private void HandleTelemetryUpdated(object? sender, PrinterTelemetry newTelemetry)
        {
            if (factory.Current == null) return;
            Telemetry.HotendTemp = newTelemetry.HotendTemp;
            Telemetry.HotendTarget = newTelemetry.HotendTarget;
            Telemetry.BedTemp = newTelemetry.BedTemp;
            Telemetry.BedTarget = newTelemetry.BedTarget;
            Telemetry.Position = newTelemetry.Position;
            Telemetry.Status = newTelemetry.Status;
            Telemetry.FeedRate = newTelemetry.FeedRate;
            Telemetry.FlowRate = newTelemetry.FlowRate;
            //Telemetry.SDCard = newTelemetry.SDCard;
        }

        public async Task SetHotendTemp(int targetTemp = 0)
        {
            if (factory.Current == null || (targetTemp < 0 || targetTemp > 300)) return;
            var command = GCodeCommands.SetTemp
                .SetParameterValue(GCodeParameters.TargetTemp.Label, targetTemp.ToString())
                .ToString();
            await factory.Current.WriteDataAsync(command);
        }

        public async Task SetBedTemp(int targetTemp = 0)
        {
            if (factory.Current == null || (targetTemp < 0 || targetTemp > 120)) return;
            var command = GCodeCommands.SetBedTemp
                .SetParameterValue(GCodeParameters.TargetTemp.Label, targetTemp.ToString())
                .ToString();
            await factory.Current.WriteDataAsync(command);
        }

        public async Task Home(bool x = true, bool y = true, bool z = true)
        {
            if (factory.Current == null) return;
            var command = GCodeCommands.Home;
            if (!(x && y && z))
            {
                if (x) command.SetParameterValue(GCodeParameters.HomeX.Label);
                if (y) command.SetParameterValue(GCodeParameters.HomeY.Label);
                if (z) command.SetParameterValue(GCodeParameters.HomeZ.Label);
            }

            await factory.Current.WriteDataAsync(command.ToString());
        }

        public async Task RelativeMove(int feedRate, float x = 0.0f, float y = 0.0f, float z = 0.0f, float e = 0.0f)
        {
            if (factory.Current == null) return;
            var command = GCodeCommands.MoveLinear;

            if (!IsEqual(x, 0.0f)) command.SetParameterValue(GCodeParameters.PositionX.Label, x.ToString("0.0"));
            if (!IsEqual(y, 0.0f)) command.SetParameterValue(GCodeParameters.PositionY.Label, y.ToString("0.0"));
            if (!IsEqual(z, 0.0f)) command.SetParameterValue(GCodeParameters.PositionZ.Label, z.ToString("0.0"));
            if (!IsEqual(e, 0.0f)) command.SetParameterValue(GCodeParameters.PositionE.Label, e.ToString("0.0"));
            command.SetParameterValue(GCodeParameters.Feedrate.Label, feedRate.ToString());

            await factory.Current.WriteDataAsync(GCodeCommands.RelativePositioning.ToString());
            await factory.Current.WriteDataAsync(command.ToString());
            await factory.Current.WriteDataAsync(GCodeCommands.AbsolutePositioning.ToString());
        }

        public async Task SetFanSpeed(int fanSpeedPercentage = 0)
        {
            if (factory.Current == null || (fanSpeedPercentage < 0 || fanSpeedPercentage > 100)) return;
            var command = fanSpeedPercentage == 0 ? GCodeCommands.FanOff
                :GCodeCommands.SetFanSpeed.SetParameterValue(GCodeParameters.FanSpeed.Label, ((int)(fanSpeedPercentage * 2.55)).ToString());
            await factory.Current.WriteDataAsync(command.ToString());
        }

        public async Task SetPrintSpeed(int speed)
        {
            if (factory.Current == null || (speed < 1 || speed > 200)) return;
            var command = GCodeCommands.SetFeedratePercentage.SetParameterValue(GCodeParameters.RatePercentage.Label, speed.ToString());
            await factory.Current.WriteDataAsync(command.ToString());
        }

        public async Task SetPrintFlow(int flow)
        {
            if (factory.Current == null || (flow < 1 || flow > 200)) return;
            var command = GCodeCommands.SetFlowratePercentage.SetParameterValue(GCodeParameters.RatePercentage.Label, flow.ToString());
            await factory.Current.WriteDataAsync(command.ToString());
        }

        private static bool IsEqual(float a, float b, float tolerance = 0.001f)
        {
            return Math.Abs(a - b) < tolerance;
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
