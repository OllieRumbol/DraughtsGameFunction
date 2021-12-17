using System;
using System.Collections.Generic;
using System.Text;

namespace DraughtsGameFunctionModels.Controller
{
    public class GetNextMove
    {
        public Int64 Version { get; set; }

        public Int64[,] Board { get; set; }

        public Int64 Depth { get; set; }

        public Int64 Player { get; set; }
    }
}
