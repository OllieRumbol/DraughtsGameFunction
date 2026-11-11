namespace DraughtsGameFunctionModels.Enums;

/// <summary>
/// How strongly the computer plays (spec section 6.1.5, items 4 to 6, and plan section 3.4).
/// </summary>
public enum Difficulty
{
    /// <summary>A random capture when there is one, otherwise a random move. No search.</summary>
    Easy,

    /// <summary>A search with the material evaluation, to a maximum depth and the Normal time limit.</summary>
    Normal,

    /// <summary>A search with the positional evaluation and quiescence, to the Hard time limit.</summary>
    Hard,
}
