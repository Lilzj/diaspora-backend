using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Recommendations.V1.Events;

/// <summary>
/// A member refused a recommendation.
/// <para>
/// <b>Published because it is the most informative fact this module produces and the least
/// celebrated.</b> An acceptance says a strategy was right about one thing; a dismissal says it was
/// wrong in a way the member cared enough to correct, which is a far stronger statement. A vertical
/// consuming this can learn which of its own items are being refused on sight, which is a signal it
/// cannot get any other way.
/// </para>
/// <para>
/// Consumers treat <c>EventId</c> as the idempotency key: delivery is at-least-once and unordered.
/// </para>
/// </summary>
public sealed record RecommendationDismissedIntegrationEvent : IntegrationEvent
{
    public required Guid FeedbackId { get; init; }

    public required Guid UserId { get; init; }

    public required string TargetType { get; init; }

    public required Guid TargetId { get; init; }

    public required string Surface { get; init; }

    public string? StrategyKey { get; init; }

    public string? ReasonCode { get; init; }

    /// <summary>When the suppression lapses. Null means the member will not be shown it again.</summary>
    public DateTimeOffset? SuppressedUntil { get; init; }

    public required DateTimeOffset DismissedAt { get; init; }
}
