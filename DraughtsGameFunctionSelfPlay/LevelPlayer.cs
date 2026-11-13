using DraughtsGameFunctionModels.Enums;
using DraughtsGameFunctionModels.Service;
using DraughtsGameFunctionService.Engine;
using DraughtsGameFunctionService.Instance;

namespace DraughtsGameFunctionSelfPlay;

/// <summary>
/// A level as the API plays it: the same <see cref="ComputerPlayer"/>, with the board and the move converted between
/// the engine's form and the API's form.
/// </summary>
public static class LevelPlayer
{
    public static ChooseMove For(ComputerPlayer computerPlayer, Difficulty difficulty) => (board, player) =>
    {
        var computerMove = computerPlayer.GetNextMove(ToRows(board), player, difficulty)
            ?? throw new InvalidOperationException($"{difficulty} found no move for player {player}, who has one.");

        return ToMove(computerMove.Move);
    };

    private static long[,] ToRows(int[] board)
    {
        var rows = new long[Board.Size, Board.Size];

        for (var square = 0; square < Board.SquareCount; square++)
        {
            rows[Board.Row(square), Board.Column(square)] = board[square];
        }

        return rows;
    }

    private static Move ToMove(NextMove move)
    {
        var captured = 0UL;
        foreach (var piece in move.Takes)
        {
            captured |= 1UL << Board.Index((int)piece.Height, (int)piece.Width);
        }

        return new Move(
            Board.Index((int)move.CurrentHeight, (int)move.CurrentWidth),
            Board.Index((int)move.NextHeight, (int)move.NextWidth),
            captured);
    }
}
