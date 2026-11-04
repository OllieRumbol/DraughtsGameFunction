using System.Text.Json;
using System.Text.Json.Serialization;

namespace DraughtsGameFunction.Json;

/// <summary>
/// Reads a board from JSON rows into <c>long[,]</c>, which System.Text.Json cannot read by itself.
/// Anything other than 8 rows of 8 whole numbers is a <see cref="JsonException"/> (spec section 6.1.2, rule 2).
/// </summary>
internal sealed class BoardJsonConverter : JsonConverter<long[,]>
{
    private const int BoardSize = 8;

    public override long[,] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        RequireToken(reader, JsonTokenType.StartArray);

        var board = new long[BoardSize, BoardSize];

        for (var row = 0; row < BoardSize; row++)
        {
            reader.Read();
            RequireToken(reader, JsonTokenType.StartArray);

            for (var column = 0; column < BoardSize; column++)
            {
                reader.Read();
                board[row, column] = ReadWholeNumber(reader);
            }

            reader.Read();
            RequireToken(reader, JsonTokenType.EndArray);
        }

        reader.Read();
        RequireToken(reader, JsonTokenType.EndArray);

        return board;
    }

    // The API never writes a board to a client.
    public override void Write(Utf8JsonWriter writer, long[,] value, JsonSerializerOptions options) =>
        throw new NotSupportedException("The board is only read from requests.");

    private static long ReadWholeNumber(in Utf8JsonReader reader)
    {
        RequireToken(reader, JsonTokenType.Number);

        if (!reader.TryGetInt64(out var value))
        {
            throw new JsonException("A board value must be a whole number.");
        }

        return value;
    }

    private static void RequireToken(in Utf8JsonReader reader, JsonTokenType expected)
    {
        if (reader.TokenType != expected)
        {
            throw new JsonException($"Expected {expected} in the board, but found {reader.TokenType}.");
        }
    }
}
