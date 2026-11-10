using DraughtsGameFunctionService.Engine;
using System;
using System.Collections.Generic;

namespace DraughtsGameFunctionTests;

/// <summary>
/// Plain minimax with no pruning and no table, for test 13 (plan section 7.2). It uses the same end-of-game scores as
/// the search (plan section 3.7, item 1), and of several moves with the top score it keeps the first in generator order.
/// When the evaluator uses quiescence, it follows captures at depth 0, and may stand pat (plan section 3.7).
/// </summary>
internal static class PlainMinimax
{
    // Wider than any score.
    private const int FullWindow = GameTreeSearch.WinScore + 1;

    public static (Move Move, int Score) BestMove(int[] board, int player, int depth, IEvaluator evaluator)
    {
        var moves = new List<Move>();
        MoveGenerator.Generate(board, player, moves);

        var bestMove = moves[0];
        var bestScore = int.MinValue;

        foreach (var move in moves)
        {
            var score = ScoreOfMove(board, player, move, depth, evaluator);
            if (score > bestScore)
            {
                bestScore = score;
                bestMove = move;
            }
        }

        return (bestMove, bestScore);
    }

    /// <summary>The score for <paramref name="player"/> of playing <paramref name="move"/> at the root.</summary>
    public static int ScoreOfMove(int[] board, int player, Move move, int depth, IEvaluator evaluator)
    {
        var child = (int[])board.Clone();
        MoveGenerator.Apply(child, move);

        return -Score(child, 3 - player, depth - 1, ply: 1, evaluator);
    }

    private static int Score(int[] board, int player, int depth, int ply, IEvaluator evaluator)
    {
        if (EndOfGameScore(board, player, ply) is int endScore)
        {
            return endScore;
        }

        if (depth == 0)
        {
            return evaluator.UsesQuiescence
                ? Quiescence(board, player, ply, -FullWindow, FullWindow, evaluator)
                : evaluator.Evaluate(board, player);
        }

        var moves = new List<Move>();
        MoveGenerator.Generate(board, player, moves);

        var best = int.MinValue;
        foreach (var move in moves)
        {
            var child = (int[])board.Clone();
            MoveGenerator.Apply(child, move);

            var score = -Score(child, 3 - player, depth - 1, ply + 1, evaluator);
            if (score > best)
            {
                best = score;
            }
        }

        return best;
    }

    // Stand pat, or follow any capture, up to the ply cap. Following every capture with no pruning takes minutes at
    // depth 4, so this part uses alpha-beta. Started with a window wider than any score, alpha-beta returns exactly
    // the score that plain search would. Test 31 checks quiescence by hand.
    private static int Quiescence(int[] board, int player, int ply, int alpha, int beta, IEvaluator evaluator)
    {
        var best = evaluator.Evaluate(board, player);
        if (best >= beta || ply == GameTreeSearch.MaxPly)
        {
            return best;
        }

        alpha = Math.Max(alpha, best);

        var moves = new List<Move>();
        MoveGenerator.Generate(board, player, moves);

        foreach (var move in moves)
        {
            if (move.Captured == 0)
            {
                continue;
            }

            var child = (int[])board.Clone();
            MoveGenerator.Apply(child, move);

            var score = EndOfGameScore(child, 3 - player, ply + 1)
                ?? Quiescence(child, 3 - player, ply + 1, -beta, -alpha, evaluator);

            best = Math.Max(best, -score);
            alpha = Math.Max(alpha, -score);
            if (alpha >= beta)
            {
                break;
            }
        }

        return best;
    }

    // The end-of-game scores in plan section 3.7, item 1, or null while both players can move.
    private static int? EndOfGameScore(int[] board, int player, int ply)
    {
        var playerCanMove = MoveGenerator.HasMove(board, player);
        var opponentCanMove = MoveGenerator.HasMove(board, 3 - player);

        if (!playerCanMove && !opponentCanMove)
        {
            return 0;
        }

        if (!opponentCanMove)
        {
            return GameTreeSearch.WinScore - ply;
        }

        if (!playerCanMove)
        {
            return -GameTreeSearch.WinScore + ply;
        }

        return null;
    }
}
