using System;
using System.Collections.Generic;

namespace DraughtsGameFunctionModels.Service
{
    public class PotentialNextMove
    {
        public Int64[,] Board { get; set; }

        public Int64 CurrentHeight { get; set; }

        public Int64 CurrentWidth { get; set; }

        public Int64 NextHeight { get; set; }

        public Int64 NextWidth { get; set; }

        public List<Piece> Takes { get; set; }
    }
}