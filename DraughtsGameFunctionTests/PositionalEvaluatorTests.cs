using DraughtsGameFunctionService.Engine;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using static DraughtsGameFunctionTests.EngineBoards;

namespace DraughtsGameFunctionTests;

/// <summary>
/// Plan section 7.2, test 32: the Hard evaluation, with one test for each feature in plan section 3.5.
/// Every score is for player 1.
/// </summary>
[TestClass]
public class PositionalEvaluatorTests
{
    private static readonly PositionalEvaluator Evaluator = new();

    // A man is 100 and a king 200. One king against two men is level material, so there is no trade bonus.
    // Each man is one row up from its back row, for 3. So 200 - 2 x (100 + 3) = -6.
    [TestMethod]
    public void Evaluate_KingAgainstTwoMen_ScoresKingAsTwoMen()
    {
        // Arrange
        var board = Create((7, 2, Board.Player1King), (1, 0, Board.Player2Man), (1, 2, Board.Player2Man));

        // Act
        var score = Evaluator.Evaluate(board, player: 1);

        // Assert
        Assert.AreEqual(-6, score);
    }

    // Advancement: 3 for each row a man has moved up from its back row. (5,0) is one row further up than (6,1).
    [TestMethod]
    public void Evaluate_ManOneRowFurtherUp_ScoresAdvancement()
    {
        // Arrange
        var nearer = Create((6, 1, Board.Player1Man), (0, 7, Board.Player2Man));
        var further = Create((5, 0, Board.Player1Man), (0, 7, Board.Player2Man));

        // Act
        var difference = Evaluator.Evaluate(further, player: 1) - Evaluator.Evaluate(nearer, player: 1);

        // Assert
        Assert.AreEqual(3, difference);
    }

    // Back-row guard: a man on its own back row scores 6. One row up, it scores 3 for advancement instead.
    [TestMethod]
    public void Evaluate_ManOnBackRow_ScoresGuard()
    {
        // Arrange
        var onBackRow = Create((7, 0, Board.Player1Man), (0, 7, Board.Player2Man));
        var oneRowUp = Create((6, 1, Board.Player1Man), (0, 7, Board.Player2Man));

        // Act
        var difference = Evaluator.Evaluate(onBackRow, player: 1) - Evaluator.Evaluate(oneRowUp, player: 1);

        // Assert
        Assert.AreEqual(6 - 3, difference);
    }

    // Centre: 2 for a man or a king on rows 3 and 4, columns 2 to 5. On row 4, (4,3) is a centre square and (4,1)
    // is not, so a man has the same advancement on both.
    [TestMethod]
    [DataRow(Board.Player1Man, DisplayName = "Man")]
    [DataRow(Board.Player1King, DisplayName = "King")]
    public void Evaluate_PieceOnCentreSquare_ScoresCentre(int piece)
    {
        // Arrange
        var inCentre = Create((4, 3, piece), (0, 1, Board.Player2King));
        var offCentre = Create((4, 1, piece), (0, 1, Board.Player2King));

        // Act
        var difference = Evaluator.Evaluate(inCentre, player: 1) - Evaluator.Evaluate(offCentre, player: 1);

        // Assert
        Assert.AreEqual(2, difference);
    }

    // Trading when ahead: player 1 is a king ahead on both boards, and the second board has one more king on each
    // side. The player who is ahead gets 2 for each piece missing from the starting 24, so the even trade adds 4.
    // After it, 3 pieces are left: 2 x 200 - 200 + 2 x 21 = 242.
    [TestMethod]
    public void Evaluate_EvenTradeWhenAhead_ScoresTradeBonus()
    {
        // Arrange
        var beforeTrade = Create(
            (7, 0, Board.Player1King), (7, 2, Board.Player1King), (7, 4, Board.Player1King),
            (0, 1, Board.Player2King), (0, 3, Board.Player2King));
        var afterTrade = Create((7, 0, Board.Player1King), (7, 2, Board.Player1King), (0, 1, Board.Player2King));

        // Act
        var before = Evaluator.Evaluate(beforeTrade, player: 1);
        var after = Evaluator.Evaluate(afterTrade, player: 1);

        // Assert
        Assert.AreEqual(4, after - before);
        Assert.AreEqual(242, after);
    }

    // The score for one player is the negative of the other's. The positions come from random games, and include
    // material ahead and behind, men and kings.
    [TestMethod]
    public void Evaluate_AnyPosition_OpponentsScoreIsTheNegative()
    {
        // Arrange
        var positions = PlayedPositions(seed: 32, gameCount: 6, pliesBetweenPositions: 5).ToList();
        var mismatches = new List<string>();

        foreach (var board in positions)
        {
            // Act
            var player1Score = Evaluator.Evaluate(board, player: 1);
            var player2Score = Evaluator.Evaluate(board, player: 2);

            // Assert
            if (player1Score != -player2Score)
            {
                mismatches.Add($"Player 1 {player1Score}, player 2 {player2Score}, board {string.Concat(board)}");
            }
        }

        Assert.IsEmpty(mismatches, string.Join(System.Environment.NewLine, mismatches.Take(5)));
        Assert.IsTrue(positions.Any(board => Evaluator.Evaluate(board, player: 1) != 0), "Every position scored 0.");
    }

    // Moved from the old Hard service's EvaluateTest (plan section 7.1): the starting board scores 0, as the
    // old Hard evaluation did.
    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    public void Evaluate_StartingBoard_ScoresZero(int player)
    {
        // Arrange
        var board = Board.FromRows(StartingRows());

        // Act
        var score = Evaluator.Evaluate(board, player);

        // Assert
        Assert.AreEqual(0, score);
    }

    // Plan section 3.5: quiescence is for Hard only.
    [TestMethod]
    public void UsesQuiescence_HardEvaluation_IsTrue()
    {
        // Act and assert
        Assert.IsTrue(Evaluator.UsesQuiescence);
    }
}
