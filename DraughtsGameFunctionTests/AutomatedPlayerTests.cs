using System.Threading;
using System.Threading.Tasks;
using DraughtsGameFunction;
using Microsoft.AspNetCore.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DraughtsGameFunctionTests;

[TestClass]
public class AutomatedPlayerTests
{
    // Test 1: the web app's exact request (spec section 5.1).
    [TestMethod]
    public async Task Run_WebAppRequest_ReturnsMoveWithCamelCaseFields()
    {
        var body = $$"""{ "Version": 2, "Board": {{TestHttp.StartingBoard}}, "Depth": 4, "Player": 2 }""";

        var response = await PostAsync(body);

        Assert.AreEqual(StatusCodes.Status200OK, response.StatusCode);
        Assert.IsTrue(response.Json.GetProperty("successful").GetBoolean());
        Assert.AreEqual(System.Text.Json.JsonValueKind.Null, response.Json.GetProperty("errorMessage").ValueKind);

        var nextMove = response.Json.GetProperty("nextMove");
        Assert.AreEqual(2, nextMove.GetProperty("currentHeight").GetInt64());
        Assert.AreEqual(3, nextMove.GetProperty("nextHeight").GetInt64());
        Assert.IsTrue(nextMove.TryGetProperty("currentWidth", out _));
        Assert.IsTrue(nextMove.TryGetProperty("nextWidth", out _));
        Assert.AreEqual(0, nextMove.GetProperty("takes").GetArrayLength());
    }

    // Spec section 6.1.1, item 2.
    [TestMethod]
    public async Task Run_LowerCaseFieldNames_ReturnsMove()
    {
        var body = $$"""{ "version": 1, "board": {{TestHttp.StartingBoard}}, "depth": 0, "player": 2 }""";

        var response = await PostAsync(body);

        Assert.AreEqual(StatusCodes.Status200OK, response.StatusCode);
        Assert.IsTrue(response.Json.GetProperty("successful").GetBoolean());
    }

    // Test 3: spec section 5.1.
    [TestMethod]
    public async Task Run_UnknownVersion_ReturnsBadRequestWithSameShape()
    {
        var body = $$"""{ "Version": 9, "Board": {{TestHttp.StartingBoard}} }""";

        var response = await PostAsync(body);

        TestHttp.AssertBadRequest(response, "No version in body of request");
        Assert.AreEqual(System.Text.Json.JsonValueKind.Null, response.Json.GetProperty("nextMove").ValueKind);
    }

    // Test 4: spec section 6.1.2, rule 2.
    [TestMethod]
    [DataRow("[[0,2,0,2,0,2,0,2],[2,0,2,0,2,0,2,0],[0,2,0,2,0,2,0,2],[5,0,5,0,5,0,5,0],[0,5,0,5,0,5,0,5],[1,0,1,0,1,0,1,0],[0,1,0,1,0,1,0,1]]", DisplayName = "7 rows")]
    [DataRow("[[0,2,0,2,0,2,0,2],[2,0,2,0,2,0,2,0],[0,2,0,2,0,2,0,2],[5,0,5,0,5,0,5,0],[0,5,0,5,0,5,0,5],[1,0,1,0,1,0,1,0],[0,1,0,1,0,1,0,1],[1,0,1,0,1,0,1,0],[1,0,1,0,1,0,1,0]]", DisplayName = "9 rows")]
    [DataRow("[[0,2,0,2,0,2,0],[2,0,2,0,2,0,2,0],[0,2,0,2,0,2,0,2],[5,0,5,0,5,0,5,0],[0,5,0,5,0,5,0,5],[1,0,1,0,1,0,1,0],[0,1,0,1,0,1,0,1],[1,0,1,0,1,0,1,0]]", DisplayName = "A short row")]
    [DataRow("[[0,2,0,2,0,2,0,2,0],[2,0,2,0,2,0,2,0],[0,2,0,2,0,2,0,2],[5,0,5,0,5,0,5,0],[0,5,0,5,0,5,0,5],[1,0,1,0,1,0,1,0],[0,1,0,1,0,1,0,1],[1,0,1,0,1,0,1,0]]", DisplayName = "A long row")]
    [DataRow("[[0,\"x\",0,2,0,2,0,2],[2,0,2,0,2,0,2,0],[0,2,0,2,0,2,0,2],[5,0,5,0,5,0,5,0],[0,5,0,5,0,5,0,5],[1,0,1,0,1,0,1,0],[0,1,0,1,0,1,0,1],[1,0,1,0,1,0,1,0]]", DisplayName = "A value that is not a number")]
    [DataRow("[[0,1.5,0,2,0,2,0,2],[2,0,2,0,2,0,2,0],[0,2,0,2,0,2,0,2],[5,0,5,0,5,0,5,0],[0,5,0,5,0,5,0,5],[1,0,1,0,1,0,1,0],[0,1,0,1,0,1,0,1],[1,0,1,0,1,0,1,0]]", DisplayName = "A value that is not whole")]
    public async Task Run_BoardIsNotEightByEight_ReturnsBadRequest(string board)
    {
        var body = $$"""{ "Version": 2, "Board": {{board}}, "Depth": 1, "Player": 2 }""";

        var response = await PostAsync(body);

        TestHttp.AssertBadRequest(response, "Board must be 8 rows of 8 values.");
    }

    // Test 5: spec section 6.1.2, rule 3. Padding keeps the body valid JSON, so only its size can fail it.
    [TestMethod]
    [DataRow(true, DisplayName = "With a Content-Length header")]
    [DataRow(false, DisplayName = "Without a Content-Length header")]
    public async Task Run_BodyOverTenKilobytes_ReturnsBadRequest(bool sendContentLength)
    {
        var body = PaddedBody(sizeInBytes: 10 * 1024 + 1);

        var response = await PostAsync(body, sendContentLength);

        TestHttp.AssertBadRequest(response, "The request body must be 10 KB or smaller.");
    }

    [TestMethod]
    public async Task Run_BodyOfExactlyTenKilobytes_IsAccepted()
    {
        var body = PaddedBody(sizeInBytes: 10 * 1024);

        var response = await PostAsync(body);

        Assert.AreEqual(StatusCodes.Status200OK, response.StatusCode);
    }

    // Test 29: spec section 6.1.2, rule 1.
    [TestMethod]
    [DataRow("this is not JSON", "The request body is not valid JSON.", DisplayName = "Not JSON")]
    [DataRow("", "The request body is not valid JSON.", DisplayName = "Empty body")]
    [DataRow("null", "The request body is not valid JSON.", DisplayName = "The JSON value null")]
    [DataRow("[]", "The request body is not valid JSON.", DisplayName = "A JSON array")]
    [DataRow("""{ "Version": "abc", "Board": [] }""", "The request body is not valid JSON.", DisplayName = "A Version that is not a number")]
    [DataRow("""{ "Version": 2, "Board": [[0,2,""", "The request body is not valid JSON.", DisplayName = "A body cut off inside the board")]
    [DataRow("""{ "Version": 2 }""", "Board must be 8 rows of 8 values.", DisplayName = "No Board")]
    [DataRow("""{ "Version": 2, "Board": null }""", "Board must be 8 rows of 8 values.", DisplayName = "A null Board")]
    [DataRow("""{ "Version": 2, "board": "x" }""", "Board must be 8 rows of 8 values.", DisplayName = "A lower-case board that is not rows")]
    public async Task Run_BodyCannotBeRead_ReturnsBadRequest(string body, string expectedMessage)
    {
        var response = await PostAsync(body);

        TestHttp.AssertBadRequest(response, expectedMessage);
    }

    // Test 25: spec section 6.2, example 5, and section 6.5, item 2 (defect 2).
    [TestMethod]
    [DataRow(1, "[[0,5,0,5,0,5,0,5],[5,0,5,0,5,0,5,0],[0,5,0,5,0,5,0,5],[5,0,5,0,5,0,5,0],[0,5,0,5,0,5,0,5],[1,0,1,0,1,0,1,0],[0,1,0,1,0,1,0,1],[1,0,1,0,1,0,1,0]]", DisplayName = "Easy, no player 2 pieces")]
    [DataRow(2, "[[0,5,0,5,0,5,0,5],[5,0,5,0,5,0,5,0],[0,5,0,5,0,5,0,5],[5,0,5,0,5,0,5,0],[0,5,0,5,0,5,0,5],[1,0,1,0,1,0,1,0],[0,1,0,1,0,1,0,1],[1,0,1,0,1,0,1,0]]", DisplayName = "Normal, no player 2 pieces")]
    [DataRow(3, "[[0,5,0,5,0,5,0,5],[5,0,5,0,5,0,5,0],[0,5,0,5,0,5,0,5],[5,0,5,0,5,0,5,0],[0,5,0,5,0,5,0,5],[1,0,1,0,1,0,1,0],[0,1,0,1,0,1,0,1],[1,0,1,0,1,0,1,0]]", DisplayName = "Hard, no player 2 pieces")]
    [DataRow(2, "[[0,5,0,5,0,5,0,5],[5,0,5,0,5,0,5,0],[0,5,0,5,0,5,0,5],[5,0,5,0,5,0,5,0],[0,5,0,5,0,5,0,5],[5,0,5,0,5,0,5,0],[0,2,0,5,0,5,0,5],[3,0,3,0,5,0,5,0]]", DisplayName = "Normal, a player 2 man blocked by two kings")]
    public async Task Run_PlayerTwoHasNoLegalMove_ReturnsBadRequest(int version, string board)
    {
        var body = $$"""{ "Version": {{version}}, "Board": {{board}}, "Depth": 4, "Player": 2 }""";

        var response = await PostAsync(body);

        TestHttp.AssertBadRequest(response, "Player 2 has no legal moves.");
    }

    // Until step 3, Easy plays as the request's Player (spec section 6.1.5, item 1), so player 2 having no move does
    // not stop Easy playing as player 1.
    [TestMethod]
    public async Task Run_EasyAsPlayerOneWhenPlayerTwoHasNoMove_ReturnsMove()
    {
        var body = """{ "Version": 1, "Board": [[0,5,0,5,0,5,0,5],[5,0,5,0,5,0,5,0],[0,5,0,5,0,5,0,5],[5,0,5,0,5,0,5,0],[0,5,0,5,0,5,0,5],[1,0,1,0,1,0,1,0],[0,1,0,1,0,1,0,1],[1,0,1,0,1,0,1,0]], "Player": 1 }""";

        var response = await PostAsync(body);

        Assert.AreEqual(StatusCodes.Status200OK, response.StatusCode);
        Assert.AreEqual(5, response.Json.GetProperty("nextMove").GetProperty("currentHeight").GetInt64());
    }

    // Test 28: spec section 6.1.2, rule 4.
    [TestMethod]
    public async Task Run_BoardValueOutsideZeroToSix_ReturnsBadRequest()
    {
        var body = $$"""{ "Version": 2, "Board": [[0,2,0,2,0,2,0,2],[2,0,2,0,2,0,2,0],[0,2,0,2,0,2,0,2],[5,0,5,0,7,0,5,0],[0,5,0,5,0,5,0,5],[1,0,1,0,1,0,1,0],[0,1,0,1,0,1,0,1],[1,0,1,0,1,0,1,0]], "Depth": 4, "Player": 2 }""";

        var response = await PostAsync(body);

        TestHttp.AssertBadRequest(response, "Board values must be from 0 to 6.");
    }

    // Plan section 4: a search of depth 0 has no move to return (defect 2). The owner approved this on 2026-11-10.
    [TestMethod]
    [DataRow(2, """, "Depth": 0""", DisplayName = "Normal, Depth 0")]
    [DataRow(3, """, "Depth": -1""", DisplayName = "Hard, Depth -1")]
    [DataRow(2, "", DisplayName = "Normal, no Depth")]
    public async Task Run_SearchWithDepthBelowOne_ReturnsBadRequest(int version, string depth)
    {
        var body = $$"""{ "Version": {{version}}, "Board": {{TestHttp.StartingBoard}}{{depth}}, "Player": 2 }""";

        var response = await PostAsync(body);

        TestHttp.AssertBadRequest(response, "Depth must be 1 or more.");
    }

    // A local run showed that a client which disconnects during the upload causes this exception.
    [TestMethod]
    public async Task Run_ClientStopsSendingTheBody_ReturnsBadRequest()
    {
        var request = TestHttp.CreateRequest(string.Empty);
        request.Body = new StoppedBodyStream();

        var result = await AutomatedPlayer.Run(request, CancellationToken.None);
        var response = await TestHttp.ExecuteAsync(result, request);

        TestHttp.AssertBadRequest(response, "The request body is not valid JSON.");
    }

    private sealed class StoppedBodyStream : System.IO.MemoryStream
    {
        public override System.Threading.Tasks.ValueTask<int> ReadAsync(System.Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            throw new BadHttpRequestException("Unexpected end of request content.");
        }
    }

    private static string PaddedBody(int sizeInBytes)
    {
        var body = $$"""{ "Version": 1, "Board": {{TestHttp.StartingBoard}}, "Player": 2 }""";

        return body.PadRight(sizeInBytes);
    }

    private static async Task<TestResponse> PostAsync(string body, bool sendContentLength = false)
    {
        var request = TestHttp.CreateRequest(body, sendContentLength);

        var result = await AutomatedPlayer.Run(request, CancellationToken.None);

        return await TestHttp.ExecuteAsync(result, request);
    }
}
