using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Recommendations.Domain.Events;

/// <summary>
/// A member acted on a recommendation.
/// <para>
/// <b>Reported by the client, never inferred from a click.</b> A tap means "I want to look"; an
/// acceptance means "I applied for the job". Inferring the second from the first would make every
/// strategy look roughly equally good, because every strategy can get a tap, and would hide the only
/// distinction the analytics exist to draw.
/// </para>
/// </summary>
public sealed record RecommendationAcceptedDomainEvent : DomainEvent
{
    public required RecommendationFeedbackId FeedbackId { get; init; }

    public required UserId UserId { get; init; }

    public required RecommendationTargetType TargetType { get; init; }

    public required Guid TargetId { get; init; }

    public required RecommendationSurface Surface { get; init; }

    public string? StrategyKey { get; init; }

    public RecommendationReasonCode? ReasonCode { get; init; }

    /// <summary>What the platform scored it at. The number the acceptance rate is measured against.</summary>
    public decimal? Score { get; init; }

    public required DateTimeOffset AcceptedAt { get; init; }
}
