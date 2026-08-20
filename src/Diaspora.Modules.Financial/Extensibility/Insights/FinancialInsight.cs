namespace Diaspora.Modules.Financial.Extensibility.Insights;

/// <summary>
/// One thing worth telling somebody about their money.
/// <para>
/// Shaped like the client's existing <c>AIInsightCard</c>: an icon-free title, a sentence, and an
/// action. Creator Studio already renders exactly this shape for content insights, so a financial
/// insight arrives in a component that exists.
/// </para>
/// </summary>
public sealed record FinancialInsight
{
    public required string Key { get; init; }

    public required string Title { get; init; }

    public required string Description { get; init; }

    /// <summary>What the member can do about it. Null when there is nothing to do.</summary>
    public string? ActionRoute { get; init; }

    /// <summary>0 to 1. What an ordering would sort by.</summary>
    public decimal Confidence { get; init; } = 1m;
}
