#nullable enable

using System.Numerics;

namespace DraughtsGameFunctionService.Engine;

/// <summary>
/// One legal move. <see cref="Captured"/> has one bit set for each captured square, by its board index.
/// <see cref="To"/> can equal <see cref="From"/>, when a king's chain ends where it began (spec section 6.1.3, item 8).
/// </summary>
public readonly record struct Move(int From, int To, ulong Captured)
{
    public int CaptureCount => BitOperations.PopCount(Captured);
}
