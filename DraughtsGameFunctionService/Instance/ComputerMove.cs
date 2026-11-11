#nullable enable

using DraughtsGameFunctionModels.Service;

namespace DraughtsGameFunctionService.Instance;

/// <summary>
/// The move the computer chose, and the deepest depth its search finished (plan section 3.3).
/// <see cref="CompletedDepth"/> is 0 when no search ran: for Easy, for a player with one legal move, or when the time
/// limit passed before depth 1 finished.
/// </summary>
public sealed record ComputerMove(NextMove Move, int CompletedDepth);
