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

        TestHttp.AssertBadRequest(response, "Board must be 8 rows of 8 values.");
    }

    // Test 5: spec section 6.1.2, rule 3.
    [TestMethod]
    [DataRow(true, DisplayName = "With a Content-Length header")]
    [DataRow(false, DisplayName = "Without a Content-Length header")]
    public async Task Run_BodyOverTenKilobytes_ReturnsBadRequest(bool sendContentLength)
    {
        var body = $$"""{ "Board": {{TestHttp.StartingBoard}}, "TipFor": 1 }""".PadRight(10 * 1024 + 1);

        var response = await PostAsync(body, sendContentLength);

        TestHttp.AssertBadRequest(response, "The request body must be 10 KB or smaller.");
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

        TestHttp.AssertBadRequest(response, expectedMessage);
    }

    // Test 28: spec section 6.1.2, rule 5.
    [TestMethod]
    [DataRow(""", "TipFor": 3""", DisplayName = "TipFor 3")]
    [DataRow(""", "TipFor": 0""", DisplayName = "TipFor 0")]
    [DataRow(", \"TipFor\": \"abc\"", DisplayName = "TipFor that is not a number")]
    [DataRow(""", "TipFor": true""", DisplayName = "TipFor true")]
    [DataRow(""", "tipfor": 1.5""", DisplayName = "A lower-case TipFor that is not whole")]
    [DataRow("", DisplayName = "No TipFor")]
    public async Task Run_TipForIsNotOneOrTwo_ReturnsBadRequest(string tipFor)
    {
        var body = $$"""{ "Board": {{TestHttp.StartingBoard}}{{tipFor}} }""";

        var response = await PostAsync(body);

        TestHttp.AssertBadRequest(response, "TipFor must be 1 or 2.");
    }

    // Test 28: spec section 6.1.2, rule 4.
    [TestMethod]
    [DataRow(7, DisplayName = "7")]
    [DataRow(-1, DisplayName = "-1")]
    [DataRow(4294967297, DisplayName = "2^32 + 1, which an int cast would read as 1")]
    public async Task Run_BoardValueOutsideZeroToSix_ReturnsBadRequest(long value)
    {
        var board = TestHttp.StartingBoard.Replace("[5,0,5,0,5,0,5,0]", $"[5,0,5,0,{value},0,5,0]");
        var body = $$"""{ "Board": {{board}}, "TipFor": 1 }""";

        var response = await PostAsync(body);

        TestHttp.AssertBadRequest(response, "Board values must be from 0 to 6.");
    }

    // Spec section 6.1.4, item 3: the player 2 man is blocked by two kings, which a man cannot capture.
    [TestMethod]
    public async Task Run_PlayerHasNoLegalMove_ReturnsEmptyList()
    {
        var body = $$"""{ "Board": [[0,5,0,5,0,5,0,5],[5,0,5,0,5,0,5,0],[0,5,0,5,0,5,0,5],[5,0,5,0,5,0,5,0],[0,5,0,5,0,5,0,5],[5,0,5,0,5,0,5,0],[0,2,0,5,0,5,0,5],[3,0,3,0,5,0,5,0]], "TipFor": 2 }""";

        var response = await PostAsync(body);

        Assert.AreEqual(StatusCodes.Status200OK, response.StatusCode);
        Assert.IsTrue(response.Json.GetProperty("successful").GetBoolean());
        Assert.AreEqual(0, response.Json.GetProperty("potentialMoves").GetArrayLength());
    }

    private static async Task<TestResponse> PostAsync(string body, bool sendContentLength = false)
    {
        var request = TestHttp.CreateRequest(body, sendContentLength);

        var result = await PlayerTips.Run(request, CancellationToken.None);

        return await TestHttp.ExecuteAsync(result, request);
    }
}
