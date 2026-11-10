using DraughtsGameFunctionService.Engine;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using static DraughtsGameFunctionTests.EngineBoards;

namespace DraughtsGameFunctionTests;

/// <summary>
/// The transposition table in plan section 3.7: its keys, full key comparison (risk 10), and win and loss scores.
/// </summary>
[TestClass]
public class TranspositionTableTests
{
    private const int Win = GameTreeSearch.WinScore;

    [TestMethod]
    public void KeyOf_HighlightedSquareInsteadOfEmpty_GivesSameKey()
    {
        // Arrange
        var board = Board.FromRows(StartingRows());
        var highlighted = (int[])board.Clone();
        highlighted[Board.Index(3, 2)] = Board.Highlighted;

        // Act
        var key = TranspositionTable.KeyOf(board, player: 2);
        var highlightedKey = TranspositionTable.KeyOf(highlighted, player: 2);

        // Assert
        Assert.AreEqual(key, highlightedKey);
    }

    [TestMethod]
    public void KeyOf_OtherPlayerToMove_GivesDifferentKey()
    {
        // Arrange
        var board = Board.FromRows(StartingRows());

        // Act
        var player1Key = TranspositionTable.KeyOf(board, player: 1);
        var player2Key = TranspositionTable.KeyOf(board, player: 2);

        // Assert
        Assert.AreNotEqual(player1Key, player2Key);
    }

    [TestMethod]
    [DataRow(Board.Player1King, DisplayName = "Man becomes king")]
    [DataRow(Board.Empty, DisplayName = "Piece removed")]
    public void KeyOf_OnePieceChanged_GivesDifferentKey(int newValue)
    {
        // Arrange
        var board = Board.FromRows(StartingRows());
        var changed = (int[])board.Clone();
        changed[Board.Index(5, 0)] = newValue;

        // Act
        var key = TranspositionTable.KeyOf(board, player: 1);
        var changedKey = TranspositionTable.KeyOf(changed, player: 1);

        // Assert
        Assert.AreNotEqual(key, changedKey);
    }

    [TestMethod]
    public void TryGet_KeyStored_ReturnsEntry()
    {
        // Arrange
        var table = new TranspositionTable();
        table.Store(key: 12345, depth: 4, ply: 3, score: -17, ScoreBound.Lower, bestMoveIndex: 2);

        // Act
        var found = table.TryGet(12345, ply: 3, out var entry);

        // Assert
        Assert.IsTrue(found);
        Assert.AreEqual(new TableEntry(Depth: 4, Score: -17, ScoreBound.Lower, BestMoveIndex: 2), entry);
    }

    [TestMethod]
    public void TryGet_NothingStored_ReturnsFalse()
    {
        // Arrange
        var table = new TranspositionTable();

        // Act
        var found = table.TryGet(0, ply: 1, out _);

        // Assert
        Assert.IsFalse(found);
    }

    // Risk 10: two keys that share a slot are told apart, because the full 64-bit key is compared.
    [TestMethod]
    public void TryGet_OtherKeyInSameSlot_ReturnsFalse()
    {
        // Arrange
        var table = new TranspositionTable();
        const ulong storedKey = 12345;
        const ulong sameSlotKey = storedKey + TranspositionTable.EntryCount;
        table.Store(storedKey, depth: 2, ply: 1, score: 5, ScoreBound.Exact, bestMoveIndex: 0);

        // Act
        var found = table.TryGet(sameSlotKey, ply: 1, out _);

        // Assert
        Assert.IsFalse(found);
    }

    [TestMethod]
    public void TryGet_AfterClear_ReturnsFalse()
    {
        // Arrange
        var table = new TranspositionTable();
        table.Store(key: 12345, depth: 2, ply: 1, score: 5, ScoreBound.Exact, bestMoveIndex: 0);

        // Act
        table.Clear();
        var found = table.TryGet(12345, ply: 1, out _);

        // Assert
        Assert.IsFalse(found);
    }

    // Plan section 3.7: a win or loss is stored by its distance from the position. Stored at ply 2 as a win at ply 5,
    // it is 3 moves from the position. Found again at ply 4, it is a win at ply 7.
    [TestMethod]
    [DataRow(Win - 5, Win - 7, DisplayName = "Win")]
    [DataRow(-Win + 5, -Win + 7, DisplayName = "Loss")]
    [DataRow(250, 250, DisplayName = "Evaluation score")]
    public void TryGet_ScoreStoredAtOtherPly_KeepsDistanceFromPosition(int storedScore, int expectedScore)
    {
        // Arrange
        var table = new TranspositionTable();
        table.Store(key: 12345, depth: 3, ply: 2, storedScore, ScoreBound.Exact, bestMoveIndex: 0);

        // Act
        table.TryGet(12345, ply: 4, out var entry);

        // Assert
        Assert.AreEqual(expectedScore, entry.Score);
    }

    [TestMethod]
    [DataRow(0, 0, DisplayName = "Depth 0")]
    [DataRow(GameTreeSearch.MaxPly + 1, 0, DisplayName = "Depth above the cap")]
    [DataRow(1, -1, DisplayName = "Negative move index")]
    public void Store_ValueOutOfRange_Throws(int depth, int bestMoveIndex)
    {
        // Arrange
        var table = new TranspositionTable();

        // Act and assert
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => table.Store(key: 1, depth, ply: 1, score: 0, ScoreBound.Exact, bestMoveIndex));
    }
}
