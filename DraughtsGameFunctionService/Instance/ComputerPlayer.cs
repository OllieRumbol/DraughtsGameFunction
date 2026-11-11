#nullable enable

using DraughtsGameFunctionModels.Enums;
using DraughtsGameFunctionModels.Options;
using DraughtsGameFunctionService.Engine;
using DraughtsGameFunctionService.Helpers;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DraughtsGameFunctionService.Instance;

/// <summary>
/// Chooses the computer's move for a difficulty (spec section 6.1.5, and plan section 3.3).
/// It is safe to share between requests: each call creates its own search, the evaluators keep no state, and in
/// production <paramref name="random"/> is <see cref="Random.Shared"/>, which is thread-safe.
/// </summary>
public sealed class ComputerPlayer(IOptions<SearchOptions> options, TimeProvider timeProvider, Random random)
{
    private static readonly MaterialEvaluator NormalEvaluator = new();
    private static readonly PositionalEvaluator HardEvaluator = new();

    /// <summary>
    /// The move for <paramref name="player"/>, or null when that player has no legal move. The function always passes
    /// player 2. The self-play tool passes either player.
    /// </summary>
    public ComputerMove? GetNextMove(long[,] board, int player, Difficulty difficulty)
    {
        ArgumentNullException.ThrowIfNull(board);
        if (board.GetLength(0) != Board.Size || board.GetLength(1) != Board.Size)
        {
            throw new ArgumentException("The board must be 8 rows of 8 values.", nameof(board));
        }

        if (player is not (1 or 2))
        {
            throw new ArgumentOutOfRangeException(nameof(player), player, "The player must be 1 or 2.");
        }

        var engineBoard = Board.FromRows(board);

        // Spec section 6.1.5, item 3: the caller turns this into an error.
        if (!MoveGenerator.HasMove(engineBoard, player))
        {
            return null;
        }

        var settings = options.Value;

        return difficulty switch
        {
            Difficulty.Easy => new ComputerMove(FindMove.ToNextMove(ChooseEasyMove(engineBoard, player)), CompletedDepth: 0),
            Difficulty.Normal => Search(engineBoard, player, NormalEvaluator, new SearchLimits(
                settings.NormalMaxDepth, TimeSpan.FromMilliseconds(settings.NormalTimeLimitMs))),
            Difficulty.Hard => Search(engineBoard, player, HardEvaluator, new SearchLimits(
                MaxDepth: null, TimeSpan.FromMilliseconds(settings.HardTimeLimitMs))),
            _ => throw new ArgumentOutOfRangeException(nameof(difficulty), difficulty, "Unknown difficulty."),
        };
    }

    // Spec section 6.1.5, item 4, the rule from AutomatedPlayerServiceV1: a random capture when there is one,
    // otherwise a random move.
    private Move ChooseEasyMove(int[] board, int player)
    {
        var moves = new List<Move>();
        MoveGenerator.Generate(board, player, moves);

        var captures = moves.Where(move => move.Captured != 0).ToList();
        var choices = captures.Count > 0 ? captures : moves;

        return choices[random.Next(choices.Count)];
    }

    // Plan section 3.5: a new search, with its own table, for each request. The random tie breaker chooses between
    // moves that share the top score (spec section 6.1.5, item 7).
    private ComputerMove Search(int[] board, int player, IEvaluator evaluator, SearchLimits limits)
    {
        var search = new GameTreeSearch(evaluator, timeProvider, random);
        var result = search.Search(board, player, limits);

        return new ComputerMove(FindMove.ToNextMove(result.Move), result.CompletedDepth);
    }
}
