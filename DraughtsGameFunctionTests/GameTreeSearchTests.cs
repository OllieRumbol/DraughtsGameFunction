using DraughtsGameFunctionService.Engine;
using Microsoft.Extensions.Time.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using static DraughtsGameFunctionTests.EngineBoards;

namespace DraughtsGameFunctionTests;

/// <summary>
/// Plan section 7.2, tests 13, 14, 16, 18, 19, 20 and 33: the alpha-beta search, its limits and its ties.
/// </summary>
[TestClass]
public class GameTreeSearchTests
{
    private const int Win = GameTreeSearch.WinScore;

    private static readonly MaterialEvaluator Evaluator = new();

    // Test 13: the search finds the same move and score as plain minimax, for both players, at depths 1 to 4.
    // The positions come from games of random legal moves with a fixed seed, so every run uses the same positions.
    // A player with one legal move is left out, because the search returns that move without searching (test 18).
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
                if (LegalMoves(board, player).Count < 2)
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

    // Test 20 and spec section 6.5, item 9: a move that leaves the mover with no moves loses at once. The man has two
    // moves, to (6,1) and (6,3), and kings block it on both, because a man cannot capture a king.
    [TestMethod]
    public void Search_EveryMoveLeavesMoverWithNoMoves_ScoresLossAtOnce()
    {
        // Arrange
        var board = Create(
            (5, 2, Board.Player2Man), (7, 0, Board.Player1King), (7, 2, Board.Player1King), (7, 4, Board.Player1King));

        // Act
        var result = NewSearch().Search(board, player: 2, new SearchLimits(MaxDepth: 3, TimeLimit: null));

        // Assert
        Assert.HasCount(2, LegalMoves(board, player: 2));
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

    // Test 20: when neither player can move after a move, the game is a tie and scores 0. Player 2 has two moves,
    // from (4,1) and from (4,3), and both fill (5,2). After either one, every piece on both sides is blocked.
    // Before it, a king can capture through (5,2). Player 2 has 9 men against 4 kings, so the material evaluation
    // would give player 2 one point, and 0 shows the tie rule was used.
    [TestMethod]
    public void Search_MoveLeavesNeitherPlayerAbleToMove_ScoresTie()
    {
        // Arrange
        var board = Create(
            (7, 0, Board.Player1King), (7, 2, Board.Player1King), (7, 4, Board.Player1King), (7, 6, Board.Player1King),
            (6, 1, Board.Player2Man), (6, 3, Board.Player2Man), (6, 5, Board.Player2Man), (6, 7, Board.Player2Man),
            (5, 0, Board.Player2Man), (5, 4, Board.Player2Man), (5, 6, Board.Player2Man),
            (4, 1, Board.Player2Man), (4, 3, Board.Player2Man));

        // Act
        var result = NewSearch().Search(board, player: 2, new SearchLimits(MaxDepth: 1, TimeLimit: null));

        // Assert
        Assert.AreEqual(1, Evaluator.Evaluate(board, player: 2), "Player 2 should be one point ahead.");
        Assert.HasCount(2, LegalMoves(board, player: 2));
        Assert.AreEqual(Describe(Move((4, 1), (5, 2))), Describe(result.Move));
        Assert.AreEqual(1, result.CompletedDepth);
        Assert.AreEqual(0, result.Score);
    }

    [TestMethod]
    public void Search_AnyPosition_LeavesCallersBoardUnchanged()
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
    [DataRow(null, null, DisplayName = "Neither limit")]
    [DataRow(0, null, DisplayName = "Depth 0")]
    [DataRow(GameTreeSearch.MaxPly + 1, null, DisplayName = "Depth above the cap")]
    [DataRow(null, 0, DisplayName = "Time limit 0")]
    [DataRow(4, -1, DisplayName = "Negative time limit")]
    public void Search_LimitsMissingOrOutOfRange_Throws(int? maxDepth, int? timeLimitMilliseconds)
    {
        // Arrange
        var board = Board.FromRows(StartingRows());
        TimeSpan? timeLimit = timeLimitMilliseconds is int milliseconds ? TimeSpan.FromMilliseconds(milliseconds) : null;

        // Act and assert
        Assert.ThrowsExactly<ArgumentException>(() => NewSearch().Search(board, 2, new SearchLimits(maxDepth, timeLimit)));
    }

    // Test 14: when several moves share the top score, repeated searches return each of them, and never a move with
    // a lower score. The position is the first from the random games where some, but not all, moves tie for the top.
    // A plain alpha-beta window would cut off a move that only equals the best, so only the first would be returned.
    [TestMethod]
    public void Search_SeveralMovesTieForTopScore_ReturnsEachTiedMoveAndNoOther()
    {
        // Arrange
        const int depth = 3;
        var (board, player, tiedMoves, topScore) = FirstPositionWithSomeMovesTied(depth);
        var search = new GameTreeSearch(Evaluator, TimeProvider.System, new Random(14));
        var returned = new HashSet<Move>();

        // Act
        for (var run = 0; run < 50; run++)
        {
            var result = search.Search(board, player, new SearchLimits(depth, TimeLimit: null));
            returned.Add(result.Move);

            Assert.AreEqual(topScore, result.Score);
        }

        // Assert
        CollectionAssert.AreEquivalent(tiedMoves.Select(Describe).ToList(), returned.Select(Describe).ToList());
    }

    // Test 16: the fake clock moves on 1 ms each time it is read. The search must stop at the first reading at or past
    // the limit, part way through a depth, and return what the last finished depth returns on its own.
    [TestMethod]
    public void Search_TimeLimitPasses_ReturnsLastFinishedDepthsMove()
    {
        // Arrange
        const int maxDepth = 12;
        var board = Board.FromRows(StartingRows());
        var step = TimeSpan.FromMilliseconds(1);
        var timeLimit = TimeSpan.FromMilliseconds(30);
        var clock = new FakeTimeProvider { AutoAdvanceAmount = step };
        var search = new GameTreeSearch(Evaluator, clock, tieBreaker: null);
        var start = clock.GetUtcNow();

        // Act
        var result = search.Search(board, player: 2, new SearchLimits(maxDepth, timeLimit));
        var elapsed = clock.GetUtcNow() - start;

        // Assert
        // The readings here, at the start of the search, and at and past the limit, account for the other 2 ms.
        Assert.IsLessThanOrEqualTo(timeLimit + 2 * step, elapsed, "The search read the clock after the limit passed.");
        Assert.IsGreaterThan(1, result.CompletedDepth);
        Assert.IsLessThan(maxDepth, result.CompletedDepth);

        var lastFinishedDepth = NewSearch().Search(board, player: 2, new SearchLimits(result.CompletedDepth, null));
        Assert.AreEqual(Describe(lastFinishedDepth.Move), Describe(result.Move));
        Assert.AreEqual(lastFinishedDepth.Score, result.Score);
    }

    // Plan section 4: if the limit passes before depth 1 finishes, the first legal move is returned. Here the fake
    // clock moves on by the whole limit at each reading, so the limit has passed at the first check.
    [TestMethod]
    public void Search_TimeLimitPassesBeforeDepthOne_ReturnsFirstLegalMove()
    {
        // Arrange
        var board = Board.FromRows(StartingRows());
        var timeLimit = TimeSpan.FromMilliseconds(500);
        var clock = new FakeTimeProvider { AutoAdvanceAmount = timeLimit };
        var search = new GameTreeSearch(Evaluator, clock, tieBreaker: null);

        // Act
        var result = search.Search(board, player: 2, new SearchLimits(MaxDepth: null, timeLimit));

        // Assert
        Assert.AreEqual(Describe(LegalMoves(board, player: 2)[0]), Describe(result.Move));
        Assert.AreEqual(0, result.CompletedDepth);
        Assert.AreEqual(0, result.Score);
    }

    // Test 18 and spec section 6.2, example 15: one legal move is returned at once. The evaluator counts its calls,
    // and none is made. The man's only move is to (6,1).
    [TestMethod]
    public void Search_OneLegalMove_ReturnsItWithoutSearching()
    {
        // Arrange
        var board = Create((5, 0, Board.Player2Man), (7, 0, Board.Player1King), (7, 2, Board.Player1King));
        var evaluator = new CountingEvaluator();
        var search = new GameTreeSearch(evaluator, TimeProvider.System, new Random(18));

        // Act
        var result = search.Search(board, player: 2, new SearchLimits(MaxDepth: 6, TimeSpan.FromSeconds(1)));

        // Assert
        Assert.AreEqual(Describe(Move((5, 0), (6, 1))), Describe(result.Move));
        Assert.AreEqual(0, result.CompletedDepth);
        Assert.AreEqual(0, evaluator.Calls);
    }

    // Test 33: a forced win stops the search before the time limit. The fake clock does not move, so only the win
    // can stop it before the maximum depth. The maximum depth is set only so that a search that missed the win would
    // fail this test rather than run for a very long time. The position is test 19's, where a capture wins in 1.
    [TestMethod]
    public void Search_ForcedWinFound_StopsBeforeTimeLimit()
    {
        // Arrange
        var board = Create(
            (5, 2, Board.Player2King), (5, 0, Board.Player2Man), (7, 2, Board.Player2King),
            (6, 1, Board.Player1Man));
        var search = new GameTreeSearch(Evaluator, new FakeTimeProvider(), new Random(33));

        // Act
        var result = search.Search(board, player: 2, new SearchLimits(MaxDepth: 8, TimeSpan.FromSeconds(1)));

        // Assert
        Assert.AreEqual(1, result.CompletedDepth);
        Assert.AreEqual(Win - 1, result.Score);
    }

    // Spec section 6.1.5, item 5, and plan section 3.5: material only, a man 1 and a king 2, positive for the player.
    [TestMethod]
    [DataRow(1, 3)]
    [DataRow(2, -3)]
    public void Evaluate_TwoMenAndAKingAgainstOneMan_ScoresMaterial(int player, int expected)
    {
        // Arrange
        var board = Create(
            (5, 0, Board.Player1Man), (5, 2, Board.Player1Man), (6, 1, Board.Player1King),
            (2, 1, Board.Player2Man), (3, 4, Board.Highlighted));

        // Act
        var score = Evaluator.Evaluate(board, player);

        // Assert
        Assert.AreEqual(expected, score);
        Assert.IsFalse(Evaluator.UsesQuiescence);
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

    private static List<Move> LegalMoves(int[] board, int player)
    {
        var moves = new List<Move>();
        MoveGenerator.Generate(board, player, moves);

        return moves;
    }

    // Finds the first position from the random games where at least two moves, but not every move, share the top
    // score of plain minimax at this depth.
    private static (int[] Board, int Player, List<Move> TiedMoves, int TopScore) FirstPositionWithSomeMovesTied(int depth)
    {
        foreach (var board in PlayedPositions(seed: 14, gameCount: 12, pliesBetweenPositions: 4))
        {
            foreach (var player in new[] { 2, 1 })
            {
                var moves = LegalMoves(board, player);
                if (moves.Count == 0)
                {
                    continue;
                }

                var scores = moves.Select(move => PlainMinimax.ScoreOfMove(board, player, move, depth, Evaluator)).ToList();
                var topScore = scores.Max();
                var tiedMoves = moves.Where((move, index) => scores[index] == topScore).ToList();

                if (tiedMoves.Count >= 2 && tiedMoves.Count < moves.Count)
                {
                    return (board, player, tiedMoves, topScore);
                }
            }
        }

        throw new InvalidOperationException("No position from the random games has some, but not all, moves tied.");
    }

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

    // The material evaluation, counting how often it is called.
    private sealed class CountingEvaluator : IEvaluator
    {
        public int Calls { get; private set; }

        public bool UsesQuiescence => false;

        public int Evaluate(int[] board, int player)
        {
            Calls++;

            return Evaluator.Evaluate(board, player);
        }
    }
}
