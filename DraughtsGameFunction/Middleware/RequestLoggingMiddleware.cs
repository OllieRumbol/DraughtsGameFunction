using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;

namespace DraughtsGameFunction.Middleware;

/// <summary>
/// Writes one line for each request, with the route, the status code and the duration (spec section 6.4, item 6).
/// It logs no board or player data.
/// </summary>
public sealed class RequestLoggingMiddleware(ILogger<RequestLoggingMiddleware> logger, TimeProvider timeProvider) : IFunctionsWorkerMiddleware
{
    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var httpContext = context.GetHttpContext();
        if (httpContext is null)
        {
            await next(context);
            return;
        }

        var startTimestamp = timeProvider.GetTimestamp();

        await next(context);

        LogRequest(httpContext, context.GetInvocationResult().Value, startTimestamp);
    }

    /// <summary>
    /// The host writes the <see cref="IResult"/> that a function returns after the worker middleware has finished,
    /// so the response still has its default status here. The returned result holds the real one. A local run
    /// showed that logging later, from Response.OnCompleted, does not reach the console.
    /// </summary>
    public void LogRequest(HttpContext httpContext, object? functionResult, long startTimestamp)
    {
        var statusCode = (functionResult as IStatusCodeHttpResult)?.StatusCode ?? httpContext.Response.StatusCode;
        var durationMs = (long)timeProvider.GetElapsedTime(startTimestamp).TotalMilliseconds;

        logger.LogInformation(
            "{Route} returned {StatusCode} in {DurationMs} ms",
            httpContext.Request.Path.Value,
            statusCode,
            durationMs);
    }
}
