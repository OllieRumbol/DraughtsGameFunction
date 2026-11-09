#nullable enable

namespace DraughtsGameFunctionService.Engine;

/// <summary>
/// The board as a flat array of 64 squares, indexed by row * 8 + column, with the values in spec section 2.2.
/// </summary>
public static class Board
{
    public const int Size = 8;
    public const int SquareCount = Size * Size;

    public const int Player1Man = 1;
    public const int Player2Man = 2;
    public const int Player1King = 3;
    public const int Player2King = 4;
    public const int Empty = 5;
    public const int Highlighted = 6;

    // The four diagonal directions, as row and column steps.
    public const int UpLeft = 0;
    public const int UpRight = 1;
    public const int DownLeft = 2;
    public const int DownRight = 3;

    private static readonly int[] RowSteps = [-1, -1, 1, 1];
    private static readonly int[] ColumnSteps = [-1, 1, -1, 1];

    /// <summary>The square one step away in each direction, or -1 off the board.</summary>
    internal static readonly int[,] OneStep = BuildSteps(distance: 1);

    /// <summary>The square two steps away in each direction, or -1 off the board.</summary>
    internal static readonly int[,] TwoSteps = BuildSteps(distance: 2);

    public static int Index(int row, int column) => row * Size + column;

    public static int Row(int square) => square / Size;

    public static int Column(int square) => square % Size;

    /// <summary>
    /// Copies the request's board into the flat array. The caller has already checked that it is 8 by 8 and that
    /// each value is 0 to 6 (spec section 6.1.2, rules 2 and 4), so the cast to int cannot change a value.
    /// </summary>
    public static int[] FromRows(long[,] rows)
    {
        var board = new int[SquareCount];

        for (var row = 0; row < Size; row++)
        {
            for (var column = 0; column < Size; column++)
            {
                board[Index(row, column)] = (int)rows[row, column];
            }
        }

        return board;
    }

    // A highlighted square is an empty square that the web app has marked (spec section 2.2).
    public static bool IsEmpty(int value) => value == Empty || value == Highlighted;

    public static bool IsKing(int value) => value == Player1King || value == Player2King;

    public static bool BelongsTo(int value, int player) => player == 1
        ? value == Player1Man || value == Player1King
        : value == Player2Man || value == Player2King;

    /// <summary>The row where a man of this player becomes a king: row 0 for player 1, row 7 for player 2.</summary>
    public static int FarRow(int player) => player == 1 ? 0 : Size - 1;

    private static int[,] BuildSteps(int distance)
    {
        var steps = new int[SquareCount, 4];

        for (var square = 0; square < SquareCount; square++)
        {
            for (var direction = 0; direction < 4; direction++)
            {
                var row = Row(square) + RowSteps[direction] * distance;
                var column = Column(square) + ColumnSteps[direction] * distance;
                var isOnBoard = row is >= 0 and < Size && column is >= 0 and < Size;

                steps[square, direction] = isOnBoard ? Index(row, column) : -1;
            }
        }

        return steps;
    }
}
