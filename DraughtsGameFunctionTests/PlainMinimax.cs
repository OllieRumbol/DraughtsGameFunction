using DraughtsGameFunctionService.Engine;
using System.Collections.Generic;

namespace DraughtsGameFunctionTests;

/// <summary>
/// Plain minimax with no pruning, for test 13 (plan section 7.2). It uses the same end-of-game scores as the search
/// (plan section 3.7, item 1), and of several moves with the top score it keeps the first in generator order.
/// </summary>
internal static class PlainMinimax
{
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

        if (depth == 0)
        {
            return evaluator.Evaluate(board, player);
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
}
