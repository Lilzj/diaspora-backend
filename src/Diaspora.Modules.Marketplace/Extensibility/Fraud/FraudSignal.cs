namespace Diaspora.Modules.Marketplace.Extensibility.Fraud;

/// <summary>
/// One thing a provider noticed.
/// <para>
/// <see cref="Explanation"/> is written for a moderator rather than for a member: it appears in
/// the moderation trail, and "matched 7 of 9 tokens against listing 4c1e…" is what lets a human
/// decide whether the provider was right. Nothing here is ever rendered to the seller — telling
/// somebody exactly which heuristic caught them is telling them how to get past it.
/// </para>
/// </summary>
public sealed record FraudSignal
{
    /// <summary>A short stable code — <c>duplicate-title</c>, <c>price-outlier</c>.</summary>
    public required string Code { get; init; }

    public required FraudSignalSeverity Severity { get; init; }

    /// <summary>What the provider found, for a moderator. Never shown to the seller.</summary>
    public required string Explanation { get; init; }

    /// <summary>The provider's own confidence, zero to one. Advisory.</summary>
    public double Confidence { get; init; } = 1d;
}
