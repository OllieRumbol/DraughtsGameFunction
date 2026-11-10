#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace DraughtsGameFunctionService.Engine;

/// <summary>
/// A negamax alpha-beta search that deepens one move at a time (plan section 3.7). A move is one ply, and a capture
/// chain is one move. It stops at the maximum depth, when the time limit passes, when it finds a forced win, or when
/// every move loses. It remembers scored positions in a transposition table, and follows captures past the depth
/// limit when the evaluator asks for it.
/// Create one search for each request: it keeps working buffers and its table between calls, so it is not
/// thread-safe. Each call starts with an empty table.
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
    /// evaluation score. A search with a time limit and no maximum depth stops here too.
    /// </summary>
    public const int MaxPly = 64;

    private const int TieScore = 0;

    // Wider than any score, so the first move searched always sets the best score.
    private const int Infinity = WinScore + 1;

    // Plan section 3.5: the clock is read once every this many positions.
    private const int PositionsBetweenClockChecks = 1_024;

    private readonly IEvaluator _evaluator = evaluator;
    private readonly TimeProvider _timeProvider = timeProvider;

    // Null in the fixed-depth test mode: of several moves with the top score, the first in generator order is kept
    // (spec section 6.1.5, item 9).
    private readonly Random? _tieBreaker = tieBreaker;

    // No move from the table, so no move is put first.
    private const int NoMove = -1;

    // One board, one move list and one search order for each ply, so the search does not allocate as it goes.
    // Quiescence can go past the depth limit, up to MaxPly.
    private readonly int[][] _boards = NewBuffers(() => new int[Board.SquareCount]);
    private readonly List<Move>[] _moveLists = NewBuffers(() => new List<Move>());
    private readonly List<int>[] _searchOrders = NewBuffers(() => new List<int>());

    private readonly TranspositionTable _table = new();

    // The root moves with the top score at the current depth, by their index in generator order.
    private readonly List<int> _tiedMoves = [];

    // The time limit of the current search, and whether it has passed.
    private TimeSpan? _timeLimit;
    private long _startTimestamp;
    private long _positionCount;
    private bool _timeIsUp;

    /// <summary>
    /// Chooses a move for <paramref name="player"/>, who must have at least one legal move.
    /// It returns the best move of the deepest depth that finished. When no depth finished, because the player has
    /// only one legal move or the time limit passed first, it returns the first legal move with a depth and score of 0.
    /// </summary>
    public SearchResult Search(int[] board, int player, SearchLimits limits)
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(board.Length, Board.SquareCount, nameof(board));
        var maxDepth = MaxDepthOf(limits);

        StartClock(limits.TimeLimit);
        _table.Clear();

        // The caller's board is copied, so the search never changes it.
        Array.Copy(board, _boards[0], Board.SquareCount);

        var rootMoves = _moveLists[0];
        MoveGenerator.Generate(_boards[0], player, rootMoves);
        if (rootMoves.Count == 0)
        {
            throw new ArgumentException($"Player {player} has no legal move.", nameof(board));
        }

        // Plan section 4: if depth 1 does not finish in time, the first legal move is returned.
        var result = new SearchResult(rootMoves[0], CompletedDepth: 0, Score: 0);

        // Spec section 6.1.5, item 7: one legal move is returned at once, without a search.
        if (rootMoves.Count == 1)
        {
            return result;
        }

        // The root moves in the order they are searched, by their index in generator order.
        var searchOrder = Enumerable.Range(0, rootMoves.Count).ToList();

        for (var depth = 1; depth <= maxDepth; depth++)
        {
            CheckClock();
            if (_timeIsUp)
            {
                break;
            }

            // Plan section 3.5: a depth that did not finish is abandoned, and the last finished depth is used.
            var finishedDepth = SearchRoot(player, depth, searchOrder);
            if (finishedDepth is null)
            {
                break;
            }

            var (bestIndex, bestScore) = finishedDepth.Value;
            result = new SearchResult(rootMoves[bestIndex], depth, bestScore);

            // Plan section 3.7, item 5: the best move is searched first at the next depth.
            searchOrder.Remove(bestIndex);
            searchOrder.Insert(0, bestIndex);

            // Spec section 6.1.5, item 7, and plan section 3.7, item 6: stop when the result is decided within this
            // depth. A depth sees every win and loss within that many moves, so a deeper search cannot change them.
            // A win: the first depth that finds one finds the fastest. A loss: the best move loses, so every move
            // loses, and the slowest loss is already chosen.
            if (IsWinOrLossWithin(bestScore, depth))
            {
                break;
            }
        }

        return result;
    }

    /// <summary>
    /// Searches every root move to <paramref name="depth"/>. Returns the chosen move's index in generator order and
    /// its score, or null if the time limit passed first.
    /// </summary>
    private (int MoveIndex, int Score)? SearchRoot(int player, int depth, List<int> searchOrder)
    {
        var rootMoves = _moveLists[0];
        var bestScore = -Infinity;
        _tiedMoves.Clear();

        foreach (var index in searchOrder)
        {
            // Plan section 3.5: the window starts one point below the best score, so a move that equals the best
            // returns its exact score and is seen as a tie. A move that scores lower returns a bound below the best.
            var alpha = bestScore - 1;
            var child = PlayMove(ply: 0, rootMoves[index]);
            var score = -Negamax(child, 3 - player, depth - 1, ply: 1, -Infinity, -alpha);

            if (_timeIsUp)
            {
                return null;
            }

            if (score > bestScore)
            {
                bestScore = score;
                _tiedMoves.Clear();
                _tiedMoves.Add(index);
            }
            else if (score == bestScore)
            {
                _tiedMoves.Add(index);
            }
        }

        return (ChooseTiedMove(), bestScore);
    }

    /// <summary>
    /// Chooses one of the moves with the top score (spec section 6.1.5, item 7). The choice goes by generator order,
    /// not by search order, so putting the last depth's best move first does not change a fixed-depth result.
    /// </summary>
    private int ChooseTiedMove()
    {
        _tiedMoves.Sort();

        if (_tieBreaker is null)
        {
            return _tiedMoves[0];
        }

        return _tiedMoves[_tieBreaker.Next(_tiedMoves.Count)];
    }

    /// <summary>
    /// The score of the position for <paramref name="player"/>, who moves next. When the time limit passes, it
    /// returns 0 and sets <see cref="_timeIsUp"/>, and every caller returns without using the score.
    /// </summary>
    private int Negamax(int[] board, int player, int depth, int ply, int alpha, int beta)
    {
        if (CountPositionAndCheckClock())
        {
            return 0;
        }

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
            return _evaluator.UsesQuiescence
                ? Quiescence(board, player, ply, alpha, beta)
                : _evaluator.Evaluate(board, player);
        }

        // Plan section 3.7: a stored score is used only when the position was searched to exactly this depth, so the
        // search gives the same result as plain minimax at a fixed depth. The stored best move is used at any depth.
        var key = TranspositionTable.KeyOf(board, player);
        var tableMoveIndex = NoMove;

        if (_table.TryGet(key, ply, out var entry))
        {
            if (entry.Depth == depth && CanUseStoredScore(entry, alpha, beta))
            {
                return entry.Score;
            }

            tableMoveIndex = entry.BestMoveIndex;
        }

        var moves = _moveLists[ply];
        MoveGenerator.Generate(board, player, moves);

        var originalAlpha = alpha;
        var bestScore = -Infinity;
        var bestIndex = 0;

        foreach (var index in SearchOrder(moves, tableMoveIndex, ply, capturesOnly: false))
        {
            var child = PlayMove(ply, moves[index]);
            var score = -Negamax(child, 3 - player, depth - 1, ply + 1, -beta, -alpha);

            // A depth that did not finish stores nothing (T14 notes).
            if (_timeIsUp)
            {
                return 0;
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestIndex = index;
            }

            alpha = Math.Max(alpha, score);

            if (alpha >= beta)
            {
                break;
            }
        }

        _table.Store(key, depth, ply, bestScore, BoundOf(bestScore, originalAlpha, beta), bestIndex);

        return bestScore;
    }

    /// <summary>
    /// Plan section 3.7: at depth 0, keeps searching captures, so a position in the middle of an exchange is scored as
    /// the exchange ends. The player may stop capturing at any point and keep the evaluation ("stand pat"), because
    /// captures are optional (spec section 2.1, item 1). The caller has already counted this position and checked the
    /// end of the game. Quiescence scores are not stored in the table.
    /// </summary>
    private int Quiescence(int[] board, int player, int ply, int alpha, int beta)
    {
        var bestScore = _evaluator.Evaluate(board, player);

        // There is no buffer past MaxPly, so the search stands pat there.
        if (bestScore >= beta || ply == MaxPly)
        {
            return bestScore;
        }

        alpha = Math.Max(alpha, bestScore);

        var moves = _moveLists[ply];
        MoveGenerator.Generate(board, player, moves);

        foreach (var index in SearchOrder(moves, NoMove, ply, capturesOnly: true))
        {
            var child = PlayMove(ply, moves[index]);
            var score = -Negamax(child, 3 - player, depth: 0, ply + 1, -beta, -alpha);

            if (_timeIsUp)
            {
                return 0;
            }

            bestScore = Math.Max(bestScore, score);
            alpha = Math.Max(alpha, score);

            if (alpha >= beta)
            {
                break;
            }
        }

        return bestScore;
    }

    /// <summary>
    /// The order to search the moves in, by their index in generator order (plan section 3.7, item 4): the table's
    /// best move first, then moves that capture more pieces before moves that capture fewer, then generator order.
    /// </summary>
    private List<int> SearchOrder(List<Move> moves, int tableMoveIndex, int ply, bool capturesOnly)
    {
        var order = _searchOrders[ply];
        order.Clear();

        for (var index = 0; index < moves.Count; index++)
        {
            if (capturesOnly && moves[index].Captured == 0)
            {
                continue;
            }

            // An insertion sort. It keeps generator order between moves of the same priority, and the lists are short.
            var priority = OrderPriority(moves, index, tableMoveIndex);
            var position = order.Count;

            while (position > 0 && OrderPriority(moves, order[position - 1], tableMoveIndex) < priority)
            {
                position--;
            }

            order.Insert(position, index);
        }

        return order;
    }

    // The table's move has an index that may be out of range after a key collision (risk 10). Then no move matches it.
    private static int OrderPriority(List<Move> moves, int index, int tableMoveIndex) =>
        index == tableMoveIndex ? int.MaxValue : BitOperations.PopCount(moves[index].Captured);

    private static bool CanUseStoredScore(TableEntry entry, int alpha, int beta) => entry.Bound switch
    {
        ScoreBound.Exact => true,
        ScoreBound.Lower => entry.Score >= beta,
        ScoreBound.Upper => entry.Score <= alpha,
        _ => false,
    };

    // Alpha-beta stops looking at a position once its score is outside the window, so the score is then only a bound.
    private static ScoreBound BoundOf(int score, int originalAlpha, int beta)
    {
        if (score <= originalAlpha)
        {
            return ScoreBound.Upper;
        }

        if (score >= beta)
        {
            return ScoreBound.Lower;
        }

        return ScoreBound.Exact;
    }

    // True when the score is a win or a loss no more than depth moves ahead. The check uses the depth, not only the
    // size of the score, because quiescence can find a win or loss beyond the depth, which a deeper search could
    // still change.
    private static bool IsWinOrLossWithin(int score, int depth) => Math.Abs(score) >= WinScore - depth;

    // Copies the board at this ply into the next ply's buffer and plays the move there.
    private int[] PlayMove(int ply, Move move)
    {
        var child = _boards[ply + 1];
        Array.Copy(_boards[ply], child, Board.SquareCount);
        MoveGenerator.Apply(child, move);

        return child;
    }

    private static int MaxDepthOf(SearchLimits limits)
    {
        if (limits.MaxDepth is null && limits.TimeLimit is null)
        {
            throw new ArgumentException("The search needs a maximum depth, a time limit or both.", nameof(limits));
        }

        if (limits.MaxDepth is int depth)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(depth, 1, nameof(limits));
            ArgumentOutOfRangeException.ThrowIfGreaterThan(depth, MaxPly, nameof(limits));
        }

        if (limits.TimeLimit is TimeSpan timeLimit)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeLimit, TimeSpan.Zero, nameof(limits));
        }

        return limits.MaxDepth ?? MaxPly;
    }

    private void StartClock(TimeSpan? timeLimit)
    {
        _timeLimit = timeLimit;
        _startTimestamp = timeLimit is null ? 0 : _timeProvider.GetTimestamp();
        _positionCount = 0;
        _timeIsUp = false;
    }

    // Plan section 3.5: the clock is read every 1,024 positions, not at every one, because reading it takes time.
    private bool CountPositionAndCheckClock()
    {
        _positionCount++;

        if (_positionCount % PositionsBetweenClockChecks == 0)
        {
            CheckClock();
        }

        return _timeIsUp;
    }

    // Reads the clock, and sets _timeIsUp when the time limit has passed. With no time limit, it never passes.
    private void CheckClock()
    {
        if (_timeLimit is TimeSpan timeLimit && _timeProvider.GetElapsedTime(_startTimestamp) >= timeLimit)
        {
            _timeIsUp = true;
        }
    }

    // One buffer for each ply from 0 to MaxPly.
    private static T[] NewBuffers<T>(Func<T> create)
    {
        var buffers = new T[MaxPly + 1];

        for (var ply = 0; ply <= MaxPly; ply++)
        {
            buffers[ply] = create();
        }

        return buffers;
    }
}
