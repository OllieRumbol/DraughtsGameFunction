using DraughtsGameFunctionModels.Service;
using DraughtsGameFunctionService.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DraughtsGameFunctionTests
{
    [TestClass]
    public class FindMoveTests
    {
        [TestMethod]
        public void FindAvailableMovesTests()
        {
            //Associate
            Int64[,] board = new Int64[,]
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
            Int64 player = 1;

            //Act
            List<NextMove> moves = FindMove.FindAvailableMoves(board, player);

            //Assert
            Assert.AreEqual(7, moves.Count);
        }

        [TestMethod]
        public void FindAvailableMovesTests_Takes()
        {
            //Associate
            Int64[,] board = new Int64[,]
            {
                { 0, 2, 0, 2, 0, 2, 0, 2 },
                { 2, 0, 2, 0, 2, 0, 2, 0 },
                { 0, 2, 0, 5, 0, 2, 0, 2 },
                { 5, 0, 5, 0, 5, 0, 5, 0 },
                { 0, 5, 0, 2, 0, 5, 0, 5 },
                { 1, 0, 1, 0, 1, 0, 1, 0 },
                { 0, 1, 0, 1, 0, 1, 0, 1 },
                { 1, 0, 1, 0, 1, 0, 1, 0 },
            };
            Int64 player = 1;

            //Act
            List<NextMove> moves = FindMove.FindAvailableMoves(board, player);

            //Assert
            Assert.AreEqual(2, moves.Count( m => m.Takes.Count > 0));
        }

        [TestMethod]
        public void FindAvailableMovesTests_Kings()
        {
            //Associate
            Int64[,] board = new Int64[,]
            {
                { 0, 2, 0, 2, 0, 2, 0, 2 },
                { 2, 0, 2, 0, 2, 0, 2, 0 },
                { 0, 2, 0, 5, 0, 2, 0, 2 },
                { 5, 0, 5, 0, 5, 0, 5, 0 },
                { 0, 5, 0, 5, 0, 5, 0, 5 },
                { 5, 0, 5, 0, 5, 0, 5, 0 },
                { 0, 5, 0, 2, 0, 5, 0, 5 },
                { 5, 0, 3, 0, 5, 0, 5, 0 },
            };
            Int64 player = 1;

            //Act
            List<NextMove> moves = FindMove.FindAvailableMoves(board, player);

            //Assert
            Assert.AreEqual(2, moves.Count);
        }

        [TestMethod]
        public void FindAvailableMovesTests_KingsAdvanced()
        {
            //Associate
            Int64[,] board = new Int64[,]
            {
                { 0, 2, 0, 2, 0, 2, 0, 2 },
                { 2, 0, 2, 0, 2, 0, 2, 0 },
                { 0, 2, 0, 5, 0, 2, 0, 2 },
                { 5, 0, 5, 0, 5, 0, 5, 0 },
                { 0, 4, 0, 2, 0, 5, 0, 5 },
                { 5, 0, 5, 0, 5, 0, 5, 0 },
                { 0, 5, 0, 2, 0, 5, 0, 5 },
                { 5, 0, 3, 0, 5, 0, 5, 0 },
            };
            Int64 player = 1;

            //Act
            List<NextMove> moves = FindMove.FindAvailableMoves(board, player);

            //Assert
            Assert.AreEqual(4, moves.Count);
        }

        // Moved from the tests of the removed capture code (plan section 7.1): the same board, and the same capture down-left from (3,4).
        [TestMethod]
        public void FindAvailableMoves_ManCanCaptureDownLeftOnly_ReturnsThatCapture()
        {
            //Associate
            long[,] board = new long[,]
            {
                { 0, 2, 0, 2, 0, 2, 0, 2 },
                { 2, 0, 2, 0, 2, 0, 2, 0 },
                { 0, 2, 0, 5, 0, 2, 0, 2 },
                { 5, 0, 5, 0, 2, 0, 5, 0 },
                { 0, 5, 0, 1, 0, 5, 0, 5 },
                { 1, 0, 5, 0, 1, 0, 1, 0 },
                { 0, 1, 0, 1, 0, 1, 0, 1 },
                { 1, 0, 1, 0, 1, 0, 1, 0 },
            };

            //Act
            List<NextMove> moves = FindMove.FindAvailableMoves(board, 2);

            //Assert
            AssertCaptures(moves, 3, 4, "(5,2) x(4,3)");
        }

        // Moved from the tests of the removed capture code: the same board, and the same double jump from (2,3) to (6,7).
        [TestMethod]
        public void FindAvailableMoves_ManCanDoubleJump_ReturnsBothStops()
        {
            //Associate
            long[,] board = new long[,]
            {
                { 0, 2, 0, 2, 0, 2, 0, 2 },
                { 2, 0, 2, 0, 2, 0, 2, 0 },
                { 0, 2, 0, 2, 0, 2, 0, 2 },
                { 5, 0, 5, 0, 1, 0, 5, 0 },
                { 0, 5, 0, 5, 0, 5, 0, 1 },
                { 1, 0, 5, 0, 1, 0, 1, 0 },
                { 0, 1, 0, 1, 0, 1, 0, 5 },
                { 1, 0, 1, 0, 1, 0, 1, 0 },
            };

            //Act
            List<NextMove> moves = FindMove.FindAvailableMoves(board, 2);

            //Assert
            AssertCaptures(moves, 2, 3, "(4,5) x(3,4)", "(6,7) x(3,4)(5,6)");
        }

        // Moved from the tests of the removed capture code: the same board, and the same king capture up-right from (7,2) over (6,3).
        [TestMethod]
        public void FindAvailableMoves_KingCanCaptureUpRight_ReturnsThatCapture()
        {
            //Associate
            long[,] board = new long[,]
            {
                { 0, 2, 0, 2, 0, 2, 0, 2 },
                { 2, 0, 2, 0, 2, 0, 2, 0 },
                { 0, 2, 0, 5, 0, 2, 0, 2 },
                { 5, 0, 5, 0, 5, 0, 5, 0 },
                { 0, 4, 0, 2, 0, 5, 0, 5 },
                { 5, 0, 5, 0, 5, 0, 5, 0 },
                { 0, 5, 0, 2, 0, 5, 0, 5 },
                { 5, 0, 3, 0, 5, 0, 5, 0 },
            };

            //Act
            List<NextMove> moves = FindMove.FindAvailableMoves(board, 1);

            //Assert
            AssertCaptures(moves, 7, 2, "(5,4) x(6,3)", "(3,2) x(4,3)(6,3)", "(5,0) x(4,1)(4,3)(6,3)");
        }

        [TestMethod]
        [DataRow(0L)]
        [DataRow(3L)]
        public void FindAvailableMoves_PlayerNotOneOrTwo_ReturnsNoMoves(long player)
        {
            //Associate
            long[,] board = new long[,]
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

            //Act
            List<NextMove> moves = FindMove.FindAvailableMoves(board, player);

            //Assert
            Assert.IsEmpty(moves);
        }

        // Checks the capture moves of the piece on (row, column), written as "(to) x(captured)(captured)".
        private static void AssertCaptures(List<NextMove> moves, long row, long column, params string[] expected)
        {
            IEnumerable<string> actual = moves
                .Where(m => m.CurrentHeight == row && m.CurrentWidth == column && m.Takes.Count > 0)
                .Select(m => $"({m.NextHeight},{m.NextWidth}) x" + string.Concat(m.Takes.Select(t => $"({t.Height},{t.Width})")))
                .Order();

            Assert.AreEqual(string.Join(", ", expected.Order()), string.Join(", ", actual));
        }
    }
}
