#nullable enable

namespace DraughtsGameFunctionService.Engine;

/// <summary>
/// Scores a position for the search (plan section 3.3).
/// </summary>
public interface IEvaluator
{
    /// <summary>A positive score is good for <paramref name="player"/>.</summary>
    int Evaluate(int[] board, int player);

    /// <summary>True when the search keeps following captures at depth 0 (plan section 3.7). Hard only.</summary>
    bool UsesQuiescence { get; }
}
