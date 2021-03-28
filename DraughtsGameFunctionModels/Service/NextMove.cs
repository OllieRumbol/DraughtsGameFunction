using System;
using System.Collections.Generic;
using System.Text;

namespace DraughtsGameFunctionModels.Service
{
    public class NextMove
    {
        public int CurrentHeight { get; set; }

        public int CurrentWidth { get; set; }

        public int NextHeight { get; set; }

        public int NextWidth { get; set; }

        public List<Piece> Takes { get; set; }
    }
}
