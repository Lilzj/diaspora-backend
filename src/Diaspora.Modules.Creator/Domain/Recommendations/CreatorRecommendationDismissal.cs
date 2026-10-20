using Diaspora.Modules.Creator.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Creator.Domain.Recommendations;

/// <summary>
/// A member told the platform they do not want one suggestion.
/// <para>
/// <b>Keyed by the member, not the creator record.</b> Recommendations are served to members who
/// have never done anything creator-shaped too — "your profile is missing a bio" is a reason to
/// open a creator record, not something that needs one — so a dismissal cannot wait for a
/// <c>CreatorId</c> to exist.
/// </para>
/// <para>
/// <b>Stored server-side so it holds everywhere.</b> The client type says it plainly: dismissal is a
/// round trip rather than a local filter, so a suggestion dismissed on the phone is dismissed on the
/// web. The identifier is the recommendation's own stable key (<c>milestone:{id}</c>,
/// <c>course:{id}</c>…), so a dismissal outlives any one list and a new state of the same item —
/// a different key — is allowed to surface again.
/// </para>
/// </summary>
public sealed class CreatorRecommendationDismissal : AggregateRoot<CreatorRecommendationDismissalId>
{
    /// <summary>Longest recommendation key accepted. Generated keys are a prefix and a Guid.</summary>
    public const int MaxRecommendationIdLength = 128;

    private CreatorRecommendationDismissal(
        CreatorRecommendationDismissalId id,
        Guid userId,
        string recommendationId,
        DateTimeOffset dismissedAt) : base(id)
    {
        UserId = userId;
        RecommendationId = recommendationId;
        DismissedAt = dismissedAt;
    }

    private CreatorRecommendationDismissal() { }

    public Guid UserId { get; private set; }

    public string RecommendationId { get; private set; } = string.Empty;

    public DateTimeOffset DismissedAt { get; private set; }

    public static CreatorRecommendationDismissal Record(
        CreatorRecommendationDismissalId id,
        Guid userId,
        string recommendationId,
        DateTimeOffset dismissedAt)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(recommendationId);

        return new CreatorRecommendationDismissal(id, userId, recommendationId.Trim(), dismissedAt);
    }
}
