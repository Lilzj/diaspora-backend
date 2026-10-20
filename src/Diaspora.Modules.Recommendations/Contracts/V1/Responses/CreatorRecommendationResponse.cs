namespace Diaspora.Modules.Recommendations.Contracts.V1.Responses;

/// <summary>
/// One creator suggestion — CREATOR-GAP-013 — in the client's <c>CreatorRecommendation</c> shape
/// (<c>src/types/creator-economy.ts</c>), field for field.
/// <para>
/// Composed by the Creator Platform behind <c>ICreatorRecommendationProvider</c>; this module only
/// serves it. <c>recommendationId</c> is stable per underlying item and state —
/// <c>milestone:{id}</c>, <c>challenge:{id}</c>, <c>challenge-reward:{id}</c>, <c>grant:{id}</c>,
/// <c>course:{id}</c>, <c>profile:{key}</c>, <c>verification:eligible</c>,
/// <c>verification:{applicationId}:{state}</c> — which is what lets a dismissal stick.
/// </para>
/// </summary>
public sealed record CreatorRecommendationResponse
{
    public required string RecommendationId { get; init; }

    /// <summary>One of the client's kinds: <c>course</c>, <c>challenge</c>, <c>grant</c>, <c>verification</c>, <c>improvement</c>.</summary>
    public required string Kind { get; init; }

    public required string Title { get; init; }

    public required string Body { get; init; }

    /// <summary>Why the platform is suggesting it, from the caller's own state.</summary>
    public required string Reason { get; init; }

    public string? Icon { get; init; }

    public required string ActionLabel { get; init; }

    public required string ActionRoute { get; init; }

    /// <summary>0–1. For emphasis only; the list is already in order.</summary>
    public double? Confidence { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }
}

/// <summary>The answer to a creator-suggestion dismissal. The client ignores the body.</summary>
public sealed record CreatorRecommendationDismissedResponse
{
    public required string RecommendationId { get; init; }

    public required DateTimeOffset DismissedAt { get; init; }
}
