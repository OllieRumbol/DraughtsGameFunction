#nullable enable

using DraughtsGameFunctionModels.Enums;

namespace DraughtsGameFunctionModels.Controller;

/// <summary>
/// The GetNextMove request from stage 2 (spec section 6.1.1, item 5, and plan section 3.4). <c>Depth</c> and
/// <c>Player</c> are no longer read, so a request that still sends them succeeds, and they have no effect.
/// </summary>
public class GetNextMove
{
    public Difficulty? Difficulty { get; set; }

    /// <summary>The old way to send the level: 1 Easy, 2 Normal, 3 Hard. Used only when Difficulty is missing.</summary>
    public long? Version { get; set; }

    /// <summary>Null when the request has no board. The function rejects it.</summary>
    public long[,]? Board { get; set; }
}
