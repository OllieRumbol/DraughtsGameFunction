using DraughtsGameAPIModels;
using System;
using System.Collections.Generic;
using System.Text;

namespace DraughtsGameAPIService.Instance
{
    public interface IAutomatedPlayerService
    {
        NextMove GetNextMoveForAutomatedPlayer(GetNextMove getNextMove);
    }
}
