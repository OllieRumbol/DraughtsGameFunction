using System.Collections.Generic;

namespace DraughtsGameFunctionModels.Service
{
    public class PotentialNextMove
    {
        public int[,] Board { get; set; }

        public int CurrentHeight { get; set; }

        public int CurrentWidth { get; set; }

        public int NextHeight { get; set; }

        public int NextWidth { get; set; }

        public List<Piece> Takes { get; set; }
    }
}