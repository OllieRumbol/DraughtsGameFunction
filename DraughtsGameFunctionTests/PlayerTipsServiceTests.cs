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
    }
}
