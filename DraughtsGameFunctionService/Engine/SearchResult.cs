#nullable enable

namespace DraughtsGameFunctionService.Engine;

/// <summary>
/// The move the search chose, the deepest depth it finished, and that move's score for the player who moves.
/// </summary>
public sealed record SearchResult(Move Move, int CompletedDepth, int Score);
