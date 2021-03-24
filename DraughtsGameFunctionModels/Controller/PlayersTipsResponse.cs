using DraughtsGameFunctionModels.Service;
using System;
using System.Collections.Generic;
using System.Text;

namespace DraughtsGameFunctionModels.Controller
{
    public class PlayersTipsResponse : Response
    {
        public List<Piece> PotentialMoves { get; set; }
    }
}
