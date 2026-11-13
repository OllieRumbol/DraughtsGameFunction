using DraughtsGameFunctionService.Engine;

namespace DraughtsGameFunctionSelfPlay;

/// <summary>
/// One game of computer against computer, with the rules in spec section 6.4, item 3, and plan section 7.4.
/// A move is one player's turn.
/// </summary>
public static class SelfPlayGame
{
    /// <summary>Plan section 7.4: each game opens with this many random moves, so the games are not all the same.</summary>
    public const int OpeningMoves = 4;

    /// <summary>Spec section 6.4, item 3: a draw after this many moves in a row with no capture.</summary>
    public const int MovesWithoutCaptureForDraw = 50;

    /// <summary>Spec section 6.4, item 3: a draw after this many moves in all.</summary>
    public const int MovesForDraw = 200;

    /// <summary>
    /// Plays one game from <paramref name="startBoard"/>. Player 1 moves first. The first
    /// <paramref name="openingMoves"/> moves are chosen at random with <paramref name="random"/>, and then each
    /// player's move by its <see cref="ChooseMove"/>. Tests can set fewer opening moves, so a position stays as built.
    /// </summary>
    public static GameResult Play(
        int[] startBoard, ChooseMove player1, ChooseMove player2, Random random, int openingMoves = OpeningMoves)
    {
        ArgumentNullException.ThrowIfNull(startBoard);
        ArgumentOutOfRangeException.ThrowIfNotEqual(startBoard.Length, Board.SquareCount, nameof(startBoard));
        ArgumentOutOfRangeException.ThrowIfNegative(openingMoves);

        var board = (int[])startBoard.Clone();
        var legalMoves = new List<Move>();
        var player = 1;
        var movesWithoutCapture = 0;

        if (!MoveGenerator.HasMove(board, player))
        {
            throw new ArgumentException("Player 1 has no legal move on the start board.", nameof(startBoard));
        }

        for (var moveNumber = 1; ; moveNumber++)
        {
            MoveGenerator.Generate(board, player, legalMoves);

            var move = moveNumber <= openingMoves
                ? legalMoves[random.Next(legalMoves.Count)]
                : (player == 1 ? player1 : player2)(board, player);

            // A player that returns a move the rules do not allow would make the scores meaningless.
            if (!legalMoves.Contains(move))
            {
                throw new InvalidOperationException($"Player {player} chose an illegal move at move {moveNumber}.");
            }

            MoveGenerator.Apply(board, move);
            movesWithoutCapture = move.Captured == 0 ? movesWithoutCapture + 1 : 0;

            var result = ResultAfterMove(board, moveNumber, movesWithoutCapture);
            if (result is not null)
            {
                return result;
            }

            player = 3 - player;
        }
    }

    // Spec section 2.1, item 6, after every move, then the draw rules for these games only.
    private static GameResult? ResultAfterMove(int[] board, int moveNumber, int movesWithoutCapture)
    {
        var player1CanMove = MoveGenerator.HasMove(board, 1);
        var player2CanMove = MoveGenerator.HasMove(board, 2);

        if (!player1CanMove && !player2CanMove)
        {
            return new GameResult(GameOutcome.Draw, moveNumber, "Neither player can move.");
        }

        if (!player2CanMove)
        {
            return new GameResult(GameOutcome.Player1Wins, moveNumber, "Player 2 cannot move.");
        }

        if (!player1CanMove)
        {
            return new GameResult(GameOutcome.Player2Wins, moveNumber, "Player 1 cannot move.");
        }

        if (movesWithoutCapture >= MovesWithoutCaptureForDraw)
        {
            return new GameResult(GameOutcome.Draw, moveNumber, $"{MovesWithoutCaptureForDraw} moves without a capture.");
        }

        if (moveNumber >= MovesForDraw)
        {
            return new GameResult(GameOutcome.Draw, moveNumber, $"{MovesForDraw} moves in all.");
        }

        return null;
    }
}
