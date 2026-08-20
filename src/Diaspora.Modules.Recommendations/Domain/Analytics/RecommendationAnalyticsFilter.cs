using Diaspora.Modules.Recommendations.Domain.Enums;

namespace Diaspora.Modules.Recommendations.Domain.Analytics;

/// <summary>
/// What an operator asks the analytics surface.
/// <para>
/// Bounded by construction: a null date range is resolved to the last thirty days by the handler
/// rather than left open, because an unbounded aggregate over the rollup table is the one query in
/// this module that could take a production database down.
/// </para>
/// </summary>
public sealed record RecommendationAnalyticsFilter
{
    public RecommendationSurface? Surface { get; init; }

    public DateOnly? From { get; init; }

    public DateOnly? To { get; init; }

    /// <summary>Restricts the breakdowns returned. Null returns every dimension.</summary>
    public string? Dimension { get; init; }

    /// <summary>How many days to return. Clamped by the handler.</summary>
    public int Limit { get; init; } = 90;
}
