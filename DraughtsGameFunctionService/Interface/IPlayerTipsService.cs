using DraughtsGameFunctionModels.Controller;
using DraughtsGameFunctionModels.Service;
using System;
using System.Collections.Generic;
using System.Text;

namespace DraughtsGameFunctionService.Interface
{
    public interface IPlayerTipsService
    {
        List<Piece> GetPotentialMoves(GetPlayerTips getPlayerTips);
    }
}
