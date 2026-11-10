using DraughtsGameFunctionService.Engine;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using static DraughtsGameFunctionTests.EngineBoards;

namespace DraughtsGameFunctionTests;

/// <summary>
/// Plan section 7.2, tests 13, 19 and 20: the alpha-beta search at a fixed depth.
/// </summary>
[TestClass]
public class GameTreeSearchTests
{
    private const int Win = GameTreeSearch.WinScore;

    private static readonly MaterialEvaluator Evaluator = new();

    // Test 13: the search finds the same move and score as plain minimax, for both players, at depths 1 to 4.
    // The positions come from games of random legal moves with a fixed seed, so every run uses the same positions.
    [TestMethod]
    public void Search_FixedDepth_MatchesPlainMinimax()
    {
        // Arrange
        var positions = PlayedPositions(seed: 20261110, gameCount: 12, pliesBetweenPositions: 6).ToList();
        var mismatches = new List<string>();
        var compared = 0;

        foreach (var board in positions)
        {
            foreach (var player in new[] { 1, 2 })
            {
                if (!MoveGenerator.HasMove(board, player))
                {
                    continue;
                }

                for (var depth = 1; depth <= 4; depth++)
                {
                    // Act
                    var expected = PlainMinimax.BestMove(board, player, depth, Evaluator);
                    var actual = NewSearch().Search(board, player, new SearchLimits(depth, null));
                    compared++;

                    // Assert
                    if (actual.Move != expected.Move || actual.Score != expected.Score)
                    {
                        mismatches.Add($"Player {player}, depth {depth}: search {Describe(actual.Move)} = {actual.Score}, "
                            + $"minimax {Describe(expected.Move)} = {expected.Score}, board {Text(board)}");
                    }
                }
            }
        }

        Assert.IsEmpty(mismatches, string.Join(Environment.NewLine, mismatches.Take(5)));
        Assert.IsGreaterThan(200, compared, "Too few searches were compared to trust the result.");
    }

    // Test 19: a win in 1 is chosen over a win in 3. For player 2, the king's step to (4,1) wins in 3 and comes
    // before every win in 1 in generator order, so a search that ignored the distance would choose that step.
    [TestMethod]
    [DataRow(2, DisplayName = "Player 2")]
    [DataRow(1, DisplayName = "Player 1, on the board turned round")]
    public void Search_WinInOneAndWinInThree_ChoosesWinInOne(int player)
    {
        // Arrange
        // Player 2's man on (5,0) blocks the player 1 man's only other move, and the king on (7,2) blocks
        // that man's capture.
        var board = Create(
            (5, 2, Board.Player2King), (5, 0, Board.Player2Man), (7, 2, Board.Player2King),
            (6, 1, Board.Player1Man));
        var winInOne = Move((5, 2), (7, 0), (6, 1));
        var winInThree = Move((5, 2), (4, 1));
        (board, winInOne, winInThree) = ForPlayer(player, board, winInOne, winInThree);

        // Act
        var result = NewSearch().Search(board, player, new SearchLimits(MaxDepth: 3, TimeLimit: null));

        // Assert
        // Turned round, a different move that also wins in 1 comes first, so the test checks the distance, not the move.
        Assert.AreEqual(Win - 3, PlainMinimax.ScoreOfMove(board, player, winInThree, 3, Evaluator), "The step should win in 3.");
        Assert.AreEqual(Win - 1, PlainMinimax.ScoreOfMove(board, player, winInOne, 3, Evaluator), "The capture should win in 1.");
        Assert.AreEqual(Win - 1, PlainMinimax.ScoreOfMove(board, player, result.Move, 3, Evaluator), "The chosen move should win in 1.");
        Assert.AreEqual(Win - 1, result.Score);
    }

    // Test 19: when every move loses, the slowest loss is chosen. It is not first in generator order.
    [TestMethod]
    [DataRow(1, DisplayName = "Player 1")]
    [DataRow(2, DisplayName = "Player 2, on the board turned round")]
    public void Search_EveryMoveLoses_ChoosesSlowestLoss(int player)
    {
        // Arrange
        var board = Create(
            (2, 1, Board.Player1Man), (2, 3, Board.Player1Man),
            (0, 1, Board.Player2King), (1, 4, Board.Player2King));
        var lossInTwo = Move((2, 1), (1, 0));
        var lossInFour = Move((2, 1), (1, 2));
        (board, lossInTwo, lossInFour) = ForPlayer(player, board, lossInTwo, lossInFour);

        // Act
        var result = NewSearch().Search(board, player, new SearchLimits(MaxDepth: 5, TimeLimit: null));

        // Assert
        Assert.AreEqual(-Win + 2, PlainMinimax.ScoreOfMove(board, player, lossInTwo, 5, Evaluator), "The first move should lose in 2.");
        Assert.AreEqual(Describe(lossInFour), Describe(result.Move));
        Assert.AreEqual(-Win + 4, result.Score);
    }

    // Test 20 and spec section 6.5, item 9: a move that leaves the mover with no moves loses at once. The man's only
    // move reaches (6,1), where two kings block it, and a man cannot capture a king.
    [TestMethod]
    public void Search_OnlyMoveLeavesMoverWithNoMoves_ScoresLossAtOnce()
    {
        // Arrange
        var board = Create((5, 0, Board.Player2Man), (7, 0, Board.Player1King), (7, 2, Board.Player1King));

        // Act
        var result = NewSearch().Search(board, player: 2, new SearchLimits(MaxDepth: 3, TimeLimit: null));

        // Assert
        Assert.AreEqual(-Win + 1, result.Score);
    }

    // Test 20: the search avoids a move that leaves its own pieces with no moves. That move comes first in generator
    // order, and at depth 1 both moves keep the same material.
    [TestMethod]
    public void Search_OneMoveLeavesMoverWithNoMoves_ChoosesTheOther()
    {
        // Arrange
        var board = Create((5, 2, Board.Player2Man), (7, 0, Board.Player1King), (7, 2, Board.Player1King));

        // Act
        var result = NewSearch().Search(board, player: 2, new SearchLimits(MaxDepth: 1, TimeLimit: null));

        // Assert
        Assert.AreEqual(Describe(Move((5, 2), (6, 3))), Describe(result.Move));
    }

    // Test 20: when neither player can move after a move, the game is a tie and scores 0. Here player 2's only move
    // fills (5,0), and then every piece on both sides is blocked. Before it, a king can capture through (5,0).
    // The material evaluation would give player 2 one point, so 0 shows the tie rule was used.
    [TestMethod]
    public void Search_MoveLeavesNeitherPlayerAbleToMove_ScoresTie()
    {
        // Arrange
        var board = Create(
            (7, 0, Board.Player1King), (7, 2, Board.Player1King), (7, 4, Board.Player1King), (7, 6, Board.Player1King),
            (6, 1, Board.Player2Man), (6, 3, Board.Player2Man), (6, 5, Board.Player2Man), (6, 7, Board.Player2Man),
            (5, 2, Board.Player2Man), (5, 4, Board.Player2Man), (5, 6, Board.Player2Man),
            (4, 1, Board.Player2Man));

        // Act
        var result = NewSearch().Search(board, player: 2, new SearchLimits(MaxDepth: 1, TimeLimit: null));

        // Assert
        Assert.AreEqual(Describe(Move((4, 1), (5, 0))), Describe(result.Move));
        Assert.AreEqual(0, result.Score);
    }

    [TestMethod]
    public void Search_DoesNotChangeTheCallersBoard()
    {
        // Arrange
        var board = Board.FromRows(StartingRows());
        var before = (int[])board.Clone();

        // Act
        NewSearch().Search(board, player: 2, new SearchLimits(MaxDepth: 3, TimeLimit: null));

        // Assert
        CollectionAssert.AreEqual(before, board);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow(0)]
    public void Search_NoMaximumDepth_Throws(int? maxDepth)
    {
        // Arrange
        var board = Board.FromRows(StartingRows());

        // Act and assert
        Assert.ThrowsExactly<ArgumentException>(() => NewSearch().Search(board, 2, new SearchLimits(maxDepth, null)));
    }

    [TestMethod]
    public void Search_PlayerHasNoLegalMove_Throws()
    {
        // Arrange
        var board = Create((6, 1, Board.Player1Man));

        // Act and assert
        Assert.ThrowsExactly<ArgumentException>(() => NewSearch().Search(board, 2, new SearchLimits(1, null)));
    }

    private static GameTreeSearch NewSearch() => new(Evaluator, TimeProvider.System, tieBreaker: null);

    // Turns the board round and swaps the players, so the same position can be searched for the other player.
    private static (int[] Board, Move First, Move Second) ForPlayer(int player, int[] board, Move first, Move second)
    {
        var playerInBoard = Board.BelongsTo(board[first.From], 1) ? 1 : 2;
        if (player == playerInBoard)
        {
            return (board, first, second);
        }

        return (TurnRound(board), TurnRound(first), TurnRound(second));
    }

    private static int[] TurnRound(int[] board)
    {
        var turned = new int[Board.SquareCount];

        for (var square = 0; square < Board.SquareCount; square++)
        {
            turned[Board.SquareCount - 1 - square] = board[square] switch
            {
                Board.Player1Man => Board.Player2Man,
                Board.Player2Man => Board.Player1Man,
                Board.Player1King => Board.Player2King,
                Board.Player2King => Board.Player1King,
                var value => value,
            };
        }

        return turned;
    }

    private static Move TurnRound(Move move)
    {
        var captured = 0UL;
        for (var square = 0; square < Board.SquareCount; square++)
        {
            if ((move.Captured & (1UL << square)) != 0)
            {
                captured |= 1UL << (Board.SquareCount - 1 - square);
            }
        }

        return new Move(Board.SquareCount - 1 - move.From, Board.SquareCount - 1 - move.To, captured);
    }

    private static IEnumerable<int[]> PlayedPositions(int seed, int gameCount, int pliesBetweenPositions)
    {
        var random = new Random(seed);
        var moves = new List<Move>();

        for (var game = 0; game < gameCount; game++)
        {
            var board = Board.FromRows(StartingRows());
            var player = 1;

            for (var ply = 0; ply < 80; ply++)
            {
                if (ply % pliesBetweenPositions == 0)
                {
                    yield return (int[])board.Clone();
                }

                MoveGenerator.Generate(board, player, moves);
                if (moves.Count == 0)
                {
                    break;
                }

                MoveGenerator.Apply(board, moves[random.Next(moves.Count)]);
                player = 3 - player;
            }
        }
    }

    private static string Text(int[] board) =>
        string.Join("/", Enumerable.Range(0, Board.Size).Select(row => string.Concat(board.Skip(row * Board.Size).Take(Board.Size))));
}
