using System;
using System.Collections.Generic;
using System.Text;

namespace DraughtsGameFunctionModels.Service
{
    public class MinimaxOutcome
    {
        public int Evaluation { get; set; }

        public PotentialNextMove PotentialNextMove { get; set; }
    }
}
