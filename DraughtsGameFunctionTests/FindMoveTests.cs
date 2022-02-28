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
    }
}
