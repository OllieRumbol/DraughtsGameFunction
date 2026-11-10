#nullable enable

namespace DraughtsGameFunctionService.Engine;

/// <summary>
/// A position found in the transposition table: the depth it was searched to, its score for the player who moves, what
/// the score means, and the index of its best move in generator order.
/// </summary>
public readonly record struct TableEntry(int Depth, int Score, ScoreBound Bound, int BestMoveIndex);
