using MakerPrompt.Shared.BrailleRAP.Models;

namespace MakerPrompt.Shared.BrailleRAP.Services
{
    /// <summary>
    /// Converts Braille cells to geometric points for embossing.
    /// Ported from AccessBrailleRAP's BrailleToGeometry.js
    /// </summary>
    public class BrailleToGeometry
    {
        // Standard 8-dot Braille dot positions
        private static readonly (int X, int Y)[] DotPositions = new[]
        {
            (0, 0), // Dot 1
            (0, 1), // Dot 2
            (0, 2), // Dot 3
            (1, 0), // Dot 4
            (1, 1), // Dot 5
            (1, 2), // Dot 6
            (0, 3), // Dot 7
            (1, 3)  // Dot 8
        };

        private readonly MachineConfig _config;

        public BrailleToGeometry(MachineConfig config)
        {
            _config = config;
        }

        /// <summary>
        /// Converts a single Braille character to geometric points.
        /// </summary>
        public List<GeomPoint> BrailleCharToGeom(char brailleChar, double offsetX, double offsetY)
        {
            var points = new List<GeomPoint>();
            int value = brailleChar - 0x2800;

            for (int i = 0; i < 8; i++)
            {
                if ((value & (1 << i)) != 0)
                {
                    var dot = DotPositions[i];
                    var point = new GeomPoint(
                        dot.X * _config.DotPaddingX + offsetX,
                        dot.Y * _config.DotPaddingY + offsetY
                    );
                    points.Add(point);
                }
            }

            return points;
        }

        /// <summary>
        /// Converts a page of Braille text to geometric points.
        /// </summary>
        public List<GeomPoint> BraillePageToGeom(List<string> lines, double offsetX, double offsetY)
        {
            var geometry = new List<GeomPoint>();
            var startY = offsetY;

            foreach (var line in lines)
            {
                var startX = offsetX;

                foreach (var ch in line)
                {
                    var points = BrailleCharToGeom(ch, startX, startY);
                    geometry.AddRange(points);
                    startX += _config.CellPaddingX;
                }

                startY += _config.CellPaddingY;
            }

            // Sort geometry
            SortGeom(geometry);

            // Apply zig-zag optimization for efficient printing
            var sorted = SortGeomZigZag(geometry);

            return sorted;
        }

        /// <summary>
        /// Sorts geometry by Y then X coordinates.
        /// </summary>
        private void SortGeom(List<GeomPoint> geom)
        {
            geom.Sort((a, b) =>
            {
                if (Math.Abs(a.Y - b.Y) < 0.001)
                    return a.X.CompareTo(b.X);
                return a.Y.CompareTo(b.Y);
            });
        }

        /// <summary>
        /// Sorts geometry in a zig-zag pattern for efficient printing.
        /// Alternates direction for each row to minimize travel distance.
        /// </summary>
        private List<GeomPoint> SortGeomZigZag(List<GeomPoint> geom)
        {
            if (geom == null || geom.Count == 0)
                return new List<GeomPoint>();

            var sorted = new List<GeomPoint>();
            int start = 0;
            int end = 0;
            int direction = 1;

            while (end < geom.Count)
            {
                // Find all points with the same Y coordinate
                while (end < geom.Count && Math.Abs(geom[start].Y - geom[end].Y) < 0.001)
                {
                    end++;
                }

                // Extract this row
                var row = geom.GetRange(start, end - start);

                // Sort by X, alternating direction
                row.Sort((a, b) => direction * a.X.CompareTo(b.X));

                sorted.AddRange(row);

                // Alternate direction for next row
                direction = -direction;
                start = end;
            }

            return sorted;
        }
    }
}
