#nullable enable

namespace DraughtsGameFunctionService.Engine;

/// <summary>
/// The move the search chose, the deepest depth it finished, and that move's score for the player who moves.
/// When no depth finished, <see cref="CompletedDepth"/> and <see cref="Score"/> are 0.
/// </summary>
public sealed record SearchResult(Move Move, int CompletedDepth, int Score);
