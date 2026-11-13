using System.Globalization;
using DraughtsGameFunctionModels.Options;

namespace DraughtsGameFunctionSelfPlay;

/// <summary>
/// The tool's options (plan section 7.4): <c>--games</c>, the games for each pairing, and <c>--time-scale</c>, which
/// multiplies the time limits so the depth on this machine matches the depth on Azure.
/// </summary>
public sealed record SelfPlayOptions(int Games, double TimeScale)
{
    public const string Usage = "Usage: --games <an even number, 200 by default> --time-scale <above 0, 1 by default>";

    private const int DefaultGames = 200;
    private const double DefaultTimeScale = 1.0;

    /// <summary>The options, or an error message. Exactly one of the two results is set.</summary>
    public static (SelfPlayOptions? Options, string? Error) Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var games = DefaultGames;
        var timeScale = DefaultTimeScale;

        for (var index = 0; index < args.Length; index += 2)
        {
            var name = args[index];
            var value = index + 1 < args.Length ? args[index + 1] : null;

            switch (name)
            {
                case "--games":
                    if (!int.TryParse(value, CultureInfo.InvariantCulture, out games) || games < 2 || games % 2 != 0)
                    {
                        return (null, "--games must be an even number, 2 or more, so each side moves first in half of the games.");
                    }

                    break;
                case "--time-scale":
                    if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out timeScale)
                        || !double.IsFinite(timeScale) || timeScale <= 0)
                    {
                        return (null, "--time-scale must be a number above 0, for example 0.35.");
                    }

                    break;
                default:
                    return (null, $"Unknown option: {name}");
            }
        }

        return (new SelfPlayOptions(games, timeScale), null);
    }

    /// <summary>The production search settings, with each time limit multiplied by the time scale.</summary>
    public SearchOptions ScaledSearchOptions()
    {
        var production = new SearchOptions();

        return new SearchOptions
        {
            NormalMaxDepth = production.NormalMaxDepth,
            NormalTimeLimitMs = Scale(production.NormalTimeLimitMs),
            HardTimeLimitMs = Scale(production.HardTimeLimitMs),
        };
    }

    // At least 1 ms, the lowest limit the settings allow.
    private int Scale(int milliseconds) => Math.Max(1, (int)Math.Round(milliseconds * TimeScale));
}
