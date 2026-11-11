using System.Text.Json;
using System.Text.Json.Serialization;

namespace DraughtsGameFunction.Json;

/// <summary>
/// The JSON options for every request and response (plan section 3.3).
/// The web defaults write camelCase names and read names in any letter case (spec section 6.1.1).
/// </summary>
internal static class ApiJsonOptions
{
    public static readonly JsonSerializerOptions Default = new(JsonSerializerDefaults.Web)
    {
        // The web defaults also read "2" as a number. Spec section 6.1.1, item 8, does not allow it.
        NumberHandling = JsonNumberHandling.Strict,
        Converters =
        {
            new BoardJsonConverter(),
            new DifficultyJsonConverter(),
        }
    };
}
