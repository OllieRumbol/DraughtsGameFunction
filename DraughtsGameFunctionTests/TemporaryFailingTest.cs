using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DraughtsGameFunctionTests;

// Temporary, for the T3 Verify. It is removed in the next commit.
[TestClass]
public class TemporaryFailingTest
{
    [TestMethod]
    public void Check_MustFailTheBuild() => Assert.Fail("T3 Verify: this test must fail the check.");
}
