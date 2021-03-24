using DraughtsGameFunctionModels.Service;
using System;
using System.Collections.Generic;
using System.Text;

namespace DraughtsGameFunctionService.Helpers
{
    public class PieceComparer : IEqualityComparer<Piece>
    {
        public bool Equals(Piece piece1, Piece piece2)
        {
            return piece1.Height == piece2.Height && piece1.Width == piece2.Width;
        }

        public int GetHashCode(Piece piece)
        {
            return piece.Height.GetHashCode() + piece.Width.GetHashCode();
        }
    }
}
