using DraughtsGameFunctionModels.Controller;
using DraughtsGameFunctionModels.Service;
using DraughtsGameFunctionService.Helpers;
using DraughtsGameFunctionService.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DraughtsGameFunctionService.Intstance
{
    public class AutomatedPlayerServiceV1 : IAutomatedPlayerService
    {
        public NextMove GetNextMoveForAutomatedPlayer(GetNextMove getNextMove)
        {
            List<NextMove> results = FindMove.FindAvailableMoves(getNextMove.Board, getNextMove.Player);

            List<NextMove> takeMoves = results.Where(m => m.Takes.Count > 0).ToList();

            Random random = new Random();
            if (takeMoves.Count > 0)
            {
                return takeMoves[random.Next(0, takeMoves.Count)];
            }
            else if (results.Count > 0)
            {
                return results[random.Next(0, results.Count)];
            }
            else
            {
                throw new Exception($"Board contains no moves for player {getNextMove.Player}");
            }
        }
    }
}