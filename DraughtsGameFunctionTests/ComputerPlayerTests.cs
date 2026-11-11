using DraughtsGameFunctionModels.Enums;
using DraughtsGameFunctionModels.Options;
using DraughtsGameFunctionModels.Service;
using DraughtsGameFunctionService.Engine;
using DraughtsGameFunctionService.Helpers;
using DraughtsGameFunctionService.Instance;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using static DraughtsGameFunctionTests.EngineBoards;

namespace DraughtsGameFunctionTests;

/// <summary>
/// Plan section 7.2, tests 15, 17 and 23: the computer player at each difficulty.
/// </summary>
[TestClass]
public class ComputerPlayerTests
{
    // Test 15 and spec section 6.2, example 16: Normal varies its play from the starting board, because many moves
    // share the top score and the tie breaker chooses one at random. A seeded Random keeps the test repeatable.
    [TestMethod]
    public void GetNextMove_NormalFromStartTwentyTimes_ReturnsAtLeastTwoDifferentMoves()
    {
        // Arrange
        var player = NewPlayer(new SearchOptions(), TimeProvider.System, new Random(15));
        var board = StartingRows();
        var returned = new HashSet<string>();

        // Act
        for (var request = 0; request < 20; request++)
        {
            returned.Add(Text(player.GetNextMove(board, player: 2, Difficulty.Normal)!.Move));
        }

        // Assert
        Assert.IsGreaterThanOrEqualTo(2, returned.Count);
    }

    // Test 17: Normal stops at its maximum depth of 6. The fake clock does not move, so only the maximum depth can stop
    // the search. The starting board is neither won nor lost, so no forced win stops it sooner.
    [TestMethod]
    public void GetNextMove_NormalWithTimeToSpare_StopsAtDepthSix()
    {
        // Arrange
        var player = NewPlayer(new SearchOptions(), new FakeTimeProvider(), new Random(17));

        // Act
        var move = player.GetNextMove(StartingRows(), player: 2, Difficulty.Normal);

        // Assert
        Assert.AreEqual(6, move!.CompletedDepth);
    }

    // Test 17: Hard has no maximum depth, so it searches past 6 until its time limit. The fake clock moves on 1 ms
    // at each reading, so the Hard limit of 1,000 ms passes after a fixed amount of work on every machine.
    [TestMethod]
    public void GetNextMove_Hard_SearchesPastDepthSix()
    {
        // Arrange
        var clock = new FakeTimeProvider { AutoAdvanceAmount = TimeSpan.FromMilliseconds(1) };
        var player = NewPlayer(new SearchOptions(), clock, new Random(17));

        // Act
        var move = player.GetNextMove(StartingRows(), player: 2, Difficulty.Hard);

        // Assert
        Assert.IsGreaterThan(6, move!.CompletedDepth);
    }

    // Test 23 and spec section 6.1.5, item 4: Easy always takes a capture when one exists, and chooses between the
    // captures at random. Player 2 has two quiet moves and two captures: (2,1) takes (3,2), and (2,5) takes (3,6).
    [TestMethod]
    public void GetNextMove_EasyWithCapturesAvailable_AlwaysCaptures()
    {
        // Arrange
        var board = Rows(Create(
            (2, 1, Board.Player2Man), (2, 5, Board.Player2Man),
            (3, 2, Board.Player1Man), (3, 6, Board.Player1Man)));
        var player = NewPlayer(new SearchOptions(), TimeProvider.System, new Random(23));
        var returned = new HashSet<string>();

        // Act
        for (var request = 0; request < 30; request++)
        {
            var move = player.GetNextMove(board, player: 2, Difficulty.Easy)!;
            returned.Add(Text(move.Move));

            Assert.AreEqual(0, move.CompletedDepth);
        }

        // Assert
        Assert.HasCount(4, FindMove.FindAvailableMoves(board, 2), "Player 2 should have two quiet moves and two captures.");
        CollectionAssert.AreEquivalent(new[] { "(2,1)->(4,3) x(3,2)", "(2,5)->(4,7) x(3,6)" }, returned.ToList());
    }

    // Spec section 6.1.5, item 4: with no capture, Easy chooses any legal move at random.
    [TestMethod]
    public void GetNextMove_EasyWithNoCapture_ChoosesBetweenLegalMoves()
    {
        // Arrange
        var board = StartingRows();
        var legalMoves = FindMove.FindAvailableMoves(board, 2).Select(Text).ToList();
        var player = NewPlayer(new SearchOptions(), TimeProvider.System, new Random(23));
        var returned = new HashSet<string>();

        // Act
        for (var request = 0; request < 30; request++)
        {
            returned.Add(Text(player.GetNextMove(board, player: 2, Difficulty.Easy)!.Move));
        }

        // Assert
        Assert.IsGreaterThanOrEqualTo(2, returned.Count);
        CollectionAssert.IsSubsetOf(returned.ToList(), legalMoves);
    }

    // Normal searches with the material evaluation and the Normal settings: the same move and depth as a search
    // with MaterialEvaluator at those settings. The position is one where the two evaluations choose differently.
    [TestMethod]
    public void GetNextMove_Normal_SearchesWithMaterialEvaluationAndNormalSettings()
    {
        // Arrange
        var settings = new SearchOptions { NormalMaxDepth = 3 };
        var limits = new SearchLimits(3, TimeSpan.FromMilliseconds(settings.NormalTimeLimitMs));
        var (board, material, _) = PositionWhereEvaluationsDiffer(limits, () => new FakeTimeProvider());
        var player = NewPlayer(settings, new FakeTimeProvider(), new Random(16));

        // Act
        var move = player.GetNextMove(Rows(board), player: 2, Difficulty.Normal)!;

        // Assert
        Assert.AreEqual(Text(material.Move), Text(move.Move));
        Assert.AreEqual(3, move.CompletedDepth);
    }

    // Hard searches with the positional evaluation, no maximum depth and the Hard time limit: the same move and depth
    // as a search with PositionalEvaluator at those settings, on clocks that move on 1 ms at each reading.
    [TestMethod]
    public void GetNextMove_Hard_SearchesWithPositionalEvaluationAndHardTimeLimit()
    {
        // Arrange
        var settings = new SearchOptions { HardTimeLimitMs = 50 };
        var limits = new SearchLimits(MaxDepth: null, TimeSpan.FromMilliseconds(50));
        var (board, _, positional) = PositionWhereEvaluationsDiffer(limits, AdvancingClock);
        var player = NewPlayer(settings, AdvancingClock(), new Random(16));

        // Act
        var move = player.GetNextMove(Rows(board), player: 2, Difficulty.Hard)!;

        // Assert
        Assert.AreEqual(Text(positional.Move), Text(move.Move));
        Assert.AreEqual(positional.CompletedDepth, move.CompletedDepth);
    }

    // Spec section 6.1.5, item 3: no legal move gives no move, and the caller returns the error.
    [TestMethod]
    [DataRow(Difficulty.Easy)]
    [DataRow(Difficulty.Normal)]
    [DataRow(Difficulty.Hard)]
    public void GetNextMove_NoLegalMove_ReturnsNull(Difficulty difficulty)
    {
        // Arrange
        var board = Rows(Create((6, 1, Board.Player1Man)));
        var player = NewPlayer(new SearchOptions(), TimeProvider.System, new Random(3));

        // Act
        var move = player.GetNextMove(board, player: 2, difficulty);

        // Assert
        Assert.IsNull(move);
    }

    // Spec section 6.1.5, item 7: one legal move is returned at once, with no search.
    [TestMethod]
    public void GetNextMove_HardWithOneLegalMove_ReturnsItWithoutSearching()
    {
        // Arrange
        var board = Rows(Create((5, 0, Board.Player2Man), (2, 7, Board.Player1Man)));
        var player = NewPlayer(new SearchOptions(), TimeProvider.System, new Random(18));

        // Act
        var move = player.GetNextMove(board, player: 2, Difficulty.Hard)!;

        // Assert
        Assert.AreEqual("(5,0)->(6,1)", Text(move.Move));
        Assert.AreEqual(0, move.CompletedDepth);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(3)]
    public void GetNextMove_PlayerOutOfRange_Throws(int playerNumber)
    {
        // Arrange
        var player = NewPlayer(new SearchOptions(), TimeProvider.System, new Random(1));

        // Act and assert
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => player.GetNextMove(StartingRows(), playerNumber, Difficulty.Easy));
    }

    [TestMethod]
    public void GetNextMove_BoardNotEightByEight_Throws()
    {
        // Arrange
        var player = NewPlayer(new SearchOptions(), TimeProvider.System, new Random(1));

        // Act and assert
        Assert.ThrowsExactly<ArgumentException>(() => player.GetNextMove(new long[7, 8], 2, Difficulty.Easy));
    }

    [TestMethod]
    public void GetNextMove_UnknownDifficulty_Throws()
    {
        // Arrange
        var player = NewPlayer(new SearchOptions(), TimeProvider.System, new Random(1));

        // Act and assert
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => player.GetNextMove(StartingRows(), 2, (Difficulty)7));
    }

    private static ComputerPlayer NewPlayer(SearchOptions settings, TimeProvider clock, Random random) =>
        new(Options.Create(settings), clock, random);

    private static FakeTimeProvider AdvancingClock() => new() { AutoAdvanceAmount = TimeSpan.FromMilliseconds(1) };

    // Finds the first position from random games where a search for player 2 with each evaluation, at these limits,
    // chooses a different move. Each search gets its own new clock and the same seeded Random as the player.
    private static (int[] Board, SearchResult Material, SearchResult Positional) PositionWhereEvaluationsDiffer(
        SearchLimits limits, Func<TimeProvider> newClock)
    {
        foreach (var board in PlayedPositions(seed: 16, gameCount: 4, pliesBetweenPositions: 4))
        {
            if (Generate(board, player: 2).Count < 2)
            {
                continue;
            }

            var material = new GameTreeSearch(new MaterialEvaluator(), newClock(), new Random(16)).Search(board, 2, limits);
            var positional = new GameTreeSearch(new PositionalEvaluator(), newClock(), new Random(16)).Search(board, 2, limits);

            if (material.Move != positional.Move)
            {
                return (board, material, positional);
            }
        }

        throw new InvalidOperationException("No position from the random games has the evaluations choosing differently.");
    }

    private static long[,] Rows(int[] board)
    {
        var rows = new long[Board.Size, Board.Size];
        for (var square = 0; square < Board.SquareCount; square++)
        {
            rows[Board.Row(square), Board.Column(square)] = board[square];
        }

        return rows;
    }

    private static string Text(Move move) => Text(FindMove.ToNextMove(move));

    // For example "(2,1)->(4,3) x(3,2)".
    private static string Text(NextMove move)
    {
        var text = $"({move.CurrentHeight},{move.CurrentWidth})->({move.NextHeight},{move.NextWidth})";
        var takes = string.Concat(move.Takes.Select(piece => $"({piece.Height},{piece.Width})"));

        return takes.Length == 0 ? text : $"{text} x{takes}";
    }
}
