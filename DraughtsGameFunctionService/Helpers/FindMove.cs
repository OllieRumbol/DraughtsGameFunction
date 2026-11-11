using DraughtsGameFunctionModels.Service;
using DraughtsGameFunctionService.Engine;
using System.Collections.Generic;
using System.Numerics;

namespace DraughtsGameFunctionService.Helpers
{
    public class FindMove
    {
        /// <summary>
        /// Every legal move for the player, from <see cref="MoveGenerator"/> (plan section 3.3).
        /// A player other than 1 or 2 has no moves, as before. Spec section 6.1.2, rule 5, makes that an error from T12.
        /// </summary>
        public static List<NextMove> FindAvailableMoves(long[,] board, long player)
        {
            var results = new List<NextMove>();
            if (player is not (1 or 2))
            {
                return results;
            }

            var moves = new List<Move>();
            MoveGenerator.Generate(Board.FromRows(board), (int)player, moves);

            foreach (var move in moves)
            {
                results.Add(ToNextMove(move));
            }

            return results;
        }

        /// <summary>A move in the API's terms: rows are heights and columns are widths.</summary>
        public static NextMove ToNextMove(Move move)
        {
            return new NextMove
            {
                CurrentHeight = Board.Row(move.From),
                CurrentWidth = Board.Column(move.From),
                NextHeight = Board.Row(move.To),
                NextWidth = Board.Column(move.To),
                Takes = ToPieces(move.Captured),
            };
        }

        // The captured squares come out in board order, not in the order of the jumps. The web app removes them all
        // at once, so the order does not matter (CompleteBoard.js, player2Go).
        private static List<Piece> ToPieces(ulong captured)
        {
            var pieces = new List<Piece>();

            while (captured != 0)
            {
                var square = BitOperations.TrailingZeroCount(captured);
                pieces.Add(new Piece { Height = Board.Row(square), Width = Board.Column(square) });
                captured &= captured - 1;
            }

            return pieces;
        }
    }
}
