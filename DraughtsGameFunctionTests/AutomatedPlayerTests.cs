using System;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DraughtsGameFunction;
using DraughtsGameFunctionModels.Options;
using DraughtsGameFunctionService.Engine;
using DraughtsGameFunctionService.Instance;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static DraughtsGameFunctionTests.EngineBoards;

namespace DraughtsGameFunctionTests;

/// <summary>
/// GetNextMove through the function, from stage 2 (spec section 6.1.1, item 5). The searches use short time limits,
/// because these tests check the contract, not the strength of play.
/// </summary>
[TestClass]
public class AutomatedPlayerTests
{
    private const string DifficultyMessage = "Difficulty must be one of: easy, normal, hard";
    private const string VersionMessage = "Version must be 1, 2 or 3.";

    // The time limits for these tests, short so the tests stay quick.
    private const int TestTimeLimitMs = 100;

    // Player 2 has two moves. The man on (4,1) can step to (5,0), or take the man on (5,2) and land on (6,3).
    // There, kings block both of its moves, and a man cannot capture a king, so player 2 has no moves left and loses
    // at once (spec section 2.1, item 6). Easy always takes the capture. Normal and Hard never do.
    private static readonly string CaptureThatLosesBoard = BoardJson(
        (4, 1, Board.Player2Man), (5, 2, Board.Player1Man), (7, 2, Board.Player1King), (7, 4, Board.Player1King));

    private const string CaptureThatLoses = "(4,1)->(6,3) x(5,2)";
    private const string QuietMove = "(4,1)->(5,0)";

    // Test 1: the web app's exact request (spec section 5.1). It still works in stage 2.
    [TestMethod]
    public async Task Run_WebAppRequest_ReturnsMoveWithCamelCaseFields()
    {
        var body = $$"""{ "Version": 2, "Board": {{TestHttp.StartingBoard}}, "Depth": 4, "Player": 2 }""";

        var response = await PostAsync(body);

        Assert.AreEqual(StatusCodes.Status200OK, response.StatusCode);
        Assert.IsTrue(response.Json.GetProperty("successful").GetBoolean());
        Assert.AreEqual(JsonValueKind.Null, response.Json.GetProperty("errorMessage").ValueKind);

        var nextMove = response.Json.GetProperty("nextMove");
        Assert.AreEqual(2, nextMove.GetProperty("currentHeight").GetInt64());
        Assert.AreEqual(3, nextMove.GetProperty("nextHeight").GetInt64());
        Assert.IsTrue(nextMove.TryGetProperty("currentWidth", out _));
        Assert.IsTrue(nextMove.TryGetProperty("nextWidth", out _));
        Assert.AreEqual(0, nextMove.GetProperty("takes").GetArrayLength());
    }

    // Spec section 6.1.1, item 2: both ways to send the level work with lower-case names.
    [TestMethod]
    [DataRow("\"difficulty\": \"easy\"", DisplayName = "difficulty")]
    [DataRow("\"version\": 1", DisplayName = "version, with no difficulty")]
    public async Task Run_LowerCaseFieldNames_ReturnsMove(string level)
    {
        var body = $$"""{ {{level}}, "board": {{TestHttp.StartingBoard}} }""";

        var response = await PostAsync(body);

        Assert.AreEqual(StatusCodes.Status200OK, response.StatusCode);
        Assert.IsTrue(response.Json.GetProperty("successful").GetBoolean());
    }

    // Test 27 and spec section 6.2, example 2.
    [TestMethod]
    public async Task Run_DifficultyNormal_ReturnsLegalPlayerTwoMoveFromRowTwoToRowThree()
    {
        var body = $$"""{ "Difficulty": "normal", "Board": {{TestHttp.StartingBoard}} }""";
        var legalMoves = Generate(Board.FromRows(StartingRows()), player: 2).Select(Describe).ToList();

        var response = await PostAsync(body);

        Assert.AreEqual(StatusCodes.Status200OK, response.StatusCode);
        var move = MoveText(response);
        CollectionAssert.Contains(legalMoves, move);
        StringAssert.StartsWith(move, "(2,");
        Assert.AreEqual(3, response.Json.GetProperty("nextMove").GetProperty("nextHeight").GetInt64());
    }

    // Test 21 and spec section 6.2, example 10: letter case is ignored.
    [TestMethod]
    [DataRow("HARD")]
    [DataRow("Easy")]
    [DataRow("nOrMaL")]
    public async Task Run_DifficultyInAnyLetterCase_ReturnsMove(string difficulty)
    {
        var body = $$"""{ "Difficulty": "{{difficulty}}", "Board": {{TestHttp.StartingBoard}} }""";

        var response = await PostAsync(body);

        Assert.AreEqual(StatusCodes.Status200OK, response.StatusCode);
    }

    // Test 21 and spec section 6.2, examples 11 and 12, and spec section 6.1.1, item 7: only the three names.
    [TestMethod]
    [DataRow("\"Difficulty\": 3", DisplayName = "A number (example 11)")]
    [DataRow("\"difficulty\": 3", DisplayName = "A number, lower-case name")]
    [DataRow("\"Difficulty\": \"extreme\"", DisplayName = "Another name (example 12)")]
    [DataRow("\"Difficulty\": \"easy,hard\"", DisplayName = "Two names")]
    [DataRow("\"Difficulty\": \" hard \"", DisplayName = "A name with spaces")]
    [DataRow("\"Difficulty\": \"2\"", DisplayName = "A number as a string")]
    [DataRow("\"Difficulty\": true", DisplayName = "true")]
    public async Task Run_DifficultyNotAllowed_ReturnsBadRequestListingNames(string difficulty)
    {
        var body = $$"""{ {{difficulty}}, "Board": {{TestHttp.StartingBoard}} }""";

        var response = await PostAsync(body);

        TestHttp.AssertBadRequest(response, DifficultyMessage);
    }

    // Test 21 and spec section 6.2, example 13: when both are sent, Difficulty is used. Easy takes the capture, and
    // the Hard control does not.
    [TestMethod]
    [DataRow("easy", 3, CaptureThatLoses, DisplayName = "Difficulty easy, Version 3")]
    [DataRow("hard", 1, QuietMove, DisplayName = "Difficulty hard, Version 1")]
    [DataRow("hard", 9, QuietMove, DisplayName = "Difficulty hard, Version 9 is not checked")]
    public async Task Run_DifficultyAndVersion_UsesDifficulty(string difficulty, int version, string expectedMove)
    {
        var body = $$"""{ "Difficulty": "{{difficulty}}", "Version": {{version}}, "Board": {{CaptureThatLosesBoard}} }""";

        var response = await PostAsync(body);

        Assert.AreEqual(StatusCodes.Status200OK, response.StatusCode);
        Assert.AreEqual(expectedMove, MoveText(response));
    }

    // Spec section 6.1.1, item 8: with no Difficulty, Version gives the level, as the web app sends it today.
    [TestMethod]
    [DataRow(1, CaptureThatLoses, DisplayName = "Version 1 is Easy")]
    [DataRow(2, QuietMove, DisplayName = "Version 2 is Normal")]
    [DataRow(3, QuietMove, DisplayName = "Version 3 is Hard")]
    public async Task Run_VersionWithoutDifficulty_PlaysThatLevel(int version, string expectedMove)
    {
        var body = $$"""{ "Version": {{version}}, "Board": {{CaptureThatLosesBoard}} }""";

        var response = await PostAsync(body);

        Assert.AreEqual(StatusCodes.Status200OK, response.StatusCode);
        Assert.AreEqual(expectedMove, MoveText(response));
    }

    // Test 21 and spec section 6.2, example 14. A Difficulty of null counts as missing.
    [TestMethod]
    [DataRow("", DisplayName = "Neither field")]
    [DataRow("\"Difficulty\": null, ", DisplayName = "Difficulty null")]
    public async Task Run_NeitherDifficultyNorVersion_ReturnsBadRequestSayingItIsMissing(string fields)
    {
        var body = $$"""{ {{fields}}"Board": {{TestHttp.StartingBoard}} }""";

        var response = await PostAsync(body);

        TestHttp.AssertBadRequest(response, AutomatedPlayer.MissingDifficultyMessage);
    }

    // Test 3 and spec section 6.2, example 6: an unknown Version names the allowed values.
    [TestMethod]
    [DataRow(9)]
    [DataRow(0)]
    [DataRow(-1)]
    public async Task Run_UnknownVersion_ReturnsBadRequestWithSameShape(int version)
    {
        var body = $$"""{ "Version": {{version}}, "Board": {{TestHttp.StartingBoard}} }""";

        var response = await PostAsync(body);

        TestHttp.AssertBadRequest(response, VersionMessage);
        Assert.AreEqual(JsonValueKind.Null, response.Json.GetProperty("nextMove").ValueKind);
    }

    // Test 21 and spec section 6.1.1, item 8: a Version that is not a whole number is rejected, even when Difficulty
    // is valid, because the body cannot be read.
    [TestMethod]
    [DataRow("\"abc\"", DisplayName = "Not a number")]
    [DataRow("\"2\"", DisplayName = "A number as a string")]
    [DataRow("2.5", DisplayName = "Not whole")]
    public async Task Run_VersionNotAWholeNumber_ReturnsBadRequestEvenWithDifficulty(string version)
    {
        var body = $$"""{ "Difficulty": "normal", "Version": {{version}}, "Board": {{TestHttp.StartingBoard}} }""";

        var response = await PostAsync(body);

        TestHttp.AssertBadRequest(response, VersionMessage);
    }

    // Test 26 and spec section 6.1.5, item 1, and section 6.5, item 5: Player has no effect. Every level plays player
    // 2, so the move starts on row 2 of the starting board, where player 1 has no pieces.
    [TestMethod]
    [DataRow("easy", "1")]
    [DataRow("normal", "1")]
    [DataRow("hard", "1")]
    [DataRow("easy", "-7")]
    [DataRow("easy", "\"one\"")]
    public async Task Run_AnyPlayer_PlaysPlayerTwo(string difficulty, string player)
    {
        var body = $$"""{ "Difficulty": "{{difficulty}}", "Board": {{TestHttp.StartingBoard}}, "Player": {{player}} }""";

        var response = await PostAsync(body);

        Assert.AreEqual(StatusCodes.Status200OK, response.StatusCode);
        Assert.AreEqual(2, response.Json.GetProperty("nextMove").GetProperty("currentHeight").GetInt64());
    }

    // Spec section 6.5, item 5: Depth has no effect, with any value or none. The owner's T12 rule for Depth below 1
    // ended with stage 2.
    [TestMethod]
    [DataRow(""", "Depth": 0""", DisplayName = "Depth 0")]
    [DataRow(""", "Depth": -1""", DisplayName = "Depth -1")]
    [DataRow("", DisplayName = "No Depth")]
    [DataRow(", \"Depth\": \"abc\"", DisplayName = "Depth that is not a number")]
    [DataRow(""", "Depth": 9999999999""", DisplayName = "A very large Depth")]
    public async Task Run_AnyDepth_HasNoEffect(string depth)
    {
        var body = $$"""{ "Version": 2, "Board": {{TestHttp.StartingBoard}}{{depth}}, "Player": 2 }""";

        var response = await PostAsync(body);

        Assert.AreEqual(StatusCodes.Status200OK, response.StatusCode);
    }

    // Test 22 and spec section 6.2, example 7: the web app's request at Version 3 with Depth 50 returns quickly,
    // because Depth is not read. The old code searched to the requested depth, which took minutes. These tests set
    // the Hard limit to 100 ms, and the allowance is generous, so a slow build machine does not fail the test.
    // ComputerPlayerTests check the time limit itself, and T18 measures the production limit on the live app.
    [TestMethod]
    public async Task Run_HardWithDepthFifty_ReturnsWithinTimeLimit()
    {
        var body = $$"""{ "Version": 3, "Board": {{TestHttp.StartingBoard}}, "Depth": 50, "Player": 2 }""";
        var stopwatch = Stopwatch.StartNew();

        var response = await PostAsync(body);

        Assert.AreEqual(StatusCodes.Status200OK, response.StatusCode);
        Assert.IsLessThan(TestTimeLimitMs + 2000, stopwatch.ElapsedMilliseconds);
    }

    // Spec section 6.2, example 15: with one legal move, Normal and Hard return it.
    [TestMethod]
    [DataRow("normal")]
    [DataRow("hard")]
    public async Task Run_OneLegalMove_ReturnsIt(string difficulty)
    {
        var board = BoardJson((5, 0, Board.Player2Man), (2, 7, Board.Player1Man));
        var body = $$"""{ "Difficulty": "{{difficulty}}", "Board": {{board}} }""";

        var response = await PostAsync(body);

        Assert.AreEqual(StatusCodes.Status200OK, response.StatusCode);
        Assert.AreEqual("(5,0)->(6,1)", MoveText(response));
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
    [DataRow("\"Version\": 1", NoPlayerTwoPieces, DisplayName = "Easy, no player 2 pieces")]
    [DataRow("\"Version\": 2", NoPlayerTwoPieces, DisplayName = "Normal, no player 2 pieces")]
    [DataRow("\"Version\": 3", NoPlayerTwoPieces, DisplayName = "Hard, no player 2 pieces")]
    [DataRow("\"Difficulty\": \"hard\"", NoPlayerTwoPieces, DisplayName = "Difficulty hard, no player 2 pieces")]
    [DataRow("\"Version\": 2", "[[0,5,0,5,0,5,0,5],[5,0,5,0,5,0,5,0],[0,5,0,5,0,5,0,5],[5,0,5,0,5,0,5,0],[0,5,0,5,0,5,0,5],[5,0,5,0,5,0,5,0],[0,2,0,5,0,5,0,5],[3,0,3,0,5,0,5,0]]", DisplayName = "Normal, a player 2 man blocked by two kings")]
    public async Task Run_PlayerTwoHasNoLegalMove_ReturnsBadRequest(string level, string board)
    {
        var body = $$"""{ {{level}}, "Board": {{board}}, "Depth": 4, "Player": 2 }""";

        var response = await PostAsync(body);

        TestHttp.AssertBadRequest(response, "Player 2 has no legal moves.");
    }

    // Test 26: in stage 1, Easy played as the request's Player. From stage 2 it plays player 2, so Player 1 no longer
    // finds player 1's moves when player 2 has none.
    [TestMethod]
    public async Task Run_EasyWithPlayerOneWhenPlayerTwoHasNoMove_ReturnsBadRequest()
    {
        var body = $$"""{ "Version": 1, "Board": {{NoPlayerTwoPieces}}, "Player": 1 }""";

        var response = await PostAsync(body);

        TestHttp.AssertBadRequest(response, "Player 2 has no legal moves.");
    }

    // Test 28: spec section 6.1.2, rule 4.
    [TestMethod]
    public async Task Run_BoardValueOutsideZeroToSix_ReturnsBadRequest()
    {
        var body = """{ "Version": 2, "Board": [[0,2,0,2,0,2,0,2],[2,0,2,0,2,0,2,0],[0,2,0,2,0,2,0,2],[5,0,5,0,7,0,5,0],[0,5,0,5,0,5,0,5],[1,0,1,0,1,0,1,0],[0,1,0,1,0,1,0,1],[1,0,1,0,1,0,1,0]], "Depth": 4, "Player": 2 }""";

        var response = await PostAsync(body);

        TestHttp.AssertBadRequest(response, "Board values must be from 0 to 6.");
    }

    // A local run showed that a client which disconnects during the upload causes this exception.
    [TestMethod]
    public async Task Run_ClientStopsSendingTheBody_ReturnsBadRequest()
    {
        var request = TestHttp.CreateRequest(string.Empty);
        request.Body = new StoppedBodyStream();

        var result = await NewFunction().Run(request, CancellationToken.None);
        var response = await TestHttp.ExecuteAsync(result, request);

        TestHttp.AssertBadRequest(response, "The request body is not valid JSON.");
    }

    private const string NoPlayerTwoPieces =
        "[[0,5,0,5,0,5,0,5],[5,0,5,0,5,0,5,0],[0,5,0,5,0,5,0,5],[5,0,5,0,5,0,5,0],[0,5,0,5,0,5,0,5],[1,0,1,0,1,0,1,0],[0,1,0,1,0,1,0,1],[1,0,1,0,1,0,1,0]]";

    private sealed class StoppedBodyStream : System.IO.MemoryStream
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            throw new BadHttpRequestException("Unexpected end of request content.");
        }
    }

    private static AutomatedPlayer NewFunction()
    {
        var settings = new SearchOptions { NormalTimeLimitMs = TestTimeLimitMs, HardTimeLimitMs = TestTimeLimitMs };

        return new AutomatedPlayer(new ComputerPlayer(Options.Create(settings), TimeProvider.System, new Random(17)));
    }

    private static string PaddedBody(int sizeInBytes)
    {
        var body = $$"""{ "Version": 1, "Board": {{TestHttp.StartingBoard}}, "Player": 2 }""";

        return body.PadRight(sizeInBytes);
    }

    // The board as JSON rows, with the given pieces and every other dark square empty.
    private static string BoardJson(params (int Row, int Column, int Value)[] pieces)
    {
        var board = Create(pieces);
        var rows = Enumerable.Range(0, Board.Size)
            .Select(row => "[" + string.Join(",", board.Skip(row * Board.Size).Take(Board.Size)) + "]");

        return "[" + string.Join(",", rows) + "]";
    }

    // The returned move in the form EngineBoards.Describe uses, for example "(2,1)->(4,3) x(3,2)".
    private static string MoveText(TestResponse response)
    {
        var move = response.Json.GetProperty("nextMove");
        var text = $"({move.GetProperty("currentHeight").GetInt64()},{move.GetProperty("currentWidth").GetInt64()})"
            + $"->({move.GetProperty("nextHeight").GetInt64()},{move.GetProperty("nextWidth").GetInt64()})";
        var takes = string.Concat(move.GetProperty("takes").EnumerateArray()
            .Select(piece => $"({piece.GetProperty("height").GetInt64()},{piece.GetProperty("width").GetInt64()})"));

        return takes.Length == 0 ? text : $"{text} x{takes}";
    }

    private static async Task<TestResponse> PostAsync(string body, bool sendContentLength = false)
    {
        var request = TestHttp.CreateRequest(body, sendContentLength);

        var result = await NewFunction().Run(request, CancellationToken.None);

        return await TestHttp.ExecuteAsync(result, request);
    }
}
