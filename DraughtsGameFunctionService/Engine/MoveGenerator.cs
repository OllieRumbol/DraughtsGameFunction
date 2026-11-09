#nullable enable

using System;
using System.Collections.Generic;
using System.Numerics;

namespace DraughtsGameFunctionService.Engine;

/// <summary>
/// The legal moves under the house rules (spec sections 2.1 and 6.1.3).
/// </summary>
public static class MoveGenerator
{
    /// <summary>
    /// Fills <paramref name="moves"/> with every legal move for the player. The list is cleared first.
    /// </summary>
    public static void Generate(int[] board, int player, List<Move> moves)
    {
        CheckArguments(board, player);
        moves.Clear();

        for (var square = 0; square < Board.SquareCount; square++)
        {
            var piece = board[square];
            if (!Board.BelongsTo(piece, player))
            {
                continue;
            }

            AddSimpleMoves(board, square, piece, moves);

            var firstCaptureMove = moves.Count;
            AddCaptureChains(board, start: square, current: square, piece, player, captured: 0, moves, firstCaptureMove);
        }
    }

    /// <summary>True when the player has at least one legal move. It stops at the first one it finds.</summary>
    public static bool HasMove(int[] board, int player)
    {
        CheckArguments(board, player);

        for (var square = 0; square < Board.SquareCount; square++)
        {
            var piece = board[square];
            if (!Board.BelongsTo(piece, player))
            {
                continue;
            }

            foreach (var direction in DirectionsFor(piece))
            {
                var next = Board.OneStep[square, direction];
                if (next >= 0 && Board.IsEmpty(board[next]))
                {
                    return true;
                }

                var landing = Board.TwoSteps[square, direction];
                if (landing >= 0 && Board.IsEmpty(board[landing]) && CanCapture(piece, player, board[next]))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Plays the move on the board: removes the captured pieces, moves the piece and promotes a man on the far row.
    /// The start square is cleared before the piece is written, so a move that ends where it began keeps the piece.
    /// </summary>
    public static void Apply(int[] board, Move move)
    {
        var piece = board[move.From];
        board[move.From] = Board.Empty;

        var captured = move.Captured;
        while (captured != 0)
        {
            var square = BitOperations.TrailingZeroCount(captured);
            board[square] = Board.Empty;
            captured &= captured - 1;
        }

        board[move.To] = Promote(piece, move.To);
    }

    // Any player value other than 1 would otherwise be read as player 2.
    private static void CheckArguments(int[] board, int player)
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(board.Length, Board.SquareCount, nameof(board));

        if (player is not (1 or 2))
        {
            throw new ArgumentOutOfRangeException(nameof(player), player, "The player must be 1 or 2.");
        }
    }

    private static void AddSimpleMoves(int[] board, int square, int piece, List<Move> moves)
    {
        foreach (var direction in DirectionsFor(piece))
        {
            var next = Board.OneStep[square, direction];
            if (next >= 0 && Board.IsEmpty(board[next]))
            {
                moves.Add(new Move(square, next, Captured: 0));
            }
        }
    }

    /// <summary>
    /// Follows every capture chain from <paramref name="current"/>. Each landing square is a separate legal move,
    /// because a chain may stop after any jump (spec section 6.1.3, item 4).
    /// </summary>
    private static void AddCaptureChains(int[] board, int start, int current, int piece, int player, ulong captured, List<Move> moves, int firstCaptureMove)
    {
        foreach (var direction in DirectionsFor(piece))
        {
            var over = Board.OneStep[current, direction];
            var landing = Board.TwoSteps[current, direction];
            if (over < 0 || landing < 0)
            {
                continue;
            }

            // A piece cannot be captured twice in the same chain (item 5).
            var overBit = 1UL << over;
            if ((captured & overBit) != 0 || !CanCapture(piece, player, board[over]))
            {
                continue;
            }

            // The piece has left its start square, so a king may land back on it (item 8).
            var canLand = landing == start || Board.IsEmpty(board[landing]);
            if (!canLand)
            {
                continue;
            }

            var capturedNow = captured | overBit;
            AddOnce(new Move(start, landing, capturedNow), moves, firstCaptureMove);

            // A man that reaches the far row becomes a king, and its move ends (item 7).
            var becomesKing = !Board.IsKing(piece) && Board.Row(landing) == Board.FarRow(player);
            if (!becomesKing)
            {
                AddCaptureChains(board, start, landing, piece, player, capturedNow, moves, firstCaptureMove);
            }
        }
    }

    // Two chains with the same start, end and captured pieces are one move (item 9). Only this piece's
    // capture moves can match, so the search starts at its first one.
    private static void AddOnce(Move move, List<Move> moves, int firstCaptureMove)
    {
        for (var i = firstCaptureMove; i < moves.Count; i++)
        {
            if (moves[i] == move)
            {
                return;
            }
        }

        moves.Add(move);
    }

    // Men capture only men, and kings capture men and kings (spec section 2.1, item 4).
    private static bool CanCapture(int piece, int player, int target)
    {
        var opponent = 3 - player;
        if (!Board.BelongsTo(target, opponent))
        {
            return false;
        }

        return Board.IsKing(piece) || !Board.IsKing(target);
    }

    // Men move and capture forward only. Kings use all four directions (spec section 2.1, item 3).
    private static int[] DirectionsFor(int piece) => piece switch
    {
        Board.Player1Man => Player1ManDirections,
        Board.Player2Man => Player2ManDirections,
        _ => KingDirections,
    };

    private static readonly int[] Player1ManDirections = [Board.UpLeft, Board.UpRight];
    private static readonly int[] Player2ManDirections = [Board.DownLeft, Board.DownRight];
    private static readonly int[] KingDirections = [Board.UpLeft, Board.UpRight, Board.DownLeft, Board.DownRight];

    private static int Promote(int piece, int square)
    {
        var row = Board.Row(square);

        return piece switch
        {
            Board.Player1Man when row == Board.FarRow(1) => Board.Player1King,
            Board.Player2Man when row == Board.FarRow(2) => Board.Player2King,
            _ => piece,
        };
    }
}
