using DraughtsGameFunctionModels.Enums;
using DraughtsGameFunctionModels.Options;
using DraughtsGameFunctionSelfPlay;
using DraughtsGameFunctionService.Engine;
using DraughtsGameFunctionService.Instance;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using static DraughtsGameFunctionTests.EngineBoards;

namespace DraughtsGameFunctionTests;

/// <summary>
/// Plan section 7.2, test 24, and the rules for computer-against-computer games in spec section 6.4, item 3.
/// The searches run at a fixed depth with no randomness (spec section 6.1.5, item 9), so the games are quick and
/// repeatable.
/// </summary>
[TestClass]
public class SelfPlayTests
{
    // Test 24: one game at a small depth runs to an end, by a rule in spec section 6.4, item 3.
    [TestMethod]
    public void Play_FixedDepthSearchesFromStart_RunsToAnEnd()
    {
        // Arrange
        var start = Board.FromRows(StartingRows());

        // Act
        var result = SelfPlayGame.Play(start, FixedDepth(new PositionalEvaluator(), 2), FixedDepth(new MaterialEvaluator(), 2), new Random(24));

        // Assert
        Assert.IsGreaterThan(SelfPlayGame.OpeningMoves, result.Moves);
        Assert.IsLessThanOrEqualTo(SelfPlayGame.MovesForDraw, result.Moves);
        Assert.IsFalse(string.IsNullOrEmpty(result.Reason));
    }

    // Spec section 2.1, item 6: a move that leaves the opponent with no moves wins. With no opening moves, player 1's
    // king takes player 2's only piece with the first move.
    [TestMethod]
    public void Play_MoveLeavesOpponentWithNoMoves_ThatPlayerWins()
    {
        // Arrange
        var start = Create((4, 3, Board.Player1King), (3, 2, Board.Player2Man));

        // Act
        var result = SelfPlayGame.Play(start, CaptureIfPossible, CaptureIfPossible, new Random(1), openingMoves: 0);

        // Assert
        Assert.AreEqual(GameOutcome.Player1Wins, result.Outcome, result.Reason);
        Assert.AreEqual("Player 2 cannot move.", result.Reason);
        Assert.AreEqual(1, result.Moves);
    }

    // Spec section 6.4, item 3: a game is a draw after 50 moves in a row with no capture. Both players only ever make
    // moves that capture nothing, on a board with one king each, from the first move.
    [TestMethod]
    public void Play_FiftyMovesWithoutACapture_IsADraw()
    {
        // Arrange
        var start = Create((7, 0, Board.Player1King), (0, 7, Board.Player2King));

        // Act
        var result = SelfPlayGame.Play(start, NeverCapture, NeverCapture, new Random(50), openingMoves: 0);

        // Assert
        Assert.AreEqual(GameOutcome.Draw, result.Outcome);
        Assert.AreEqual(SelfPlayGame.MovesWithoutCaptureForDraw, result.Moves);
        Assert.AreEqual("50 moves without a capture.", result.Reason);
    }

    // A player that returns a move the rules do not allow stops the game, so it cannot spoil the scores.
    [TestMethod]
    public void Play_PlayerChoosesIllegalMove_Throws()
    {
        // Arrange
        var start = Board.FromRows(StartingRows());
        ChooseMove illegal = (board, player) => new Move(0, 1, 0);

        // Act and assert
        Assert.ThrowsExactly<InvalidOperationException>(() => SelfPlayGame.Play(start, illegal, illegal, new Random(1)));
    }

    // Spec section 6.4, item 3: each side moves first in half of the games. The players record which player number
    // they are asked to play, and player 1 always moves first.
    [TestMethod]
    public void Run_TwoGames_EachSideMovesFirstOnceAndScoresAddUp()
    {
        // Arrange
        var start = Board.FromRows(StartingRows());
        var strongerSides = new List<int>();
        var weakerSides = new List<int>();
        var reported = new List<string>();
        var stronger = Recording(FixedDepth(new MaterialEvaluator(), 2), strongerSides);
        var weaker = Recording(FixedDepth(new MaterialEvaluator(), 1), weakerSides);

        // Act
        var result = Pairing.Run("Stronger", stronger, "Weaker", weaker, start, games: 2, new Random(7), reported.Add);

        // Assert
        Assert.AreEqual(2, result.StrongerWins + result.Draws + result.WeakerWins);
        Assert.AreEqual(result.StrongerWins + result.Draws / 2.0, result.StrongerScore);
        CollectionAssert.AreEquivalent(new[] { 1, 2 }, strongerSides.Distinct().ToList());
        CollectionAssert.AreEquivalent(new[] { 1, 2 }, weakerSides.Distinct().ToList());
        Assert.HasCount(2, reported);
        StringAssert.StartsWith(reported[0], "Game 1/2, Stronger first:");
        StringAssert.StartsWith(reported[1], "Game 2/2, Weaker first:");
    }

    // The tool plays the levels through the same ComputerPlayer as the API. A game between Easy and Normal, with short
    // time limits, checks that each move converts to a legal engine move.
    [TestMethod]
    public void For_EasyAgainstNormal_PlaysLegalMovesToAnEnd()
    {
        // Arrange
        var settings = new SearchOptions { NormalTimeLimitMs = 5, HardTimeLimitMs = 5 };
        var computerPlayer = new ComputerPlayer(Options.Create(settings), TimeProvider.System, new Random(20));
        var start = Board.FromRows(StartingRows());

        // Act
        var result = SelfPlayGame.Play(
            start, LevelPlayer.For(computerPlayer, Difficulty.Easy), LevelPlayer.For(computerPlayer, Difficulty.Normal), new Random(20));

        // Assert
        Assert.IsLessThanOrEqualTo(SelfPlayGame.MovesForDraw, result.Moves);
    }

    [TestMethod]
    public void Parse_NoOptions_UsesDefaults()
    {
        // Act
        var (options, error) = SelfPlayOptions.Parse([]);

        // Assert
        Assert.IsNull(error);
        Assert.AreEqual(new SelfPlayOptions(Games: 200, TimeScale: 1.0), options);
    }

    // Plan section 7.4 and T20's Verify command.
    [TestMethod]
    public void Parse_GamesAndTimeScale_ReadsBoth()
    {
        // Act
        var (options, error) = SelfPlayOptions.Parse(["--games", "2", "--time-scale", "0.1"]);

        // Assert
        Assert.IsNull(error);
        Assert.AreEqual(new SelfPlayOptions(Games: 2, TimeScale: 0.1), options);
    }

    [TestMethod]
    [DataRow("--games", "3", DisplayName = "Odd number of games")]
    [DataRow("--games", "0", DisplayName = "No games")]
    [DataRow("--games", "many", DisplayName = "Games not a number")]
    [DataRow("--time-scale", "0", DisplayName = "Time scale 0")]
    [DataRow("--time-scale", "-1", DisplayName = "Negative time scale")]
    [DataRow("--time-scale", "fast", DisplayName = "Time scale not a number")]
    [DataRow("--depth", "6", DisplayName = "Unknown option")]
    public void Parse_BadOption_ReturnsError(string name, string value)
    {
        // Act
        var (options, error) = SelfPlayOptions.Parse([name, value]);

        // Assert
        Assert.IsNull(options);
        Assert.IsNotNull(error);
    }

    [TestMethod]
    public void Parse_OptionWithNoValue_ReturnsError()
    {
        // Act
        var (options, error) = SelfPlayOptions.Parse(["--games"]);

        // Assert
        Assert.IsNull(options);
        Assert.IsNotNull(error);
    }

    // Plan section 7.4: the time scale multiplies the production time limits, and leaves Normal's depth alone.
    [TestMethod]
    public void ScaledSearchOptions_TimeScale_MultipliesTimeLimits()
    {
        // Arrange
        var options = new SelfPlayOptions(Games: 2, TimeScale: 0.35);

        // Act
        var settings = options.ScaledSearchOptions();

        // Assert
        Assert.AreEqual(6, settings.NormalMaxDepth);
        Assert.AreEqual(175, settings.NormalTimeLimitMs);
        Assert.AreEqual(350, settings.HardTimeLimitMs);
    }

    private static ChooseMove FixedDepth(IEvaluator evaluator, int depth)
    {
        var search = new GameTreeSearch(evaluator, TimeProvider.System, tieBreaker: null);

        return (board, player) => search.Search(board, player, new SearchLimits(depth, TimeLimit: null)).Move;
    }

    private static Move CaptureIfPossible(int[] board, int player)
    {
        var moves = Generate(board, player);

        return moves.FirstOrDefault(move => move.Captured != 0, moves[0]);
    }

    private static Move NeverCapture(int[] board, int player) =>
        Generate(board, player).First(move => move.Captured == 0);

    private static ChooseMove Recording(ChooseMove inner, List<int> sides) => (board, player) =>
    {
        sides.Add(player);

        return inner(board, player);
    };
}
