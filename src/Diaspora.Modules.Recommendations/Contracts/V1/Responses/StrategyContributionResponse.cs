namespace Diaspora.Modules.Recommendations.Contracts.V1.Responses;

/// <summary>
/// What one strategy contributed to one recommendation's score.
/// <para>
/// <b>Returned on the wire, not just stored, and that is a deliberate transparency decision.</b> The
/// mobile client does not render this today; a "why am I seeing this" panel, an internal debugging
/// screen and any future audit tool all need it, and a field that has to be added later is a contract
/// change while a field that is already there is not.
/// </para>
/// </summary>
public sealed record StrategyContributionResponse
{
    public required string StrategyKey { get; init; }

    /// <summary><c>Popularity</c>, <c>Location</c>, <c>Trust</c>, and so on.</summary>
    public required string Kind { get; init; }

    /// <summary>What the strategy said, between 0 and 1, before weighting.</summary>
    public required decimal RawScore { get; init; }

    /// <summary>What the surface's catalogue says this strategy is worth.</summary>
    public required decimal Weight { get; init; }

    /// <summary>The product. Frozen at generation time, so a later retune cannot rewrite history.</summary>
    public required decimal WeightedScore { get; init; }
}
