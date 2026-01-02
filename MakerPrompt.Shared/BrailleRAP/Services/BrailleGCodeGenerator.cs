using MakerPrompt.Shared.BrailleRAP.Models;
using System.Text;

namespace MakerPrompt.Shared.BrailleRAP.Services
{
    /// <summary>
    /// Generates G-code for BrailleRAP embossing from geometric points.
    /// Ported from AccessBrailleRAP's GeomToGCode.js
    /// </summary>
    public class BrailleGCodeGenerator
    {
        private readonly MachineConfig _config;

        public BrailleGCodeGenerator(MachineConfig config)
        {
            _config = config;
        }

        /// <summary>
        /// Generates complete G-code from geometric points.
        /// </summary>
        public string GenerateGCode(List<GeomPoint> points)
        {
            var gcode = new StringBuilder();

            // Initialize
            gcode.Append(Home());
            gcode.Append(SetSpeed(_config.FeedRate));
            gcode.Append(MoveTo(0, 0));

            // Process each point
            foreach (var point in points)
            {
                gcode.Append(MoveTo(point.X, point.Y));
                gcode.Append(PrintDot());
            }

            // Return to home position
            gcode.Append(MoveTo(0, _config.ReturnPositionY));
            gcode.Append(MotorOff());

            return gcode.ToString();
        }

        /// <summary>
        /// Generates G-code from a Braille page layout.
        /// </summary>
        public string GenerateGCodeFromLayout(BraillePageLayout layout, int pageIndex = 0)
        {
            var page = layout.GetPage(pageIndex);
            if (page.Count == 0)
                return string.Empty;

            var geometry = new BrailleToGeometry(_config);
            var points = geometry.BraillePageToGeom(page, _config.OffsetX, _config.OffsetY);

            return GenerateGCode(points);
        }

        private string MotorOff()
        {
            return "M84;\r\n";
        }

        private string Home()
        {
            var sb = new StringBuilder();
            sb.Append("G28 X;\r\n");
            sb.Append("G28 Y;\r\n");
            return sb.ToString();
        }

        private string GCodePosition(double? x, double? y)
        {
            if (x == null && y == null)
            {
                throw new ArgumentException("At least one coordinate must be specified");
            }

            var code = new StringBuilder();

            if (x.HasValue)
            {
                code.Append($" X{x.Value:F2}");
            }

            if (y.HasValue)
            {
                code.Append($" Y{y.Value:F2}");
            }

            code.Append(";\r\n");
            return code.ToString();
        }

        private string SetSpeed(int speed)
        {
            return $"G1 F{speed};\r\n";
        }

        private string MoveTo(double x, double y)
        {
            return "G1" + GCodePosition(x, y);
        }

        private string PrintDot()
        {
            var sb = new StringBuilder();
            sb.Append("M3 S1;\r\n");
            sb.Append("M3 S0;\r\n");
            return sb.ToString();
        }
    }
}
