using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Financial.V1.Events;

/// <summary>
/// A subscription stopped granting what it granted.
/// <para>
/// Covers expiry, cancellation and suspension, because from a consumer's point of view they are one
/// fact — the entitlements are gone — and only the reason differs. Entering a grace period does not
/// publish this: access continues, which is the whole purpose of a grace period.
/// </para>
/// </summary>
public sealed record SubscriptionEndedIntegrationEvent : IntegrationEvent
{
    public required Guid SubscriptionId { get; init; }

    public required Guid SubscriberUserId { get; init; }

    public required string PlanCode { get; init; }

    public required string Tier { get; init; }

    /// <summary><c>Expired</c>, <c>Cancelled</c> or <c>Suspended</c>.</summary>
    public required string Status { get; init; }

    public string? Reason { get; init; }

    public required DateTimeOffset EndedAt { get; init; }
}
