using System.ComponentModel.DataAnnotations;

namespace DraughtsGameFunctionModels.Options;

/// <summary>
/// The search settings in plan section 2, read from the <c>Search</c> section of the configuration, for example
/// <c>Search__NormalTimeLimitMs</c> in the app settings. The defaults are in code, so a setting is only needed to
/// tune a value (spec section 6.4, item 1). The app checks them when it starts.
/// </summary>
public sealed class SearchOptions
{
    public const string SectionName = "Search";

    // A time limit above this is taken to be a mistake, such as a missing decimal point. It is 10 times the Hard limit.
    private const int LongestTimeLimitMs = 10_000;

    /// <summary>
    /// The deepest Normal searches (spec section 6.1.5, item 5). The upper limit is <c>GameTreeSearch.MaxPly</c>.
    /// </summary>
    [Range(1, 64)]
    public int NormalMaxDepth { get; set; } = 6;

    [Range(1, LongestTimeLimitMs)]
    public int NormalTimeLimitMs { get; set; } = 500;

    [Range(1, LongestTimeLimitMs)]
    public int HardTimeLimitMs { get; set; } = 1000;
}
