using DraughtsGameFunctionService.Engine;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using static DraughtsGameFunctionTests.EngineBoards;

namespace DraughtsGameFunctionTests;

/// <summary>
/// Plan section 7.2, tests 8 to 11. The positions are the worked examples in spec section 6.2.
/// </summary>
[TestClass]
public class MoveGeneratorTests
{
    // Test 8, example 3: a king that can capture both down-right and up-right gets both (defect 1).
    [TestMethod]
    public void Generate_KingWithTwoCaptures_ReturnsBoth()
    {
        // Arrange
        var board = Create((3, 2, Board.Player2King), (4, 3, Board.Player1Man), (2, 3, Board.Player1Man));

        // Act
        var moves = Generate(board, player: 2);

        // Assert
        AssertMoves(moves,
            Move((3, 2), (2, 1)),
            Move((3, 2), (4, 1)),
            Move((3, 2), (5, 4), (4, 3)),
            Move((3, 2), (1, 4), (2, 3)));
    }

    // Test 8, example 4: a man can stop after any jump of a chain.
    [TestMethod]
    public void Generate_ManWithTwoJumpChain_ReturnsEachStop()
    {
        // Arrange
        var board = Create((6, 1, Board.Player1Man), (5, 2, Board.Player2Man), (3, 4, Board.Player2Man));

        // Act
        var moves = Generate(board, player: 1);

        // Assert
        AssertMoves(moves,
            Move((6, 1), (5, 0)),
            Move((6, 1), (4, 3), (5, 2)),
            Move((6, 1), (2, 5), (5, 2), (3, 4)));
    }

    // Test 8, example 17: a man cannot capture a king.
    [TestMethod]
    public void Generate_ManNextToKing_DoesNotCaptureIt()
    {
        // Arrange
        var board = Create((6, 1, Board.Player1Man), (5, 2, Board.Player2King));

        // Act
        var moves = Generate(board, player: 1);

        // Assert
        AssertMoves(moves, Move((6, 1), (5, 0)));
    }

    // Test 8, example 18: a king can capture a king, and moves in all four directions.
    [TestMethod]
    public void Generate_KingNextToKing_CapturesIt()
    {
        // Arrange
        var board = Create((6, 1, Board.Player1King), (5, 2, Board.Player2King));

        // Act
        var moves = Generate(board, player: 1);

        // Assert
        AssertMoves(moves,
            Move((6, 1), (5, 0)),
            Move((6, 1), (7, 0)),
            Move((6, 1), (7, 2)),
            Move((6, 1), (4, 3), (5, 2)));
    }

    // Test 9, example 19: the chain can end on the start square, and the loop in both directions is one move (defect 10).
    [TestMethod]
    public void Generate_KingLoopBackToStart_ReturnsOneMoveEndingOnStart()
    {
        // Arrange
        var board = Create(
            (1, 2, Board.Player2King),
            (2, 3, Board.Player1Man), (4, 3, Board.Player1Man), (4, 1, Board.Player1Man), (2, 1, Board.Player1Man));

        // Act
        var moves = Generate(board, player: 2);

        // Assert
        AssertMoves(moves,
            Move((1, 2), (0, 1)),
            Move((1, 2), (0, 3)),
            Move((1, 2), (3, 4), (2, 3)),
            Move((1, 2), (5, 2), (2, 3), (4, 3)),
            Move((1, 2), (3, 0), (2, 3), (4, 3), (4, 1)),
            Move((1, 2), (3, 0), (2, 1)),
            Move((1, 2), (5, 2), (2, 1), (4, 1)),
            Move((1, 2), (3, 4), (2, 1), (4, 1), (4, 3)),
            Move((1, 2), (1, 2), (2, 3), (4, 3), (4, 1), (2, 1)));
    }

    // Test 9, example 20: a loop that does not pass the start square ends, with no piece captured twice (defect 9).
    [TestMethod]
    public void Generate_KingLoopAwayFromStart_EndsWithoutCapturingTwice()
    {
        // Arrange
        var board = Create(
            (7, 4, Board.Player2King),
            (6, 3, Board.Player1Man), (4, 1, Board.Player1Man), (4, 3, Board.Player1Man), (2, 1, Board.Player1Man), (2, 3, Board.Player1Man));

        // Act
        var moves = Generate(board, player: 2);

        // Assert
        AssertMoves(moves,
            Move((7, 4), (6, 5)),
            Move((7, 4), (5, 2), (6, 3)),
            Move((7, 4), (3, 0), (6, 3), (4, 1)),
            Move((7, 4), (1, 2), (6, 3), (4, 1), (2, 1)),
            Move((7, 4), (3, 4), (6, 3), (4, 1), (2, 1), (2, 3)),
            Move((7, 4), (3, 4), (6, 3), (4, 3)),
            Move((7, 4), (1, 2), (6, 3), (4, 3), (2, 3)),
            Move((7, 4), (3, 0), (6, 3), (4, 3), (2, 3), (2, 1)),
            Move((7, 4), (5, 2), (6, 3), (4, 1), (4, 3), (2, 1), (2, 3)));
    }

    // Test 9: a move that ends where it began must not lose the king (spec section 6.5, item 7).
    [TestMethod]
    public void Apply_MoveEndsOnStartSquare_KeepsKingAndRemovesCaptures()
    {
        // Arrange
        var board = Create(
            (1, 2, Board.Player2King),
            (2, 3, Board.Player1Man), (4, 3, Board.Player1Man), (4, 1, Board.Player1Man), (2, 1, Board.Player1Man));
        var loop = Move((1, 2), (1, 2), (2, 3), (4, 3), (4, 1), (2, 1));
        var expected = Create((1, 2, Board.Player2King));

        // Act
        MoveGenerator.Apply(board, loop);

        // Assert
        CollectionAssert.AreEqual(expected, board);
    }

    // Test 8: a man captures forward only, even when the square behind the opponent is empty (spec 6.1.3, item 3).
    [TestMethod]
    public void Generate_OpponentManBehindMan_DoesNotCaptureBackwards()
    {
        // Arrange
        var board = Create((4, 3, Board.Player1Man), (5, 4, Board.Player2Man));

        // Act
        var moves = Generate(board, player: 1);

        // Assert
        AssertMoves(moves,
            Move((4, 3), (3, 2)),
            Move((4, 3), (3, 4)));
    }

    // Any player other than 1 or 2 would otherwise be read as player 2.
    [TestMethod]
    [DataRow(0)]
    [DataRow(3)]
    public void Generate_PlayerNotOneOrTwo_Throws(int player)
    {
        // Arrange
        var board = Board.FromRows(StartingRows());

        // Act and assert
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => Generate(board, player));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => MoveGenerator.HasMove(board, player));
    }

    // Test 10: a man that reaches the far row stops there, even when it could jump again as a king.
    [TestMethod]
    public void Generate_ManCapturesOntoFarRow_ChainEnds()
    {
        // Arrange
        // After landing on (0,3), a king could jump (1,2) to (2,1). A man that has just been promoted must not.
        var board = Create((2, 5, Board.Player1Man), (1, 4, Board.Player2Man), (1, 2, Board.Player2Man));

        // Act
        var moves = Generate(board, player: 1);

        // Assert
        AssertMoves(moves,
            Move((2, 5), (1, 6)),
            Move((2, 5), (0, 3), (1, 4)));
    }

    // Test 10: Apply promotes a man on its far row.
    [TestMethod]
    [DataRow(Board.Player1Man, 1, 2, 0, 1, Board.Player1King)]
    [DataRow(Board.Player2Man, 6, 1, 7, 0, Board.Player2King)]
    public void Apply_ManReachesFarRow_BecomesKing(int man, int fromRow, int fromColumn, int toRow, int toColumn, int king)
    {
        // Arrange
        var board = Create((fromRow, fromColumn, man));

        // Act
        MoveGenerator.Apply(board, Move((fromRow, fromColumn), (toRow, toColumn)));

        // Assert
        Assert.AreEqual(king, board[Board.Index(toRow, toColumn)]);
        Assert.AreEqual(Board.Empty, board[Board.Index(fromRow, fromColumn)]);
    }

    // Test 10: a king on the far row stays the same king.
    [TestMethod]
    public void Apply_KingMovesOnFarRow_StaysKing()
    {
        // Arrange
        var board = Create((1, 2, Board.Player1King));

        // Act
        MoveGenerator.Apply(board, Move((1, 2), (0, 1)));

        // Assert
        Assert.AreEqual(Board.Player1King, board[Board.Index(0, 1)]);
    }

    // Test 11: a highlighted square (value 6) is empty, for moves and for landings.
    [TestMethod]
    public void Generate_HighlightedSquares_BehaveAsEmpty()
    {
        // Arrange
        var board = Create(
            (6, 1, Board.Player1Man), (5, 2, Board.Player2Man), (3, 4, Board.Player2Man),
            (5, 0, Board.Highlighted), (4, 3, Board.Highlighted), (2, 5, Board.Highlighted));

        // Act
        var moves = Generate(board, player: 1);

        // Assert
        AssertMoves(moves,
            Move((6, 1), (5, 0)),
            Move((6, 1), (4, 3), (5, 2)),
            Move((6, 1), (2, 5), (5, 2), (3, 4)));
    }

    // Test 11: HasMove also treats a highlighted square as empty.
    [TestMethod]
    public void HasMove_OnlyMoveIsToHighlightedSquare_ReturnsTrue()
    {
        // Arrange
        var board = Create((7, 0, Board.Player1Man), (6, 1, Board.Highlighted));

        // Act
        var hasMove = MoveGenerator.HasMove(board, player: 1);

        // Assert
        Assert.IsTrue(hasMove);
    }

    [TestMethod]
    public void HasMove_StartingBoard_ReturnsTrueForBothPlayers()
    {
        // Arrange
        var board = Board.FromRows(StartingRows());

        // Act
        var player1HasMove = MoveGenerator.HasMove(board, player: 1);
        var player2HasMove = MoveGenerator.HasMove(board, player: 2);

        // Assert
        Assert.IsTrue(player1HasMove);
        Assert.IsTrue(player2HasMove);
    }

    [TestMethod]
    public void HasMove_ManBlockedByKing_ReturnsFalse()
    {
        // Arrange
        // The man's only forward square holds a king, which a man cannot capture, though the landing square is empty.
        var board = Create((7, 0, Board.Player1Man), (6, 1, Board.Player2King));

        // Act
        var hasMove = MoveGenerator.HasMove(board, player: 1);

        // Assert
        Assert.IsFalse(hasMove);
    }

    [TestMethod]
    public void Generate_StartingBoard_ReturnsSevenMovesForEachPlayer()
    {
        // Arrange
        var board = Board.FromRows(StartingRows());

        // Act
        var player1Moves = Generate(board, player: 1);
        var player2Moves = Generate(board, player: 2);

        // Assert
        Assert.HasCount(7, player1Moves);
        Assert.HasCount(7, player2Moves);
    }

    // A value outside 0 to 6 blocks its square until request validation rejects it (spec 6.1.2, rule 4).
    // A plain cast to int would turn 2^32 + 1 into 1, a player 1 man.
    [TestMethod]
    [DataRow(4294967297L)]
    [DataRow(7L)]
    [DataRow(-1L)]
    public void FromRows_ValueOutsideZeroToSix_BlocksTheSquare(long value)
    {
        // Arrange
        var rows = StartingRows();
        rows[4, 1] = value;

        // Act
        var board = Board.FromRows(rows);

        // Assert
        Assert.AreEqual(0, board[Board.Index(4, 1)]);
    }

    // HasMove must agree with Generate, because the no-move error depends on it. Positions come from games of
    // random legal moves with a fixed seed, so every run checks the same positions.
    [TestMethod]
    public void HasMove_PositionsFromRandomGames_AgreesWithGenerate()
    {
        // Arrange
        var random = new Random(20261109);
        var moves = new List<Move>();
        var checkedCount = 0;

        for (var game = 0; game < 60; game++)
        {
            var board = Board.FromRows(StartingRows());
            var player = 1;

            for (var ply = 0; ply < 120; ply++)
            {
                foreach (var side in new[] { 3 - player, player })
                {
                    // Act
                    MoveGenerator.Generate(board, side, moves);
                    var hasMove = MoveGenerator.HasMove(board, side);

                    // Assert
                    Assert.AreEqual(moves.Count > 0, hasMove, $"Player {side}, board {string.Concat(board)}");
                    checkedCount++;
                }

                // The moves list now holds the moves of the player to move.
                if (moves.Count == 0)
                {
                    break;
                }

                MoveGenerator.Apply(board, moves[random.Next(moves.Count)]);
                player = 3 - player;
            }
        }

        Assert.IsGreaterThan(5000, checkedCount);
    }

    private static void AssertMoves(List<Move> actual, params Move[] expected)
    {
        var actualText = string.Join(", ", actual.Select(Describe).Order());
        var expectedText = string.Join(", ", expected.Select(Describe).Order());

        Assert.AreEqual(expectedText, actualText);
        Assert.HasCount(expected.Length, actual, "A move is listed more than once.");
    }
}
