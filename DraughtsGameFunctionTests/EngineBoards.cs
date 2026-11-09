using DraughtsGameFunctionService.Engine;
using System.Collections.Generic;
using System.Linq;

namespace DraughtsGameFunctionTests;

/// <summary>
/// Builds engine boards and moves for tests, in the (row, column) terms that the spec's examples use.
/// </summary>
internal static class EngineBoards
{
    /// <summary>A board with the given pieces. Every other dark square is empty and every light square is 0.</summary>
    public static int[] Create(params (int Row, int Column, int Value)[] pieces)
    {
        var board = new int[Board.SquareCount];

        for (var square = 0; square < Board.SquareCount; square++)
        {
            var isDark = (Board.Row(square) + Board.Column(square)) % 2 == 1;
            board[square] = isDark ? Board.Empty : 0;
        }

        foreach (var (row, column, value) in pieces)
        {
            board[Board.Index(row, column)] = value;
        }

        return board;
    }

    /// <summary>The starting board in spec section 2.2.</summary>
    public static long[,] StartingRows() => new long[,]
    {
        { 0, 2, 0, 2, 0, 2, 0, 2 },
        { 2, 0, 2, 0, 2, 0, 2, 0 },
        { 0, 2, 0, 2, 0, 2, 0, 2 },
        { 5, 0, 5, 0, 5, 0, 5, 0 },
        { 0, 5, 0, 5, 0, 5, 0, 5 },
        { 1, 0, 1, 0, 1, 0, 1, 0 },
        { 0, 1, 0, 1, 0, 1, 0, 1 },
        { 1, 0, 1, 0, 1, 0, 1, 0 },
    };

    public static long[,] ToRows(int[] board)
    {
        var rows = new long[Board.Size, Board.Size];

        for (var square = 0; square < Board.SquareCount; square++)
        {
            rows[Board.Row(square), Board.Column(square)] = board[square];
        }

        return rows;
    }

    public static Move Move((int Row, int Column) from, (int Row, int Column) to, params (int Row, int Column)[] captured)
    {
        var mask = 0UL;
        foreach (var (row, column) in captured)
        {
            mask |= 1UL << Board.Index(row, column);
        }

        return new Move(Board.Index(from.Row, from.Column), Board.Index(to.Row, to.Column), mask);
    }

    public static List<Move> Generate(int[] board, int player)
    {
        var moves = new List<Move>();
        MoveGenerator.Generate(board, player, moves);

        return moves;
    }

    /// <summary>A readable form of a move for assertion messages, for example "(1,2)->(3,4) x(2,3)".</summary>
    public static string Describe(Move move)
    {
        var captured = Enumerable.Range(0, Board.SquareCount)
            .Where(square => (move.Captured & (1UL << square)) != 0)
            .Select(square => $"({Board.Row(square)},{Board.Column(square)})");

        var text = $"({Board.Row(move.From)},{Board.Column(move.From)})->({Board.Row(move.To)},{Board.Column(move.To)})";

        return move.Captured == 0 ? text : $"{text} x{string.Join("", captured)}";
    }
}
