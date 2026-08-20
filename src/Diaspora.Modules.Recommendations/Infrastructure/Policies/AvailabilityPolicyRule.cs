using Diaspora.Modules.Recommendations.Domain.Candidates;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Extensibility.Policies;

namespace Diaspora.Modules.Recommendations.Infrastructure.Policies;

/// <summary>
/// Do not recommend something that is over.
/// <para>
/// <b>The last line of defence rather than the first, and it exists because the first line is
/// eventually consistent.</b> The candidate source already filters on activity and expiry, so in
/// steady state this rule excludes nothing. It matters in the window that is guaranteed to exist:
/// the outbox has not yet delivered the <c>JobClosed</c> event, or a pool was assembled milliseconds
/// before an event's start time passed.
/// </para>
/// <para>
/// <b>A system policy, because the alternative is recommending a member a flat that was taken last
/// week.</b> That is the single most damaging kind of recommendation the platform can make — it
/// wastes somebody's time and teaches them not to trust the feed — and it should not be one
/// configuration change away.
/// </para>
/// </summary>
public sealed class AvailabilityPolicyRule : IRecommendationPolicyRule
{
    public const string PolicyKey = "availability";

    public string Key => PolicyKey;

    public RecommendationPolicyKind Kind => RecommendationPolicyKind.Availability;

    public PolicyDecision Evaluate(RecommendationCandidate candidate, PolicyContext context)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(context);

        if (!candidate.IsActive)
        {
            return PolicyDecision.Exclude("the item is no longer published");
        }

        return candidate.ExpiresAt is { } expiry && expiry <= context.Personalization.AsOf
            ? PolicyDecision.Exclude("the item has expired")
            : PolicyDecision.Allow();
    }
}
