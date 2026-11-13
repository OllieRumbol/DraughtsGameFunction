using DraughtsGameFunctionService.Engine;

namespace DraughtsGameFunctionSelfPlay;

/// <summary>Chooses a legal move for <paramref name="player"/>, who has at least one.</summary>
public delegate Move ChooseMove(int[] board, int player);
