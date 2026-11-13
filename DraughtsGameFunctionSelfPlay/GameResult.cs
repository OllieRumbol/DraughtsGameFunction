namespace DraughtsGameFunctionSelfPlay;

/// <summary>How a game ended, after how many moves in all, and why.</summary>
public sealed record GameResult(GameOutcome Outcome, int Moves, string Reason);
