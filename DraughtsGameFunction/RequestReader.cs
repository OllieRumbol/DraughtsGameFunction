using System.Text.Json;
using DraughtsGameFunction.Json;
using Microsoft.AspNetCore.Http;

namespace DraughtsGameFunction;

/// <summary>
/// Reads a request body and checks its board, used by both functions.
/// </summary>
internal static class RequestReader
{
    public const int MaximumBodyBytes = 10 * 1024; // spec section 6.4, item 7

    public const string BodyTooLargeMessage = "The request body must be 10 KB or smaller.";
    public const string InvalidJsonMessage = "The request body is not valid JSON.";
    public const string InvalidBoardMessage = "Board must be 8 rows of 8 values.";
    public const string BoardValuesMessage = "Board values must be from 0 to 6.";
    public const string InvalidTipForMessage = "TipFor must be 1 or 2.";

    private const string BoardJsonPath = "$.Board";
    private const string TipForJsonPath = "$.TipFor";

    // The highest board value, a highlighted empty square (spec section 2.2).
    private const long HighestBoardValue = 6;

    /// <summary>
    /// Reads at most <see cref="MaximumBodyBytes"/> + 1 bytes, so a chunked body cannot get past the limit.
    /// Exactly one of the two results is set.
    /// </summary>
    public static async Task<(T? Request, string? Error)> ReadAsync<T>(HttpRequest request, CancellationToken ct)
        where T : class
    {
        var buffer = new byte[MaximumBodyBytes + 1];
        int bytesRead;

        try
        {
            bytesRead = await ReadUpToBufferSizeAsync(request.Body, buffer, ct);
        }
        catch (BadHttpRequestException)
        {
            // The client stopped sending the body, for example by disconnecting. This is not a fault in the app.
            return (null, InvalidJsonMessage);
        }

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

    /// <summary>
    /// The error message for a board that is missing or holds a value outside 0 to 6 (spec section 6.1.2, rules 1 and 4),
    /// or null for a valid board.
    /// </summary>
    public static string? BoardError(long[,]? board)
    {
        if (board is null)
        {
            return InvalidBoardMessage;
        }

        foreach (var value in board)
        {
            if (value is < 0 or > HighestBoardValue)
            {
                return BoardValuesMessage;
            }
        }

        return null;
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
        // A wrong value has no inner exception when a converter throws it, as BoardJsonConverter does, or an inner
        // FormatException or InvalidOperationException when the serializer cannot read it (plan section 3.5).
        // Broken JSON has the reader's error as the inner exception, even when it breaks inside the board.
        var isWrongValue = exception.InnerException is null or FormatException or InvalidOperationException;
        if (!isWrongValue)
        {
            return InvalidJsonMessage;
        }

        // The path follows the client's letter case, for example $.board (plan section 3.5).
        if (string.Equals(exception.Path, BoardJsonPath, StringComparison.OrdinalIgnoreCase))
        {
            return InvalidBoardMessage;
        }

        if (string.Equals(exception.Path, TipForJsonPath, StringComparison.OrdinalIgnoreCase))
        {
            return InvalidTipForMessage;
        }

        return InvalidJsonMessage;
    }
}
