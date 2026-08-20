using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Recommendations.Domain.Events;

/// <summary>
/// A member refused a recommendation.
/// <para>
/// <b>The most informative event this module raises, and the least celebrated.</b> An acceptance
/// says a strategy was right about one thing; a dismissal says it was wrong in a way the member
/// cared enough to correct, which is a far stronger statement. It carries the strategy and the
/// reason code for exactly that purpose — a strategy whose dismissals cluster on one reason is a
/// strategy with a bug, and no other record would show it.
/// </para>
/// </summary>
public sealed record RecommendationDismissedDomainEvent : DomainEvent
{
    public required RecommendationFeedbackId FeedbackId { get; init; }

    public required UserId UserId { get; init; }

    public required RecommendationTargetType TargetType { get; init; }

    public required Guid TargetId { get; init; }

    public required RecommendationSurface Surface { get; init; }

    /// <summary>The strategy that put it on the page, when the set is still known.</summary>
    public string? StrategyKey { get; init; }

    public RecommendationReasonCode? ReasonCode { get; init; }

    /// <summary>When the suppression lapses. Null means never.</summary>
    public DateTimeOffset? SuppressedUntil { get; init; }

    public required DateTimeOffset DismissedAt { get; init; }
}
