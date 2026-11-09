using DraughtsGameFunctionModels.Service;
using DraughtsGameFunctionService.Engine;
using DraughtsGameFunctionService.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using static DraughtsGameFunctionTests.EngineBoards;

namespace DraughtsGameFunctionTests;

/// <summary>
/// Plan section 7.2, test 12: the old and new move generators agree, except for defects 1 and 10.
/// This test is removed with the old code, in step 2 of the plan.
/// </summary>
[TestClass]
public class MoveGeneratorComparisonTests(TestContext testContext)
{
    private const int Seed = 20261109;
    private const int GameCount = 60;
    private const int MaxPliesPerGame = 120;

    // The old code crashes on a king chain that loops away from its start square (defect 9). Any such loop
    // takes at least 4 jumps, over 4 different pieces, so a position whose king captures stop below 4 is safe.
    private const int CapturesThatCouldLoop = 4;

    [TestMethod]
    public void Generate_FixedAndPlayedPositions_MatchesOldCodeExceptDefects1And10()
    {
        // Arrange
        var positions = FixedPositions().Select(position => (position.Board, position.Player, IsKnownSafe: true))
            .Concat(PlayedPositions().Select(position => (position.Board, position.Player, IsKnownSafe: false)))
            .ToList();
        var comparedCount = 0;
        var skippedCount = 0;
        var endsOnStart = new List<string>();
        var otherKingCaptures = new List<string>();
        var unexpected = new List<string>();

        foreach (var (board, player, isKnownSafe) in positions)
        {
            var newMoves = Generate(board, player).ToHashSet();
            if (!isKnownSafe && CouldLoop(board, newMoves))
            {
                skippedCount++;
                continue;
            }

            // Act
            var oldMoves = FindMove.FindAvailableMoves(ToRows(board), player).Select(ToMove).ToHashSet();
            comparedCount++;

            // Assert
            foreach (var move in oldMoves.Except(newMoves))
            {
                unexpected.Add($"Only the old code gives {Describe(move)} on {Text(board)}");
            }

            foreach (var move in newMoves.Except(oldMoves))
            {
                var finding = $"{Describe(move)} on {Text(board)}";
                if (!Board.IsKing(board[move.From]) || move.Captured == 0)
                {
                    unexpected.Add($"Only the new code gives {finding}");
                }
                else if (move.To == move.From)
                {
                    endsOnStart.Add(finding);
                }
                else
                {
                    otherKingCaptures.Add(finding);
                }
            }
        }

        testContext.WriteLine($"Compared {comparedCount} positions and skipped {skippedCount} that could loop.");
        testContext.WriteLine($"New king captures that end on the start square (defect 10): {endsOnStart.Count}.");
        testContext.WriteLine($"Other new king captures (defect 1, or defect 10 where the chain passes its start square): {otherKingCaptures.Count}.");
        foreach (var finding in endsOnStart.Concat(otherKingCaptures))
        {
            testContext.WriteLine(finding);
        }

        Assert.IsEmpty(unexpected, string.Join(Environment.NewLine, unexpected.Take(10)));
        Assert.IsNotEmpty(endsOnStart, "Example 19 should show defect 10.");
        Assert.IsNotEmpty(otherKingCaptures, "Example 3 should show defect 1.");
        Assert.IsGreaterThan(1000, comparedCount, "Too few positions were compared to trust the result.");
    }

    // The examples are where the two generators should differ (examples 3 and 19) and where they should agree.
    // None of them crashes the old code: example 19 loops back to its start square, where the old code stops.
    private static IEnumerable<(int[] Board, int Player)> FixedPositions()
    {
        var start = Board.FromRows(StartingRows());
        yield return (start, 1);
        yield return (start, 2);

        // Example 3 (defect 1).
        yield return (Create((3, 2, Board.Player2King), (4, 3, Board.Player1Man), (2, 3, Board.Player1Man)), 2);

        // Example 4.
        yield return (Create((6, 1, Board.Player1Man), (5, 2, Board.Player2Man), (3, 4, Board.Player2Man)), 1);

        // Examples 17 and 18.
        yield return (Create((6, 1, Board.Player1Man), (5, 2, Board.Player2King)), 1);
        yield return (Create((6, 1, Board.Player1King), (5, 2, Board.Player2King)), 1);

        // Example 19 (defect 10). Example 20 is left out, because it crashes the old code (defect 9).
        yield return (Create(
            (1, 2, Board.Player2King),
            (2, 3, Board.Player1Man), (4, 3, Board.Player1Man), (4, 1, Board.Player1Man), (2, 1, Board.Player1Man)), 2);
    }

    // Positions from games of random legal moves, so that kings, long chains and crowded boards all appear.
    // The seed is fixed, so every run compares the same positions.
    private static IEnumerable<(int[] Board, int Player)> PlayedPositions()
    {
        var random = new Random(Seed);
        var moves = new List<Move>();

        for (var game = 0; game < GameCount; game++)
        {
            var board = Board.FromRows(StartingRows());
            var player = 1;

            for (var ply = 0; ply < MaxPliesPerGame; ply++)
            {
                yield return ((int[])board.Clone(), 1);
                yield return ((int[])board.Clone(), 2);

                MoveGenerator.Generate(board, player, moves);
                Assert.AreEqual(moves.Count > 0, MoveGenerator.HasMove(board, player), $"HasMove disagrees with Generate on {Text(board)}");
                if (moves.Count == 0)
                {
                    break;
                }

                MoveGenerator.Apply(board, moves[random.Next(moves.Count)]);
                player = 3 - player;
            }
        }
    }

    private static bool CouldLoop(int[] board, HashSet<Move> moves) =>
        moves.Any(move => Board.IsKing(board[move.From]) && move.CaptureCount >= CapturesThatCouldLoop);

    private static Move ToMove(NextMove move)
    {
        var captured = 0UL;
        foreach (var piece in move.Takes)
        {
            captured |= 1UL << Board.Index((int)piece.Height, (int)piece.Width);
        }

        var from = Board.Index((int)move.CurrentHeight, (int)move.CurrentWidth);
        var to = Board.Index((int)move.NextHeight, (int)move.NextWidth);

        return new Move(from, to, captured);
    }

    // The board as 8 rows of digits, for a failure message that can be pasted back into a test.
    private static string Text(int[] board) =>
        string.Join("/", Enumerable.Range(0, Board.Size).Select(row => string.Concat(board.Skip(row * Board.Size).Take(Board.Size))));
}
