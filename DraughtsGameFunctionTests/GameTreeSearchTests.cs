using DraughtsGameFunctionService.Engine;
using Microsoft.Extensions.Time.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using static DraughtsGameFunctionTests.EngineBoards;

namespace DraughtsGameFunctionTests;

/// <summary>
/// Plan section 7.2, tests 13, 14, 16, 18, 19, 20, 31 and 33: the alpha-beta search, its limits, its ties and quiescence.
/// </summary>
[TestClass]
public class GameTreeSearchTests
{
    private const int Win = GameTreeSearch.WinScore;

    private static readonly MaterialEvaluator Evaluator = new();

    // Test 13: the search, with its table, finds the same move and score as plain minimax, for both players, at depths
    // 1 to 4. The table can reuse a score only from depth 4, where two orders of moves first reach the same position.
    // The Hard evaluation also checks quiescence. Plain minimax is slow with it, so Hard uses the first 4 games only.
    // The positions come from games of random legal moves with a fixed seed, so every run uses the same positions.
    // A player with one legal move is left out, because the search returns that move without searching (test 18).
    [TestMethod]
    [DataRow(false, 12, 200, DisplayName = "Normal evaluation")]
    [DataRow(true, 4, 70, DisplayName = "Hard evaluation, with quiescence")]
    public void Search_FixedDepth_MatchesPlainMinimax(bool hard, int gameCount, int minimumCompared)
    {
        // Arrange
        IEvaluator evaluator = hard ? new PositionalEvaluator() : Evaluator;
        var positions = PlayedPositions(seed: 20261110, gameCount, pliesBetweenPositions: 6).ToList();

        // One search for every call: each call starts with an empty table, so earlier calls cannot change a result.
        var search = new GameTreeSearch(evaluator, TimeProvider.System, tieBreaker: null);
        var mismatches = new List<string>();
        var compared = 0;

        foreach (var board in positions)
        {
            foreach (var player in new[] { 1, 2 })
            {
                if (Generate(board, player).Count < 2)
                {
                    continue;
                }

                for (var depth = 1; depth <= 4; depth++)
                {
                    // Act
                    var expected = PlainMinimax.BestMove(board, player, depth, evaluator);
                    var actual = search.Search(board, player, new SearchLimits(depth, null));
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
        Assert.IsGreaterThan(minimumCompared, compared, "Too few searches were compared to trust the result.");
    }

    // Test 13, with many positions reached twice: kings can step back and forth, so at depths 5 and 6 the same
    // position comes back by many orders of moves, and the table reuses many scores. A table that used a bound as an
    // exact score gives a different result here. The positions are from random games, once each side has a king.
    [TestMethod]
    public void Search_FixedDepthWithKings_MatchesPlainMinimax()
    {
        // Arrange
        var positions = PlayedPositions(seed: 13, gameCount: 30, pliesBetweenPositions: 8)
            .Where(board => board.Contains(Board.Player1King) && board.Contains(Board.Player2King))
            .Take(8)
            .ToList();
        var search = NewSearch();
        var mismatches = new List<string>();
        var compared = 0;

        foreach (var board in positions)
        {
            foreach (var player in new[] { 1, 2 })
            {
                if (Generate(board, player).Count < 2)
                {
                    continue;
                }

                for (var depth = 5; depth <= 6; depth++)
                {
                    // Act
                    var expected = PlainMinimax.BestMove(board, player, depth, Evaluator);
                    var actual = search.Search(board, player, new SearchLimits(depth, null));
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
        Assert.IsGreaterThan(20, compared, "Too few searches were compared to trust the result.");
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
        Assert.HasCount(2, Generate(board, player: 2));
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
        Assert.HasCount(2, Generate(board, player: 2));
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
    public void Search_NeitherLimitSet_Throws()
    {
        // Arrange
        var board = Board.FromRows(StartingRows());

        // Act and assert
        Assert.ThrowsExactly<ArgumentException>(() => NewSearch().Search(board, 2, new SearchLimits(null, null)));
    }

    [TestMethod]
    [DataRow(0, null, DisplayName = "Depth 0")]
    [DataRow(GameTreeSearch.MaxPly + 1, null, DisplayName = "Depth above the cap")]
    [DataRow(null, 0, DisplayName = "Time limit 0")]
    [DataRow(4, -1, DisplayName = "Negative time limit")]
    public void Search_LimitOutOfRange_Throws(int? maxDepth, int? timeLimitMilliseconds)
    {
        // Arrange
        var board = Board.FromRows(StartingRows());
        TimeSpan? timeLimit = timeLimitMilliseconds is int milliseconds ? TimeSpan.FromMilliseconds(milliseconds) : null;

        // Act and assert
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => NewSearch().Search(board, 2, new SearchLimits(maxDepth, timeLimit)));
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

    // Test 16 and spec section 6.5, item 3: the fake clock moves on 1 ms each time it is read, so each limit passes part
    // way through a depth. For every position, player and limit, the search must stop at the first reading at or past
    // the limit, and return exactly what its last finished depth returns on its own. A search that kept the best move
    // so far from the unfinished depth would differ in some of these cases. One position has only kings.
    // The Hard evaluation also checks that quiescence and the table stop with the search.
    [TestMethod]
    [DataRow(false, DisplayName = "Normal evaluation")]
    [DataRow(true, DisplayName = "Hard evaluation, with quiescence")]
    public void Search_TimeLimitPasses_ReturnsLastFinishedDepthsMove(bool hard)
    {
        // Arrange
        const int maxDepth = 20;
        var step = TimeSpan.FromMilliseconds(1);
        IEvaluator evaluator = hard ? new PositionalEvaluator() : Evaluator;
        var positions = PlayedPositions(seed: 16, gameCount: 4, pliesBetweenPositions: 10).Append(OnlyKings()).ToList();
        var fixedDepthSearch = new GameTreeSearch(evaluator, TimeProvider.System, tieBreaker: null);
        var mismatches = new List<string>();
        var compared = 0;

        foreach (var board in positions)
        {
            foreach (var player in new[] { 1, 2 })
            {
                if (Generate(board, player).Count < 2)
                {
                    continue;
                }

                foreach (var milliseconds in new[] { 5, 10, 20, 40 })
                {
                    var timeLimit = TimeSpan.FromMilliseconds(milliseconds);
                    var clock = new FakeTimeProvider { AutoAdvanceAmount = step };
                    var search = new GameTreeSearch(evaluator, clock, tieBreaker: null);
                    var start = clock.GetUtcNow();

                    // Act
                    var result = search.Search(board, player, new SearchLimits(maxDepth, timeLimit));
                    var elapsed = clock.GetUtcNow() - start;
                    var lastFinishedDepth = result.CompletedDepth == 0
                        ? new SearchResult(Generate(board, player)[0], 0, 0)
                        : fixedDepthSearch.Search(board, player, new SearchLimits(result.CompletedDepth, null));
                    compared++;

                    // Assert
                    // The test reads the clock once before the search, and the search reads it once as it starts.
                    // So a search that stops at the first reading at or past the limit leaves the clock 2 ms later.
                    if (elapsed > timeLimit + 2 * step || result != lastFinishedDepth)
                    {
                        mismatches.Add($"Player {player}, {milliseconds} ms: {elapsed.TotalMilliseconds} ms on the clock, "
                            + $"search {Describe(result.Move)} = {result.Score} at depth {result.CompletedDepth}, "
                            + $"last finished depth {Describe(lastFinishedDepth.Move)} = {lastFinishedDepth.Score}, board {Text(board)}");
                    }
                }
            }
        }

        Assert.IsEmpty(mismatches, string.Join(Environment.NewLine, mismatches.Take(5)));
        Assert.IsGreaterThan(30, compared, "Too few searches were compared to trust the result.");
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
        Assert.AreEqual(Describe(Generate(board, player: 2)[0]), Describe(result.Move));
        Assert.AreEqual(0, result.CompletedDepth);
        Assert.AreEqual(0, result.Score);
    }

    // Test 18 and spec section 6.2, example 15: one legal move is returned at once. The evaluator counts its calls,
    // and none is made. The man's only move is to (6,1). After it, both players can still move, so any search
    // would call the evaluator.
    [TestMethod]
    public void Search_OneLegalMove_ReturnsItWithoutSearching()
    {
        // Arrange
        var board = Create((5, 0, Board.Player2Man), (2, 7, Board.Player1Man));
        var evaluator = new CountingEvaluator();
        var search = new GameTreeSearch(evaluator, TimeProvider.System, new Random(18));

        // Act
        var result = search.Search(board, player: 2, new SearchLimits(MaxDepth: 6, TimeSpan.FromSeconds(1)));

        // Assert
        Assert.AreEqual(Describe(Move((5, 0), (6, 1))), Describe(result.Move));
        Assert.AreEqual(0, result.CompletedDepth);
        Assert.AreEqual(0, evaluator.Calls);
    }

    // Plan section 3.7, item 6: when a finished depth shows that every move loses, the search stops and keeps the
    // slowest loss. The position is test 19's, where every move loses and the slowest loses in 4. The fake clock does
    // not move, so only the stop rule ends the search before the maximum depth. Plain minimax at depth 6 checks that
    // stopping at depth 4 gives the same move and score as searching deeper.
    [TestMethod]
    public void Search_EveryMoveLoses_StopsAtDepthThatShowsIt()
    {
        // Arrange
        var board = Create(
            (2, 1, Board.Player1Man), (2, 3, Board.Player1Man),
            (0, 1, Board.Player2King), (1, 4, Board.Player2King));
        var search = new GameTreeSearch(Evaluator, new FakeTimeProvider(), tieBreaker: null);

        // Act
        var result = search.Search(board, player: 1, new SearchLimits(MaxDepth: 10, TimeSpan.FromSeconds(1)));
        var deeper = PlainMinimax.BestMove(board, player: 1, depth: 6, Evaluator);

        // Assert
        Assert.AreEqual(4, result.CompletedDepth);
        Assert.AreEqual(-Win + 4, result.Score);
        Assert.AreEqual(Describe(deeper.Move), Describe(result.Move));
        Assert.AreEqual(deeper.Score, result.Score);
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

    // Test 31: a position in the middle of an exchange is scored as the exchange ends. Player 2's man on (2,3) can
    // take the man on (3,4), and player 1's man on (5,6) then takes it back. Without quiescence, depth 1 stops after
    // the capture, which looks like a gain of a man and scores 0. With quiescence, the search sees the man taken back,
    // so the capture scores -1. Player 2's best quiet moves also score -1.
    [TestMethod]
    public void Search_CaptureThatIsTakenBack_ScoresAsExchangeEnds()
    {
        // Arrange
        var board = Create(
            (0, 1, Board.Player2Man), (2, 3, Board.Player2Man),
            (3, 4, Board.Player1Man), (5, 6, Board.Player1Man), (6, 7, Board.Player1Man));
        var limits = new SearchLimits(MaxDepth: 1, TimeLimit: null);
        var quiescentSearch = new GameTreeSearch(new QuiescentMaterialEvaluator(), TimeProvider.System, tieBreaker: null);

        // Act
        var withoutQuiescence = NewSearch().Search(board, player: 2, limits);
        var withQuiescence = quiescentSearch.Search(board, player: 2, limits);

        // Assert
        Assert.AreEqual(Describe(Move((2, 3), (4, 5), (3, 4))), Describe(withoutQuiescence.Move));
        Assert.AreEqual(0, withoutQuiescence.Score);
        Assert.AreEqual(-1, withQuiescence.Score);
    }

    // Test 31: the search stands pat where the only capture loses material. Player 2 is 2 men ahead. After player 2's
    // man on (0,1) moves, player 1's only capture is the man on (4,7) taking (3,6). Player 2's man on (1,6) would then
    // take two men back, on (2,5) and (4,5). So player 1 does not capture, and player 2's best score stays 2. A search
    // that made player 1 capture would score that line 3 for player 2.
    [TestMethod]
    public void Search_OnlyCaptureLosesMaterial_StandsPat()
    {
        // Arrange
        var board = Create(
            (0, 1, Board.Player2Man), (0, 7, Board.Player2Man), (1, 6, Board.Player2Man), (2, 7, Board.Player2Man),
            (3, 6, Board.Player2Man),
            (4, 5, Board.Player1Man), (4, 7, Board.Player1Man), (5, 4, Board.Player1Man));
        var afterQuietMove = (int[])board.Clone();
        MoveGenerator.Apply(afterQuietMove, Move((0, 1), (1, 0)));
        var search = new GameTreeSearch(new QuiescentMaterialEvaluator(), TimeProvider.System, tieBreaker: null);

        // Act
        var result = search.Search(board, player: 2, new SearchLimits(MaxDepth: 1, TimeLimit: null));

        // Assert
        var capturesAfterQuietMove = Generate(afterQuietMove, player: 1).Where(move => move.Captured != 0).ToList();
        Assert.HasCount(1, capturesAfterQuietMove, "Player 1 should have one capture after the quiet move.");
        Assert.AreEqual(Describe(Move((4, 7), (2, 5), (3, 6))), Describe(capturesAfterQuietMove[0]));
        Assert.AreEqual(2, result.Score);
    }

    // Moved from the old Normal service's EvaluateTest (plan section 7.1): the starting board scores 0, as the
    // old Normal evaluation did.
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

    [TestMethod]
    public void Search_PlayerHasNoLegalMove_Throws()
    {
        // Arrange
        var board = Create((6, 1, Board.Player1Man));

        // Act and assert
        Assert.ThrowsExactly<ArgumentException>(() => NewSearch().Search(board, 2, new SearchLimits(1, null)));
    }

    private static GameTreeSearch NewSearch() => new(Evaluator, TimeProvider.System, tieBreaker: null);

    // Three kings each, with no captures available, for spec section 6.5, item 3.
    private static int[] OnlyKings() => Create(
        (7, 0, Board.Player1King), (6, 3, Board.Player1King), (5, 6, Board.Player1King),
        (0, 1, Board.Player2King), (1, 4, Board.Player2King), (2, 7, Board.Player2King));

    // Finds the first position from the random games where at least two moves, but not every move, share the top
    // score of plain minimax at this depth.
    private static (int[] Board, int Player, List<Move> TiedMoves, int TopScore) FirstPositionWithSomeMovesTied(int depth)
    {
        foreach (var board in PlayedPositions(seed: 14, gameCount: 12, pliesBetweenPositions: 4))
        {
            foreach (var player in new[] { 2, 1 })
            {
                var moves = Generate(board, player);
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

    private static string Text(int[] board) =>
        string.Join("/", Enumerable.Range(0, Board.Size).Select(row => string.Concat(board.Skip(row * Board.Size).Take(Board.Size))));

    // The material evaluation with quiescence on, so test 31 checks quiescence with simple scores.
    private sealed class QuiescentMaterialEvaluator : IEvaluator
    {
        public bool UsesQuiescence => true;

        public int Evaluate(int[] board, int player) => Evaluator.Evaluate(board, player);
    }

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
