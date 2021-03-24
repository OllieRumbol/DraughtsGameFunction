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
            List<Piece> potentialMoves =  FindMove.FindAvailableMoves(getPlayerTips.Board, getPlayerTips.TipFor).Select(m => 
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
