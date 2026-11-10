#nullable enable

using System;

namespace DraughtsGameFunctionService.Engine;

/// <summary>
/// When the search stops: at <see cref="MaxDepth"/> moves ahead, or when <see cref="TimeLimit"/> passes (plan section 3.3).
/// It also stops sooner when a depth finds a forced win, or shows that every move loses (plan section 3.7, item 6).
/// </summary>
public sealed record SearchLimits(int? MaxDepth, TimeSpan? TimeLimit);
