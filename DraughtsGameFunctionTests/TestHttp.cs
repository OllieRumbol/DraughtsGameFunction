using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace DraughtsGameFunctionTests;

/// <summary>
/// Builds a plain <see cref="DefaultHttpContext"/>, and writes an <see cref="IResult"/> to it as the host would.
/// </summary>
internal static class TestHttp
{
    public const string StartingBoard =
        "[[0,2,0,2,0,2,0,2],[2,0,2,0,2,0,2,0],[0,2,0,2,0,2,0,2],[5,0,5,0,5,0,5,0]," +
        "[0,5,0,5,0,5,0,5],[1,0,1,0,1,0,1,0],[0,1,0,1,0,1,0,1],[1,0,1,0,1,0,1,0]]";

    public static HttpRequest CreateRequest(string body, bool sendContentLength = false)
    {
        var bytes = Encoding.UTF8.GetBytes(body);

        var httpContext = new DefaultHttpContext
        {
            // The built-in results resolve a logger factory from the request services.
            RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider()
        };
        httpContext.Request.Method = HttpMethods.Post;
        httpContext.Request.Body = new MemoryStream(bytes);
        httpContext.Request.ContentLength = sendContentLength ? bytes.Length : null;
        httpContext.Response.Body = new MemoryStream();

        return httpContext.Request;
    }

    public static async Task<TestResponse> ExecuteAsync(IResult result, HttpRequest request)
    {
        var httpContext = request.HttpContext;

        await result.ExecuteAsync(httpContext);

        var body = (MemoryStream)httpContext.Response.Body;
        var json = JsonDocument.Parse(body.ToArray()).RootElement;

        return new TestResponse(httpContext.Response.StatusCode, json);
    }
}

internal sealed record TestResponse(int StatusCode, JsonElement Json);
