using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DraughtsGameFunction;
using Microsoft.AspNetCore.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DraughtsGameFunctionTests;

[TestClass]
public class PlayerTipsTests
{
    // Test 2: spec section 5.2 and section 6.2, example 1.
    [TestMethod]
    public async Task Run_WebAppRequest_ReturnsFourSquares()
    {
        var body = $$"""{ "Board": {{TestHttp.StartingBoard}}, "TipFor": 1 }""";

        var response = await PostAsync(body);

        Assert.AreEqual(StatusCodes.Status200OK, response.StatusCode);
        Assert.IsTrue(response.Json.GetProperty("successful").GetBoolean());
        Assert.AreEqual(System.Text.Json.JsonValueKind.Null, response.Json.GetProperty("errorMessage").ValueKind);

        var squares = response.Json.GetProperty("potentialMoves")
            .EnumerateArray()
            .Select(square => (square.GetProperty("height").GetInt64(), square.GetProperty("width").GetInt64()))
            .OrderBy(square => square.Item2)
            .ToArray();

        CollectionAssert.AreEqual(new (long, long)[] { (4, 1), (4, 3), (4, 5), (4, 7) }, squares);
    }

    // Test 4: spec section 6.1.2, rule 2.
    [TestMethod]
    public async Task Run_BoardOfSevenRows_ReturnsBadRequest()
    {
        var body = """{ "Board": [[0,2,0,2,0,2,0,2],[2,0,2,0,2,0,2,0],[0,2,0,2,0,2,0,2],[5,0,5,0,5,0,5,0],[0,5,0,5,0,5,0,5],[1,0,1,0,1,0,1,0],[0,1,0,1,0,1,0,1]], "TipFor": 1 }""";

        var response = await PostAsync(body);

        AssertBadRequest(response, "Board must be 8 rows of 8 values.");
    }

    // Test 5: spec section 6.1.2, rule 3.
    [TestMethod]
    [DataRow(true, DisplayName = "With a Content-Length header")]
    [DataRow(false, DisplayName = "Without a Content-Length header")]
    public async Task Run_BodyOverTenKilobytes_ReturnsBadRequest(bool sendContentLength)
    {
        var body = $$"""{ "Board": {{TestHttp.StartingBoard}}, "TipFor": 1 }""".PadRight(10 * 1024 + 1);

        var response = await PostAsync(body, sendContentLength);

        AssertBadRequest(response, "The request body must be 10 KB or smaller.");
    }

    // Test 29: spec section 6.1.2, rule 1.
    [TestMethod]
    [DataRow("this is not JSON", "The request body is not valid JSON.", DisplayName = "Not JSON")]
    [DataRow("null", "The request body is not valid JSON.", DisplayName = "The JSON value null")]
    [DataRow("""{ "TipFor": 1 }""", "Board must be 8 rows of 8 values.", DisplayName = "No Board")]
    [DataRow("""{ "board": "x", "TipFor": 1 }""", "Board must be 8 rows of 8 values.", DisplayName = "A lower-case board that is not rows")]
    public async Task Run_BodyCannotBeRead_ReturnsBadRequest(string body, string expectedMessage)
    {
        var response = await PostAsync(body);

        AssertBadRequest(response, expectedMessage);
    }

    // Spec section 6.1.2, rule 5: until that rule starts, a missing TipFor returns an empty list.
    [TestMethod]
    public async Task Run_NoTipFor_ReturnsEmptyListAsToday()
    {
        var body = $$"""{ "Board": {{TestHttp.StartingBoard}} }""";

        var response = await PostAsync(body);

        Assert.AreEqual(StatusCodes.Status200OK, response.StatusCode);
        Assert.AreEqual(0, response.Json.GetProperty("potentialMoves").GetArrayLength());
    }

    private static async Task<TestResponse> PostAsync(string body, bool sendContentLength = false)
    {
        var request = TestHttp.CreateRequest(body, sendContentLength);

        var result = await PlayerTips.Run(request, CancellationToken.None);

        return await TestHttp.ExecuteAsync(result, request);
    }

    private static void AssertBadRequest(TestResponse response, string expectedMessage)
    {
        Assert.AreEqual(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.IsFalse(response.Json.GetProperty("successful").GetBoolean());
        Assert.AreEqual(expectedMessage, response.Json.GetProperty("errorMessage").GetString());
    }
}
