using System.Text.Json;
using System.Text.Json.Serialization;
using DraughtsGameFunctionModels.Enums;

namespace DraughtsGameFunction.Json;

/// <summary>
/// Reads <see cref="Difficulty"/> from one of the names easy, normal and hard, in any letter case (spec section 6.1.1,
/// item 7). Anything else is a <see cref="JsonException"/>, including a number, which the built-in enum converter would
/// accept, and "easy,hard" or " hard ", which it also accepts (plan section 3.5).
/// </summary>
internal sealed class DifficultyJsonConverter : JsonConverter<Difficulty>
{
    public override Difficulty Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException($"Difficulty must be a name, but found {reader.TokenType}.");
        }

        var name = reader.GetString();

        foreach (var difficulty in Enum.GetValues<Difficulty>())
        {
            if (string.Equals(difficulty.ToString(), name, StringComparison.OrdinalIgnoreCase))
            {
                return difficulty;
            }
        }

        throw new JsonException("Difficulty is not one of the allowed names.");
    }

    // The API never writes a difficulty to a client.
    public override void Write(Utf8JsonWriter writer, Difficulty value, JsonSerializerOptions options) =>
        throw new NotSupportedException("The difficulty is only read from requests.");
}
