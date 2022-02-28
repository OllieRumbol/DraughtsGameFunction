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
    public class AutomatedPlayerServiceV2Tests
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
            AutomatedPlayerServiceV2 automatedPlayerServiceV2 = new AutomatedPlayerServiceV2();
            Int64 expectedResult = 0;

            // Act
            Int64 actualResult = automatedPlayerServiceV2.Evaluate(board);

            // Assert
            Assert.AreEqual(expectedResult, actualResult);  
        }

        [TestMethod]
        public void GetAvailableBoardsTest()
        {
            // Associate

            // Act

            // Assert
        }
    }
}
