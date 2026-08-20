namespace Diaspora.Modules.Recommendations.Contracts.V1.Responses;

/// <summary>One slice of a day's performance — by strategy, by reason, or by target type.</summary>
public sealed record AnalyticsBreakdownResponse
{
    /// <summary><c>strategy</c>, <c>reason</c> or <c>targettype</c>.</summary>
    public required string Dimension { get; init; }

    public required string Key { get; init; }

    public required long GeneratedCount { get; init; }

    public required long ViewCount { get; init; }

    public required long ClickCount { get; init; }

    public required long AcceptCount { get; init; }

    public required long DismissCount { get; init; }

    public required decimal AcceptanceRate { get; init; }

    public required decimal ClickThroughRate { get; init; }

    public required decimal DismissalRate { get; init; }
}
