using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MakerPrompt.Shared.Models
{
    internal class CalibrationParameters
    {
        public int Temperature { get; set; } = 200;
        public int Cycles { get; set; } = 5;
    }
}
