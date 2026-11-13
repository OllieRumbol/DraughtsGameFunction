namespace DraughtsGameFunctionSelfPlay;

/// <summary>
/// The result of a pairing, for the stronger level: a win scores 1 and a draw 0.5 (spec section 6.4, item 3).
/// </summary>
public sealed record PairingResult(string Stronger, string Weaker, int Games, int StrongerWins, int Draws, int WeakerWins)
{
    public double StrongerScore => StrongerWins + Draws / 2.0;

    public double StrongerPercentage => 100.0 * StrongerScore / Games;
}
