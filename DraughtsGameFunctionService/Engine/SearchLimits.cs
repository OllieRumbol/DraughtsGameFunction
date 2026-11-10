#nullable enable

using System;

namespace DraughtsGameFunctionService.Engine;

/// <summary>
/// When the search stops: at <see cref="MaxDepth"/> moves ahead, or when <see cref="TimeLimit"/> passes (plan section 3.3).
/// </summary>
public sealed record SearchLimits(int? MaxDepth, TimeSpan? TimeLimit);
