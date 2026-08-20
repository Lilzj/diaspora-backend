namespace Diaspora.IntegrationTests.Recommendations;

/// <summary>What one strategy contributed to a score, on the wire.</summary>
public sealed record StrategyContributionShape
{
    public string? StrategyKey { get; init; }

    public string? Kind { get; init; }

    public decimal RawScore { get; init; }

    public decimal Weight { get; init; }

    public decimal WeightedScore { get; init; }
}
