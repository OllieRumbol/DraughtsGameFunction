using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using DraughtsGameFunction.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DraughtsGameFunctionTests;

/// <summary>
/// Spec section 6.4, item 6, and plan section 5. These tests call LogRequest, because GetInvocationResult
/// cannot run on a fake context. A local func start run covers Invoke.
/// </summary>
[TestClass]
public class RequestLoggingMiddlewareTests
{
    [TestMethod]
    public void LogRequest_FunctionReturnedAStatus_LogsThatStatus()
    {
        var logger = new FakeLogger<RequestLoggingMiddleware>();
        var clock = new FakeTimeProvider();
        var middleware = new RequestLoggingMiddleware(logger, clock);
        var httpContext = CreateHttpContext("/api/GetNextMove");
        var startTimestamp = clock.GetTimestamp();
        clock.Advance(TimeSpan.FromMilliseconds(250));

        // The result has not been written yet, so the response still says 200, as it does on the real host.
        var functionResult = Results.Json(new { }, statusCode: StatusCodes.Status400BadRequest);
        middleware.LogRequest(httpContext, functionResult, startTimestamp);

        var record = logger.Collector.GetSnapshot().Single();
        Assert.AreEqual(LogLevel.Information, record.Level);
        Assert.AreEqual("/api/GetNextMove returned 400 in 250 ms", record.Message);

        // The plan's response-time query reads these named values.
        Assert.AreEqual("/api/GetNextMove", record.GetStructuredStateValue("Route"));
        Assert.AreEqual("400", record.GetStructuredStateValue("StatusCode"));
        Assert.AreEqual("250", record.GetStructuredStateValue("DurationMs"));
    }

    // A successful function returns a result with no status of its own.
    [TestMethod]
    public void LogRequest_FunctionReturnedNoStatus_LogsTheResponseStatus()
    {
        var logger = new FakeLogger<RequestLoggingMiddleware>();
        var middleware = new RequestLoggingMiddleware(logger, new FakeTimeProvider());
        var httpContext = CreateHttpContext("/api/GetPotentialMoves");

        middleware.LogRequest(httpContext, Results.Json(new { }), startTimestamp: 0);

        Assert.AreEqual("200", logger.Collector.GetSnapshot().Single().GetStructuredStateValue("StatusCode"));
    }

    // The exception middleware writes its 500 directly, so the function has no result.
    [TestMethod]
    public void LogRequest_NoFunctionResult_LogsTheResponseStatus()
    {
        var logger = new FakeLogger<RequestLoggingMiddleware>();
        var middleware = new RequestLoggingMiddleware(logger, new FakeTimeProvider());
        var httpContext = CreateHttpContext("/api/GetNextMove");
        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        middleware.LogRequest(httpContext, functionResult: null, startTimestamp: 0);

        Assert.AreEqual("500", logger.Collector.GetSnapshot().Single().GetStructuredStateValue("StatusCode"));
    }

    // A wrong category name here would remove the request line without any error.
    [TestMethod]
    public void AppSettings_LogLevels_KeepOnlyTheRequestLineAtInformation()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        var levels = JsonDocument.Parse(File.ReadAllText(path)).RootElement.GetProperty("Logging").GetProperty("LogLevel");

        Assert.AreEqual("Warning", levels.GetProperty("Default").GetString());
        Assert.AreEqual("Information", levels.GetProperty(typeof(RequestLoggingMiddleware).FullName!).GetString()); // FullName is null only for generic parameters.
    }

    private static DefaultHttpContext CreateHttpContext(string path)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = path;

        return httpContext;
    }
}
