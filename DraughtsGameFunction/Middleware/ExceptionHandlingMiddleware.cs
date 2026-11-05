using DraughtsGameFunction.Json;
using DraughtsGameFunctionModels.Controller;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;

namespace DraughtsGameFunction.Middleware;

/// <summary>
/// The one place unexpected errors are handled, for both functions (plan section 4).
/// The caller gets the usual response shape, so the web app handles it normally, and no internal detail.
/// </summary>
public sealed class ExceptionHandlingMiddleware(ILogger<ExceptionHandlingMiddleware> logger) : IFunctionsWorkerMiddleware
{
    public const string UnexpectedErrorMessage = "An unexpected error occurred.";

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception) when (!IsCallerCancellation(exception, context))
        {
            logger.LogError(exception, "Function {FunctionName} failed with an unexpected error.", context.FunctionDefinition.Name);

            var httpContext = context.GetHttpContext();
            if (httpContext is null)
            {
                throw;
            }

            await WriteErrorResponseAsync(httpContext);
        }
    }

    // The caller's own cancellation is not an error of the function, so it is not handled here.
    private static bool IsCallerCancellation(Exception exception, FunctionContext context)
    {
        return exception is OperationCanceledException && context.CancellationToken.IsCancellationRequested;
    }

    private static async Task WriteErrorResponseAsync(HttpContext httpContext)
    {
        if (httpContext.Response.HasStarted)
        {
            return;
        }

        var response = new Response { Successful = false, ErrorMessage = UnexpectedErrorMessage };
        var result = Results.Json(response, ApiJsonOptions.Default, statusCode: StatusCodes.Status500InternalServerError);

        await result.ExecuteAsync(httpContext);
    }
}
