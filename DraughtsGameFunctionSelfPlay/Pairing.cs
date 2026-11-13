namespace DraughtsGameFunctionSelfPlay;

/// <summary>
/// Plays a number of games between two levels. The levels swap sides each game, so each one plays player 1, and so
/// moves first, in half of the games (spec section 6.4, item 3).
/// </summary>
public static class Pairing
{
    /// <summary>
    /// Plays <paramref name="games"/> games from <paramref name="startBoard"/>, which must be an even number.
    /// <paramref name="reportGame"/> receives one line for each game, for progress.
    /// </summary>
    public static PairingResult Run(
        string strongerName, ChooseMove stronger, string weakerName, ChooseMove weaker,
        int[] startBoard, int games, Random random, Action<string>? reportGame = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(games, 2);
        if (games % 2 != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(games), games, "The number of games must be even.");
        }

        var strongerWins = 0;
        var draws = 0;
        var weakerWins = 0;

        for (var game = 1; game <= games; game++)
        {
            var strongerIsPlayer1 = game % 2 == 1;
            var result = strongerIsPlayer1
                ? SelfPlayGame.Play(startBoard, stronger, weaker, random)
                : SelfPlayGame.Play(startBoard, weaker, stronger, random);

            var strongerWon = result.Outcome == (strongerIsPlayer1 ? GameOutcome.Player1Wins : GameOutcome.Player2Wins);
            var weakerWon = result.Outcome == (strongerIsPlayer1 ? GameOutcome.Player2Wins : GameOutcome.Player1Wins);

            if (strongerWon)
            {
                strongerWins++;
            }
            else if (weakerWon)
            {
                weakerWins++;
            }
            else
            {
                draws++;
            }

            var firstMover = strongerIsPlayer1 ? strongerName : weakerName;
            var outcome = strongerWon ? $"{strongerName} wins" : weakerWon ? $"{weakerName} wins" : "draw";
            reportGame?.Invoke($"Game {game}/{games}, {firstMover} first: {outcome} after {result.Moves} moves. {result.Reason}");
        }

        return new PairingResult(strongerName, weakerName, games, strongerWins, draws, weakerWins);
    }
}
