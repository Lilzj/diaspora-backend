using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Financial.V1.Events;

/// <summary>
/// A subscription is about to end, and nobody has been told yet.
/// <para>
/// <b>The warning, which is a different fact from the ending.</b> Until this existed the only signal
/// a member had that something they paid for was about to lapse was an app open at the right moment
/// computing a countdown from <c>currentPeriodEnd</c> — which is not a signal at all for the members
/// who most need it. The lead time is a policy value rather than a constant, so a deployment can warn
/// earlier without a release.
/// </para>
/// <para>
/// <b>Announced once per period.</b> A member warned twice about one renewal learns to ignore the
/// warning; the sweep records the period it has announced so a restart does not re-announce it.
/// </para>
/// </summary>
public sealed record SubscriptionExpiringIntegrationEvent : IntegrationEvent
{
    public required Guid SubscriptionId { get; init; }

    public required Guid SubscriberUserId { get; init; }

    public required string PlanCode { get; init; }

    public required string Tier { get; init; }

    /// <summary>True when it will renew itself, false when it will simply stop.</summary>
    public required bool WillRenew { get; init; }

    public required DateTimeOffset PeriodEndsAt { get; init; }

    /// <summary>How much warning this is. Whole days.</summary>
    public required int LeadDays { get; init; }

    public required DateTimeOffset AnnouncedAt { get; init; }
}
