#nullable enable

using System;
using System.Collections.Generic;

namespace DraughtsGameFunctionService.Engine;

/// <summary>
/// A negamax alpha-beta search to a fixed depth (plan section 3.7). A move is one ply, and a capture chain is one move.
/// Create one search for each request: it keeps working buffers between calls, so it is not thread-safe.
/// </summary>
public sealed class GameTreeSearch(IEvaluator evaluator, TimeProvider timeProvider, Random? tieBreaker)
{
    /// <summary>
    /// The score of a win at the root. A win <c>ply</c> moves ahead scores <c>WinScore - ply</c>, so a faster win
    /// scores higher, and a loss scores <c>-WinScore + ply</c>, so a slower loss scores higher.
    /// Evaluation scores stay well below it.
    /// </summary>
    public const int WinScore = 100_000;

    /// <summary>
    /// The deepest the search goes. It keeps the buffers and the call stack small, and a win score far from an
    /// evaluation score. The search is also limited by time from T14 (tasks.md).
    /// </summary>
    public const int MaxPly = 64;

    private const int TieScore = 0;

    // Wider than any score, so the first root move always sets the best score.
    private const int Infinity = WinScore + 1;

    private readonly IEvaluator _evaluator = evaluator;

    // The time limit and the tie breaker are read from T14 (tasks.md). Until then the search runs to MaxDepth,
    // and of several moves with the top score it keeps the first in generator order.
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly Random? _tieBreaker = tieBreaker;

    // One board and one move list for each ply, so the search does not allocate as it goes.
    private int[][] _boards = [];
    private List<Move>[] _moveLists = [];

    /// <summary>
    /// Chooses a move for <paramref name="player"/>, searching <see cref="SearchLimits.MaxDepth"/> moves ahead.
    /// The player must have at least one legal move.
    /// </summary>
    public SearchResult Search(int[] board, int player, SearchLimits limits)
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(board.Length, Board.SquareCount, nameof(board));

        if (limits.MaxDepth is not int depth || depth < 1 || depth > MaxPly)
        {
            throw new ArgumentException($"The search needs a maximum depth from 1 to {MaxPly}.", nameof(limits));
        }

        // Fail loudly rather than search past a limit the caller asked for (spec section 6.1.5, item 8).
        if (limits.TimeLimit is not null)
        {
            throw new NotSupportedException("The time limit is added in T14 (tasks.md).");
        }

        PrepareBuffers(board, depth);

        var rootMoves = _moveLists[0];
        MoveGenerator.Generate(_boards[0], player, rootMoves);
        if (rootMoves.Count == 0)
        {
            throw new ArgumentException($"Player {player} has no legal move.", nameof(board));
        }

        // The root moves are searched in generator order, and only a higher score replaces the best move.
        // So of several moves with the top score, the first in generator order is kept, as plain minimax keeps it.
        var bestMove = rootMoves[0];
        var bestScore = -Infinity;
        var alpha = -Infinity;

        foreach (var move in rootMoves)
        {
            var child = PlayMove(ply: 0, move);
            var score = -Negamax(child, 3 - player, depth - 1, ply: 1, -Infinity, -alpha);

            if (score > bestScore)
            {
                bestScore = score;
                bestMove = move;
            }

            alpha = Math.Max(alpha, score);
        }

        return new SearchResult(bestMove, depth, bestScore);
    }

    /// <summary>The score of the position for <paramref name="player"/>, who moves next.</summary>
    private int Negamax(int[] board, int player, int depth, int ply, int alpha, int beta)
    {
        // The end of the game is checked first, for both players, as the web app checks it after every move
        // (spec section 2.1, item 6). This also catches a move that left its own player with no moves.
        var playerCanMove = MoveGenerator.HasMove(board, player);
        var opponentCanMove = MoveGenerator.HasMove(board, 3 - player);

        if (!playerCanMove && !opponentCanMove)
        {
            return TieScore;
        }

        if (!opponentCanMove)
        {
            return WinScore - ply;
        }

        if (!playerCanMove)
        {
            return -WinScore + ply;
        }

        if (depth == 0)
        {
            return _evaluator.Evaluate(board, player);
        }

        var moves = _moveLists[ply];
        MoveGenerator.Generate(board, player, moves);

        var bestScore = -Infinity;
        foreach (var move in moves)
        {
            var child = PlayMove(ply, move);
            var score = -Negamax(child, 3 - player, depth - 1, ply + 1, -beta, -alpha);

            bestScore = Math.Max(bestScore, score);
            alpha = Math.Max(alpha, score);

            if (alpha >= beta)
            {
                break;
            }
        }

        return bestScore;
    }

    // Copies the board at this ply into the next ply's buffer and plays the move there.
    private int[] PlayMove(int ply, Move move)
    {
        var child = _boards[ply + 1];
        Array.Copy(_boards[ply], child, Board.SquareCount);
        MoveGenerator.Apply(child, move);

        return child;
    }

    private void PrepareBuffers(int[] board, int depth)
    {
        _boards = new int[depth + 1][];
        _moveLists = new List<Move>[depth + 1];

        for (var ply = 0; ply <= depth; ply++)
        {
            _boards[ply] = new int[Board.SquareCount];
            _moveLists[ply] = [];
        }

        // The caller's board is copied, so the search never changes it.
        Array.Copy(board, _boards[0], Board.SquareCount);
    }
}
