using System;
using System.Collections.Generic;
using System.Text;

namespace DraughtsGameFunctionModels.Service
{
    public class MinimaxOutcome
    {
        public Int64 Evaluation { get; set; }

        public PotentialNextMove PotentialNextMove { get; set; }
    }
}
