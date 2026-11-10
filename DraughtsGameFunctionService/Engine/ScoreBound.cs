#nullable enable

namespace DraughtsGameFunctionService.Engine;

/// <summary>
/// What a score in the transposition table means (plan section 3.7). An alpha-beta search often stops looking at a
/// position as soon as it knows the score is too high or too low, so the score it has is only a bound.
/// </summary>
public enum ScoreBound : byte
{
    /// <summary>The score is the position's exact score.</summary>
    Exact,

    /// <summary>The position's score is this score or higher.</summary>
    Lower,

    /// <summary>The position's score is this score or lower.</summary>
    Upper,
}
