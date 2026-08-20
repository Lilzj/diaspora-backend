namespace Diaspora.IntegrationTests.Recommendations;

/// <summary>
/// One recommendation card on the wire.
/// <para>
/// The properties a client actually renders. See <see cref="RecommendationPageShape"/> for why these
/// are declared here rather than deserialised into the module's own response type.
/// </para>
/// </summary>
public sealed record RecommendationShape
{
    public string? TargetType { get; init; }

    public Guid TargetId { get; init; }

    public string? Title { get; init; }

    public string? Summary { get; init; }

    public string? Slug { get; init; }

    public string? MediaReference { get; init; }

    public string? CategoryKey { get; init; }

    public RecommendationLocationShape? Location { get; init; }

    public bool IsVerified { get; init; }

    public bool IsFeatured { get; init; }

    public int Position { get; init; }

    public decimal Score { get; init; }

    public string? Strategy { get; init; }

    /// <summary>Never null on a real response. The module's central promise.</summary>
    public RecommendationReasonShape? Reason { get; init; }

    public List<StrategyContributionShape> Contributions { get; init; } = [];

    public bool IsProjection { get; init; }
}
