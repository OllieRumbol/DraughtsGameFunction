#nullable enable

using System;

namespace DraughtsGameFunctionService.Engine;

/// <summary>
/// Remembers the positions a search has scored, so the search does not score a position again when a different order
/// of moves reaches it (plan section 3.7). A new position always replaces the one in its slot. Each entry keeps the
/// full 64-bit key, so two positions that share a slot are never mixed up (risk 10).
/// Use one table for each search: it is not thread-safe.
/// </summary>
public sealed class TranspositionTable
{
    /// <summary>The number of entries: 2^18, 16 bytes each, so 4 MB.</summary>
    public const int EntryCount = 1 << 18;

    // A score at least this far from 0 is a win or a loss. Evaluation scores stay well below it.
    private const int WinOrLossThreshold = GameTreeSearch.WinScore - GameTreeSearch.MaxPly;

    // A fixed seed gives the same keys on every run, so a search always does the same work.
    private const int KeySeed = 20261110;

    // One random key for each piece value on each square, and one for player 2 to move (a Zobrist hash).
    private static readonly ulong[,] PieceKeys;
    private static readonly ulong Player2ToMoveKey;

    private readonly Entry[] _entries = new Entry[EntryCount];

    static TranspositionTable()
    {
        var random = new Random(KeySeed);
        PieceKeys = new ulong[Board.SquareCount, Board.Player2King + 1];

        for (var square = 0; square < Board.SquareCount; square++)
        {
            for (var value = Board.Player1Man; value <= Board.Player2King; value++)
            {
                PieceKeys[square, value] = NextKey(random);
            }
        }

        Player2ToMoveKey = NextKey(random);
    }

    /// <summary>
    /// The key of a position: the pieces and the player who moves next. Empty and highlighted squares add nothing, so
    /// they give the same key (plan section 3.7).
    /// </summary>
    public static ulong KeyOf(int[] board, int player)
    {
        var key = player == 2 ? Player2ToMoveKey : 0UL;

        for (var square = 0; square < Board.SquareCount; square++)
        {
            var value = board[square];
            if (value is >= Board.Player1Man and <= Board.Player2King)
            {
                key ^= PieceKeys[square, value];
            }
        }

        return key;
    }

    public void Clear() => Array.Clear(_entries);

    /// <summary>
    /// Stores a score that was searched to <paramref name="depth"/> at <paramref name="ply"/> moves from the root.
    /// It replaces whatever is in the slot.
    /// </summary>
    public void Store(ulong key, int depth, int ply, int score, ScoreBound bound, int bestMoveIndex)
    {
        // Depth 0 marks an empty slot, so only a searched depth can be stored.
        ArgumentOutOfRangeException.ThrowIfLessThan(depth, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(depth, GameTreeSearch.MaxPly);
        ArgumentOutOfRangeException.ThrowIfNegative(bestMoveIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(bestMoveIndex, short.MaxValue);

        ref var entry = ref _entries[SlotOf(key)];
        entry.Key = key;
        entry.Score = ToNodeScore(score, ply);
        entry.BestMoveIndex = (short)bestMoveIndex;
        entry.Depth = (byte)depth;
        entry.Bound = bound;
    }

    /// <summary>
    /// Finds the position with this key. The score is returned for a node <paramref name="ply"/> moves from the root.
    /// </summary>
    public bool TryGet(ulong key, int ply, out TableEntry found)
    {
        var entry = _entries[SlotOf(key)];

        if (entry.Depth == 0 || entry.Key != key)
        {
            found = default;
            return false;
        }

        found = new TableEntry(entry.Depth, ToRootScore(entry.Score, ply), entry.Bound, entry.BestMoveIndex);
        return true;
    }

    private static int SlotOf(ulong key) => (int)(key & (EntryCount - 1));

    // Plan section 3.7: the search scores a win by its distance from the root. The table stores it by its distance
    // from the node, so the score is still right when another route reaches the same position at a different ply.
    private static int ToNodeScore(int score, int ply)
    {
        if (score >= WinOrLossThreshold)
        {
            return score + ply;
        }

        if (score <= -WinOrLossThreshold)
        {
            return score - ply;
        }

        return score;
    }

    private static int ToRootScore(int score, int ply)
    {
        if (score >= WinOrLossThreshold)
        {
            return score - ply;
        }

        if (score <= -WinOrLossThreshold)
        {
            return score + ply;
        }

        return score;
    }

    private static ulong NextKey(Random random)
    {
        Span<byte> bytes = stackalloc byte[sizeof(ulong)];
        random.NextBytes(bytes);

        return BitConverter.ToUInt64(bytes);
    }

    // 16 bytes. The depth is at most MaxPly, and a position has far fewer than short.MaxValue moves.
    private struct Entry
    {
        public ulong Key;
        public int Score;
        public short BestMoveIndex;
        public byte Depth;
        public ScoreBound Bound;
    }
}
