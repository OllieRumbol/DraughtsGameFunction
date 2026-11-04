using System.Text.Json;
using DraughtsGameFunction.Json;
using Microsoft.AspNetCore.Http;

namespace DraughtsGameFunction;

/// <summary>
/// Reads a request body, used by both functions.
/// </summary>
internal static class RequestReader
{
    public const int MaximumBodyBytes = 10 * 1024; // spec section 6.4, item 7

    public const string BodyTooLargeMessage = "The request body must be 10 KB or smaller.";
    public const string InvalidJsonMessage = "The request body is not valid JSON.";
    public const string InvalidBoardMessage = "Board must be 8 rows of 8 values.";

    private const string BoardJsonPath = "$.Board";

    /// <summary>
    /// Reads at most <see cref="MaximumBodyBytes"/> + 1 bytes, so a chunked body cannot get past the limit.
    /// Exactly one of the two results is set.
    /// </summary>
    public static async Task<(T? Request, string? Error)> ReadAsync<T>(HttpRequest request, CancellationToken ct)
        where T : class
    {
        var buffer = new byte[MaximumBodyBytes + 1];
        var bytesRead = await ReadUpToBufferSizeAsync(request.Body, buffer, ct);

        if (bytesRead > MaximumBodyBytes)
        {
            return (null, BodyTooLargeMessage);
        }

        try
        {
            var body = JsonSerializer.Deserialize<T>(buffer.AsSpan(0, bytesRead), ApiJsonOptions.Default);

            return body is null ? (null, InvalidJsonMessage) : (body, null);
        }
        catch (JsonException exception)
        {
            return (null, MessageFor(exception));
        }
    }

    private static async Task<int> ReadUpToBufferSizeAsync(Stream body, byte[] buffer, CancellationToken ct)
    {
        var totalRead = 0;

        while (totalRead < buffer.Length)
        {
            var read = await body.ReadAsync(buffer.AsMemory(totalRead), ct);
            if (read == 0)
            {
                break;
            }

            totalRead += read;
        }

        return totalRead;
    }

    private static string MessageFor(JsonException exception)
    {
        // Broken JSON arrives wrapped, with the reader's error as the inner exception, even when
        // it breaks inside the board. A board of the wrong shape is thrown by BoardJsonConverter.
        var isBrokenJson = exception.InnerException is not null;

        // The path follows the client's letter case, for example $.board (plan section 3.5).
        var isAtBoard = string.Equals(exception.Path, BoardJsonPath, StringComparison.OrdinalIgnoreCase);

        return !isBrokenJson && isAtBoard ? InvalidBoardMessage : InvalidJsonMessage;
    }
}
