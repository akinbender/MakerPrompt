using System.Reflection;

namespace MakerPrompt.Shared.Utils
{
    internal class GCodeParameters
    {
        public static GCodeParameter TargetTemp = new('S', Resources.GCodeDescription_S_TargetTemp);

        public static GCodeParameter FanSpeed = new('S', "Speed (0-255)");

        public static GCodeParameter RatePercentage = new('S', "Percentage");

        public static GCodeParameter CalibrationCycle = new('C', Resources.GCodeDescription_C_Cycle);

        public static GCodeParameter HomeX = new('X', Resources.GCodeDescription_X_Position);

        public static GCodeParameter HomeY = new('Y', Resources.GCodeDescription_Y_Position);

        public static GCodeParameter HomeZ = new('Z', Resources.GCodeDescription_Z_Position);

        public static GCodeParameter PositionX = new('X', Resources.GCodeDescription_X_Position);

        public static GCodeParameter PositionY = new('Y', Resources.GCodeDescription_Y_Position);

        public static GCodeParameter PositionZ = new('Z', Resources.GCodeDescription_Z_Position);

        public static GCodeParameter PositionE = new('E', Resources.GCodeDescription_E_Position);

        public static GCodeParameter Feedrate = new('F', Resources.GCodeDescription_F_Feedrate);

        public static GCodeParameter FilePath = new('F', Resources.GCodeDescription_F_File);

        public static GCodeParameter Proportional = new('P', Resources.GCodeDescription_P_Proportional);

        public static GCodeParameter Integral = new('I', Resources.GCodeDescription_I_Integral);

        public static GCodeParameter Derivative = new('D', Resources.GCodeDescription_D_Derivative);

    }
    internal static class GCodeCommands
    {
        public static GCodeCommand SetParameterValue(this GCodeCommand command, char label)
        {
            command.Parameters.First(p => p.Label.Equals(label)).Value = " ";
            return command;
        }

        public static GCodeCommand SetParameterValue(this GCodeCommand command, char label, string value)
        {
            command.Parameters.First(p => p.Label.Equals(label)).Value = value;
            return command;
        }

        public static List<GCodeCommand> AllCommands() => typeof(GCodeCommands)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.FieldType == typeof(GCodeCommand))
                .Select(f => (GCodeCommand)f.GetValue(null))
                .ToList();

        // Movement Commands
        public static GCodeCommand MoveLinearRapid =
            new("G0", Resources.GCodeDescription_G0, [GCodeCategory.Movement],
                   [ GCodeParameters.PositionX,
                     GCodeParameters.PositionY,
                     GCodeParameters.PositionZ,
                     GCodeParameters.PositionE,
                     GCodeParameters.Feedrate]);

        public static GCodeCommand MoveLinear =
            new("G1", Resources.GCodeDescription_G1, [GCodeCategory.Movement],
                   [ GCodeParameters.PositionX,
                     GCodeParameters.PositionY,
                     GCodeParameters.PositionZ,
                     GCodeParameters.PositionE,
                     GCodeParameters.Feedrate]);

        public static GCodeCommand Home =
            new("G28", Resources.GCodeDescription_G28, [GCodeCategory.Movement],
                    [ GCodeParameters.HomeX,
                      GCodeParameters.HomeY,
                      GCodeParameters.HomeZ]);


        public static GCodeCommand AbsolutePositioning =
            new("G90", Resources.GCodeDescription_G90, [GCodeCategory.Movement]);

        public static GCodeCommand RelativePositioning =
            new("G91", Resources.GCodeDescription_G91, [GCodeCategory.Movement]);

        public static GCodeCommand EnableSteppers =
            new("M17", Resources.GCodeDescription_M17, [GCodeCategory.Movement]);

        public static GCodeCommand DisableSteppers =
            new("M18", Resources.GCodeDescription_M18, [GCodeCategory.Movement]);

        // SD Card
        public static GCodeCommand ListSDCard =
            new("M20", Resources.GCodeDescription_M20, [GCodeCategory.SDCard],
            [
                new('L', "Long format listing (optional)"),
                new('T', "Timestamp (optional)")
            ]);

        public static GCodeCommand InitSDCard = 
            new("M21", Resources.GCodeDescription_M21, [GCodeCategory.SDCard]);

        public static GCodeCommand ReleaseSDCard = 
            new("M22", Resources.GCodeDescription_M22, [GCodeCategory.SDCard]);

        public static GCodeCommand SelectSDFile = 
            new("M23", Resources.GCodeDescription_M23, [GCodeCategory.SDCard],
            [
            // Note: Actual parameter is filename without letter prefix
            new('F', "File path (e.g., 'model.gcode')")
            ]);

        public static GCodeCommand StartSDPrint = 
            new("M24", Resources.GCodeDescription_M24, [GCodeCategory.SDCard],
            [
                new('S', "Start position (bytes, optional)"),
                new('T', "Start time (seconds, optional)")
            ]);

        public static GCodeCommand PauseSDPrint = new("M25", Resources.GCodeDescription_M25, [GCodeCategory.SDCard]);

        public static GCodeCommand SetSDCardPosition = new("M26", Resources.GCodeDescription_M26, [GCodeCategory.SDCard],
            [
            new('S', "Position in bytes")
            ]);

        public static GCodeCommand ReportSDStatus = new("M27", Resources.GCodeDescription_M27, [GCodeCategory.SDCard],
            [
            new('C', "Continuous reporting mode"),
            new('S', "Interval in seconds")
            ]);

        public static GCodeCommand WriteToSDCard = 
            new("M28", Resources.GCodeDescription_M28, [GCodeCategory.SDCard], [ GCodeParameters.FilePath ]);

        public static GCodeCommand EndSDWrite = 
            new("M29", Resources.GCodeDescription_M29, [GCodeCategory.SDCard]);

        public static GCodeCommand DeleteSDFile = 
            new("M30", Resources.GCodeDescription_M30, [GCodeCategory.SDCard], [GCodeParameters.FilePath]);

        public static GCodeCommand SelectAndStartPrint = 
            new("M32", Resources.GCodeDescription_M32, [GCodeCategory.SDCard], [GCodeParameters.FilePath]);

        public static GCodeCommand SetAxisSteps =
            new("M92", Resources.GCodeDescription_M92, [GCodeCategory.Movement, GCodeCategory.Settings],
                   [ GCodeParameters.PositionX,
                     GCodeParameters.PositionY,
                     GCodeParameters.PositionZ,
                     GCodeParameters.PositionE ]);

        public static GCodeCommand SetTemp =
            new("M104", Resources.GCodeDescription_M104, [GCodeCategory.Temperature],
                [GCodeParameters.TargetTemp]);

        public static GCodeCommand GetTemperature =
            new("M105", Resources.GCodeDescription_M105, [GCodeCategory.Temperature, GCodeCategory.Reporting]);

        public static GCodeCommand SetFanSpeed =
            new("M106", Resources.GCodeDescription_M106, [GCodeCategory.Temperature, GCodeCategory.FanControl],
                [GCodeParameters.FanSpeed ]);

        public static GCodeCommand FanOff =
            new("M107", Resources.GCodeDescription_M107, [GCodeCategory.Temperature, GCodeCategory.FanControl]);

        public static GCodeCommand SetAndWaitTemp =
            new("M109", Resources.GCodeDescription_M109, [GCodeCategory.Temperature],
                [GCodeParameters.TargetTemp]);

        public static GCodeCommand GetCurrentPosition =
            new("M114", Resources.GCodeDescription_M114, [GCodeCategory.Movement, GCodeCategory.Reporting]);

        public static GCodeCommand SetLcdMessage =
            new("M117", Resources.GCodeDescription_M117, [GCodeCategory.Reporting],
                [ new GCodeParameter('A', "Message")]); //fix

        public static GCodeCommand SetBedTemp =
            new("M140", Resources.GCodeDescription_M140, [GCodeCategory.Temperature],
                [GCodeParameters.TargetTemp]);

        public static GCodeCommand SetAndWaitBedTemp =
            new("M190", Resources.GCodeDescription_M190, [GCodeCategory.Temperature],
                [GCodeParameters.TargetTemp]);

        public static GCodeCommand SetFeedratePercentage =
            new("M220", Resources.GCodeDescription_M220, [GCodeCategory.Movement, GCodeCategory.Settings],
                [GCodeParameters.RatePercentage]);

        public static GCodeCommand SetFlowratePercentage =
            new("M221", Resources.GCodeDescription_M221, [GCodeCategory.Movement, GCodeCategory.Settings],
            [GCodeParameters.RatePercentage]);

        // Calibration Commands
        public static readonly GCodeCommand SetHotendPid = 
            new("M301", Resources.GCodeDescription_M301, [GCodeCategory.Calibration, GCodeCategory.Settings],
                [ GCodeParameters.Proportional,
                  GCodeParameters.Integral,
                  GCodeParameters.Derivative]);

        public static readonly GCodeCommand PidAutotune = new(
            "M303", Resources.GCodeDescription_M303, [GCodeCategory.Calibration],
            [
            new('E', "Extruder index"),
            GCodeParameters.TargetTemp,
            GCodeParameters.CalibrationCycle
            ]);

        public static readonly GCodeCommand SetBedPid =
            new("M304", Resources.GCodeDescription_M304, [GCodeCategory.Calibration, GCodeCategory.Settings],
                [ GCodeParameters.Proportional,
                  GCodeParameters.Integral,
                  GCodeParameters.Derivative]);

        public static readonly GCodeCommand ThermalModelCalibration = new(
            "M306", Resources.GCodeDescription_M306, [GCodeCategory.Calibration],
            [
            GCodeParameters.TargetTemp,
            GCodeParameters.CalibrationCycle
            ]);

        public static GCodeCommand StoreEEPROM =
            new ("M500", Resources.GCodeDescription_M500, [GCodeCategory.Settings]);

        public static GCodeCommand RestoreEEPROM =
            new("M501", Resources.GCodeDescription_M501, [GCodeCategory.Settings]);

        public static GCodeCommand ResetEEPROM =
            new("M502", Resources.GCodeDescription_M502, [GCodeCategory.Settings]);

        public static GCodeCommand ReportEEPROM =
            new("M503", Resources.GCodeDescription_M503, [GCodeCategory.Settings]);

        public static GCodeCommand ValidateEEPROM =
            new("M504", Resources.GCodeDescription_M504, [GCodeCategory.Settings]);
    }

    internal static class GCodeCommandExtensions
    {
        internal static string GetCommandExample(this GCodeCommand command)
        {
            var example = command.Command;
            if (command.Parameters.Count != 0)
            {
                example += " " + string.Join(" ", command.Parameters
                    .Select(p => $"{p.Label}123"));
            }
            return example;
        }
    }
}
