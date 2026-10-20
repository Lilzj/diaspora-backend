namespace Diaspora.Contracts.Recommendations.V1.Creator;

/// <summary>
/// Creator suggestions — CREATOR-GAP-013 — composed by the module that knows the creator.
/// <para>
/// <b>Why a seam.</b> <c>/api/v1/recommendations</c> is the Recommendations module's URL space and
/// one module owns one prefix, so <c>GET /recommendations/creator</c> is mapped there. But what to
/// suggest to a creator — a milestone to claim, a challenge nearly done, a grant they qualify for —
/// is the Creator Platform's knowledge, and Recommendations references no module at all. So the
/// route lives with the prefix and the composition lives with the knowledge, joined here.
/// </para>
/// <para>
/// <b>The provider never saves.</b> <see cref="StageDismissalAsync"/> adds to the request's unit of
/// work; the command that called it commits, so the dismissal and the command's own audit share one
/// transaction.
/// </para>
/// </summary>
public interface ICreatorRecommendationProvider
{
    /// <summary>The member's suggestions, ordered, dismissed ones removed, at most <paramref name="take"/>.</summary>
    Task<IReadOnlyList<CreatorRecommendationItem>> ListAsync(
        Guid userId, int take, CancellationToken cancellationToken = default);

    /// <summary>Records a dismissal unless one exists. Returns when the member dismissed it.</summary>
    Task<DateTimeOffset> StageDismissalAsync(
        Guid userId, string recommendationId, CancellationToken cancellationToken = default);
}

/// <summary>One suggestion, in the client's <c>CreatorRecommendation</c> shape.</summary>
public sealed record CreatorRecommendationItem(
    string RecommendationId,
    string Kind,
    string Title,
    string Body,
    string Reason,
    string? Icon,
    string ActionLabel,
    string ActionRoute,
    double? Confidence,
    DateTimeOffset? ExpiresAt);

/// <summary>Limits shared by both sides of the seam.</summary>
public static class CreatorRecommendationLimits
{
    /// <summary>Longest recommendation key accepted. Generated keys are a prefix and a Guid.</summary>
    public const int MaxRecommendationIdLength = 128;

    public const int DefaultTake = 10;

    public const int MaxTake = 50;
}
