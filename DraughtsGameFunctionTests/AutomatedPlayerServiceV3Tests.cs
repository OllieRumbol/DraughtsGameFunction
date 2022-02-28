using DraughtsGameFunctionService.Intstance;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DraughtsGameFunctionTests
{
    [TestClass]
    public class AutomatedPlayerServiceV3Tests
    {
        [TestMethod]
        public void EvaluateTest()
        {
            // Associate
            Int64[,] board = new Int64[,]
            {
                { 0, 2, 0, 2, 0, 2, 0, 2 },
                { 2, 0, 2, 0, 2, 0, 2, 0 },
                { 0, 2, 0, 2, 0, 2, 0, 2 },
                { 5, 0, 5, 0, 5, 0, 5, 0 },
                { 0, 5, 0, 5, 0, 5, 0, 5 },
                { 1, 0, 1, 0, 1, 0, 1, 0 },
                { 0, 1, 0, 1, 0, 1, 0, 1 },
                { 1, 0, 1, 0, 1, 0, 1, 0 }
            };
            AutomatedPlayerServiceV3 automatedPlayerServiceV3 = new AutomatedPlayerServiceV3();
            Int64 expectedResult = 0;

            // Act
            Int64 actualResult = automatedPlayerServiceV3.Evaluate(board);

            // Assert
            Assert.AreEqual(expectedResult, actualResult);
        }
    }
}
