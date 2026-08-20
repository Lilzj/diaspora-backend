using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Events;

/// <summary>
/// A subscription stopped granting what it granted.
/// <para>
/// Covers expiry, cancellation and suspension, because from a consumer's point of view they are one
/// fact — the entitlements are gone — and only the reason differs. Entering a grace period does not
/// raise this: access continues, which is the whole purpose of a grace period.
/// </para>
/// </summary>
public sealed record SubscriptionEndedDomainEvent : DomainEvent
{
    public required SubscriptionId SubscriptionId { get; init; }

    public required Guid SubscriberUserId { get; init; }

    public required string PlanCode { get; init; }

    public required SubscriptionTier Tier { get; init; }

    public required SubscriptionStatus Status { get; init; }

    public string? Reason { get; init; }

    public required DateTimeOffset EndedAt { get; init; }
}
