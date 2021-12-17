using System;
using System.Collections.Generic;
using System.Text;

namespace DraughtsGameFunctionModels.Service
{
    public class NextMove
    {
        public Int64 CurrentHeight { get; set; }

        public Int64 CurrentWidth { get; set; }

        public Int64 NextHeight { get; set; }

        public Int64 NextWidth { get; set; }

        public List<Piece> Takes { get; set; }
    }
}
