using DraughtsGameFunctionModels.Controller;
using DraughtsGameFunctionModels.Service;
using DraughtsGameFunctionService.Instance;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DraughtsGameFunctionTests
{
    [TestClass]
    public class PlayerTipsServiceTests
    {
        [TestMethod]
        public void GetPotentialMovesTest()
        {
            // Associate
            PlayerTipsService playerTipsService = new PlayerTipsService();
            GetPlayerTips getPlayerTips = new GetPlayerTips
            {
                Board = new Int64[,]
                {
                    { 0, 2, 0, 2, 0, 2, 0, 2 },
                    { 2, 0, 2, 0, 2, 0, 2, 0 },
                    { 0, 2, 0, 2, 0, 2, 0, 2 },
                    { 5, 0, 5, 0, 5, 0, 5, 0 },
                    { 0, 5, 0, 5, 0, 5, 0, 5 },
                    { 1, 0, 1, 0, 1, 0, 1, 0 },
                    { 0, 1, 0, 1, 0, 1, 0, 1 },
                    { 1, 0, 1, 0, 1, 0, 1, 0 },
                },
                TipFor = 1
            };

            // Act 
            List<Piece> pieces = playerTipsService.GetPotentialMoves(getPlayerTips);

            // Assert
            Assert.AreEqual(4, pieces.Count);
        }

        // Test 30, example 21: a king's chain can end on its own square, but that square is not a tip (spec 6.1.4, item 4).
        [TestMethod]
        public void GetPotentialMoves_KingChainEndsOnItsOwnSquare_LeavesThatSquareOut()
        {
            // Associate
            PlayerTipsService playerTipsService = new PlayerTipsService();
            GetPlayerTips getPlayerTips = new GetPlayerTips
            {
                Board = new Int64[,]
                {
                    { 0, 5, 0, 5, 0, 5, 0, 5 },
                    { 5, 0, 4, 0, 5, 0, 5, 0 },
                    { 0, 1, 0, 1, 0, 5, 0, 5 },
                    { 5, 0, 5, 0, 5, 0, 5, 0 },
                    { 0, 1, 0, 1, 0, 5, 0, 5 },
                    { 5, 0, 5, 0, 5, 0, 5, 0 },
                    { 0, 5, 0, 5, 0, 5, 0, 5 },
                    { 5, 0, 5, 0, 5, 0, 5, 0 },
                },
                TipFor = 2
            };

            // Act
            List<Piece> pieces = playerTipsService.GetPotentialMoves(getPlayerTips);

            // Assert
            IEnumerable<string> squares = pieces.Select(p => $"({p.Height},{p.Width})").Order();
            Assert.AreEqual("(0,1), (0,3), (3,0), (3,4), (5,2)", string.Join(", ", squares));
        }
    }
}
