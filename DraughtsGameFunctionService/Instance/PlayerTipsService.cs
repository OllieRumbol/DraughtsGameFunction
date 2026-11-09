using DraughtsGameFunctionModels.Controller;
using DraughtsGameFunctionModels.Service;
using DraughtsGameFunctionService.Helpers;
using DraughtsGameFunctionService.Interface;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DraughtsGameFunctionService.Instance
{
    public class PlayerTipsService : IPlayerTipsService
    {
        public List<Piece> GetPotentialMoves(GetPlayerTips getPlayerTips)
        {
            // A king's own square is not a tip, even when a chain ends there. The web app highlights every tip by
            // writing over the square, which would remove the king (spec section 6.1.4, item 4).
            List<Piece> potentialMoves = FindMove.FindAvailableMoves(getPlayerTips.Board, getPlayerTips.TipFor)
                .Where(m => m.NextHeight != m.CurrentHeight || m.NextWidth != m.CurrentWidth)
                .Select(m =>
                {
                    return new Piece
                    {
                        Height = m.NextHeight,
                        Width = m.NextWidth
                    };
                })
                .ToList();

            return potentialMoves.Distinct(new PieceComparer()).ToList();
        }
    }
}
