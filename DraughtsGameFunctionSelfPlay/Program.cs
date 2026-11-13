using DraughtsGameFunctionModels.Enums;
using DraughtsGameFunctionSelfPlay;
using DraughtsGameFunctionService.Engine;
using DraughtsGameFunctionService.Instance;
using Microsoft.Extensions.Options;

// Plays the levels against each other (spec section 6.4, item 3, and plan section 7.4). Run by hand, not in CI.
var (options, error) = SelfPlayOptions.Parse(args);
if (options is null)
{
    Console.Error.WriteLine(error);
    Console.Error.WriteLine(SelfPlayOptions.Usage);
    return 1;
}

var settings = options.ScaledSearchOptions();
var random = new Random();
var computerPlayer = new ComputerPlayer(Options.Create(settings), TimeProvider.System, random);
var startBoard = Board.FromRows(StartingRows());

Console.WriteLine($"{options.Games} games for each pairing, time scale {options.TimeScale}: "
    + $"Normal {settings.NormalTimeLimitMs} ms and depth {settings.NormalMaxDepth}, Hard {settings.HardTimeLimitMs} ms.");

// The strength targets in spec section 6.4, item 3, as the stronger level's share of the points.
(Difficulty Stronger, Difficulty Weaker, double TargetPercentage)[] pairings =
[
    (Difficulty.Hard, Difficulty.Normal, 80),
    (Difficulty.Normal, Difficulty.Easy, 90),
];

var results = new List<(PairingResult Result, double TargetPercentage)>();

foreach (var (stronger, weaker, targetPercentage) in pairings)
{
    Console.WriteLine();
    Console.WriteLine($"{stronger} against {weaker}");

    var result = Pairing.Run(
        stronger.ToString(), LevelPlayer.For(computerPlayer, stronger),
        weaker.ToString(), LevelPlayer.For(computerPlayer, weaker),
        startBoard, options.Games, random, line => Console.WriteLine($"  {line}"));

    results.Add((result, targetPercentage));
}

Console.WriteLine();
Console.WriteLine("Scores");
foreach (var (result, targetPercentage) in results)
{
    var verdict = result.StrongerPercentage >= targetPercentage ? "meets" : "misses";
    Console.WriteLine($"  {result.Stronger} against {result.Weaker}: {result.StrongerScore} of {result.Games} "
        + $"({result.StrongerPercentage:F1}%), {result.StrongerWins} wins, {result.Draws} draws, {result.WeakerWins} losses. "
        + $"This {verdict} the {targetPercentage}% target.");
}

return 0;

// The starting board in spec section 2.2.
static long[,] StartingRows() => new long[,]
{
    { 0, 2, 0, 2, 0, 2, 0, 2 },
    { 2, 0, 2, 0, 2, 0, 2, 0 },
    { 0, 2, 0, 2, 0, 2, 0, 2 },
    { 5, 0, 5, 0, 5, 0, 5, 0 },
    { 0, 5, 0, 5, 0, 5, 0, 5 },
    { 1, 0, 1, 0, 1, 0, 1, 0 },
    { 0, 1, 0, 1, 0, 1, 0, 1 },
    { 1, 0, 1, 0, 1, 0, 1, 0 },
};
