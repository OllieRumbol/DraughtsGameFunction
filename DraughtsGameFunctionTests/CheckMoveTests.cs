using DraughtsGameFunctionModels.Service;
using DraughtsGameFunctionService.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DraughtsGameFunctionTests
{
    [TestClass]
    public class CheckMoveTests
    {
        [TestMethod]
        public void CheckTakeDownTest()
        {
            //Associate
            Int64[,] board = new Int64[,] 
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
            Int64 height = 3;
            Int64 width = 4;
            Int64[] listOfPiecesToTake = new Int64[] { 1 };
            Tree tree = new Tree(new TreeTake
            {
                CurrentHeight = height,
                CurrentWidth = width,
            });

            //Act
            Tree resultTree = CheckMove.CheckTakeDown(board, height, width, listOfPiecesToTake, tree);

            //Assert
            Assert.AreEqual(5, resultTree.Left.Value.CurrentHeight);
            Assert.AreEqual(2, resultTree.Left.Value.CurrentWidth);
            Assert.AreEqual(4, resultTree.Left.Value.TakeHeight);
            Assert.AreEqual(3, resultTree.Left.Value.TakeWidth);
            Assert.IsNull(resultTree.Right);
        }

        [TestMethod]
        public void CheckTakeDownTest_DoubleJump()
        {
            //Associate
            Int64[,] board = new Int64[,]
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
            Int64 height = 2;
            Int64 width = 3;
            Int64[] listOfPiecesToTake = new Int64[] { 1 };
            Tree tree = new Tree(new TreeTake
            {
                CurrentHeight = height,
                CurrentWidth = width,
            });

            //Act
            Tree resultTree = CheckMove.CheckTakeDown(board, height, width, listOfPiecesToTake, tree);

            //Assert
            Assert.AreEqual(6, resultTree.Right.Right.Value.CurrentHeight);
            Assert.AreEqual(7, resultTree.Right.Right.Value.CurrentWidth);
        }

        [TestMethod]
        public void CheckKingTake()
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
            Int64 preHeight = 7;
            Int64 preWidth = 2;
            Int64 height = 7;
            Int64 width = 2;
            Int64[] listOfPiecesToTake = new Int64[] { 2, 4 };
            KingTree tree = new KingTree(new TreeTake
            {
                CurrentHeight = height,
                CurrentWidth = width,
            });

            //Act
            KingTree resultTree = CheckMove.CheckKingTake(board, preHeight, preWidth, height, width, listOfPiecesToTake, tree);

            //Assert
            Assert.AreEqual(6, resultTree.UpRight.Value.TakeHeight);
            Assert.AreEqual(4, resultTree.UpRight.Value.CurrentWidth);
        }
    }
}
