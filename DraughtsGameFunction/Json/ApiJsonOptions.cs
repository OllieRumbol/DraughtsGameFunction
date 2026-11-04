using System.Text.Json;

namespace DraughtsGameFunction.Json;

/// <summary>
/// The JSON options for every request and response.
/// The web defaults write camelCase names and read names in any letter case (spec section 6.1.1).
/// </summary>
internal static class ApiJsonOptions
{
    public static readonly JsonSerializerOptions Default = new(JsonSerializerDefaults.Web)
    {
        Converters = { new BoardJsonConverter() }
    };
}
