using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DraughtsGameFunction.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DraughtsGameFunctionTests;

/// <summary>
/// Test 6: spec section 6.3 and plan section 4.
/// </summary>
[TestClass]
public class ExceptionHandlingMiddlewareTests
{
    private const string InternalDetail = "Internal detail that must not reach the caller";

    [TestMethod]
    public async Task Invoke_FunctionThrows_LogsErrorAndReturns500WithNoInternalDetail()
    {
        var logger = new FakeLogger<ExceptionHandlingMiddleware>();
        var middleware = new ExceptionHandlingMiddleware(logger);
        var httpContext = TestHttp.CreateRequest(string.Empty).HttpContext;
        var context = new FakeFunctionContext(httpContext);

        await middleware.Invoke(context, ThrowUnexpectedError);

        var error = logger.Collector.GetSnapshot().Single(record => record.Level == LogLevel.Error);
        Assert.IsInstanceOfType<InvalidOperationException>(error.Exception);

        var body = ReadBody(httpContext);
        Assert.AreEqual(StatusCodes.Status500InternalServerError, httpContext.Response.StatusCode);
        Assert.IsFalse(body.Contains(InternalDetail, StringComparison.Ordinal));
        Assert.IsFalse(body.Contains(nameof(InvalidOperationException), StringComparison.Ordinal));

        var json = JsonDocument.Parse(body).RootElement;
        Assert.IsFalse(json.GetProperty("successful").GetBoolean());
        Assert.AreEqual(ExceptionHandlingMiddleware.UnexpectedErrorMessage, json.GetProperty("errorMessage").GetString());
    }

    // A timeout also throws OperationCanceledException, but the caller did not cancel, so it is an error.
    [TestMethod]
    public async Task Invoke_CancellationThatCallerDidNotAskFor_Returns500()
    {
        var logger = new FakeLogger<ExceptionHandlingMiddleware>();
        var middleware = new ExceptionHandlingMiddleware(logger);
        var httpContext = TestHttp.CreateRequest(string.Empty).HttpContext;
        var context = new FakeFunctionContext(httpContext);

        await middleware.Invoke(context, _ => throw new TaskCanceledException("Timed out."));

        Assert.AreEqual(StatusCodes.Status500InternalServerError, httpContext.Response.StatusCode);
        Assert.AreEqual(1, logger.Collector.Count);
    }

    // Plan section 4: the caller cancelling is not treated as an error.
    [TestMethod]
    public async Task Invoke_CallerCancels_RethrowsAndLogsNoError()
    {
        var logger = new FakeLogger<ExceptionHandlingMiddleware>();
        var middleware = new ExceptionHandlingMiddleware(logger);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var httpContext = TestHttp.CreateRequest(string.Empty).HttpContext;
        var context = new FakeFunctionContext(httpContext, cancellation.Token);

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => middleware.Invoke(context, functionContext => throw new OperationCanceledException(functionContext.CancellationToken)));

        Assert.AreEqual(0, logger.Collector.Count);
    }

    private static Task ThrowUnexpectedError(FunctionContext context)
    {
        throw new InvalidOperationException(InternalDetail);
    }

    private static string ReadBody(HttpContext httpContext)
    {
        var body = (MemoryStream)httpContext.Response.Body;

        return Encoding.UTF8.GetString(body.ToArray());
    }
}
