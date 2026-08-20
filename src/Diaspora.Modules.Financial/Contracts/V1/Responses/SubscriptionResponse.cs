namespace Diaspora.Modules.Financial.Contracts.V1.Responses;

/// <summary>
/// A member's subscription.
/// <para>
/// <b><see cref="CancelAtPeriodEnd"/> is what "cancelled" means for somebody who has already paid
/// for the month.</b> They keep what they bought until the period runs out, and the client has to be
/// able to say so — "cancelled, active until 3 September" rather than "cancelled".
/// </para>
/// </summary>
public sealed record SubscriptionResponse
{
    public required Guid SubscriptionId { get; init; }

    public required string PlanCode { get; init; }

    public required string Tier { get; init; }

    /// <summary>
    /// <c>Trial</c>, <c>Active</c>, <c>GracePeriod</c>, <c>Suspended</c>, <c>Expired</c> or
    /// <c>Cancelled</c>.
    /// </summary>
    public required string Status { get; init; }

    public required string Interval { get; init; }

    /// <summary>Whether the subscriber currently gets what they subscribed to.</summary>
    public required bool GrantsAccess { get; init; }

    public required IReadOnlyList<EntitlementResponse> Entitlements { get; init; }

    public required DateTimeOffset CurrentPeriodStart { get; init; }

    /// <summary>Null on a lifetime plan.</summary>
    public DateTimeOffset? CurrentPeriodEnd { get; init; }

    public DateTimeOffset? TrialEndsAt { get; init; }

    /// <summary>Set while a failed renewal is being retried and access continues.</summary>
    public DateTimeOffset? GraceEndsAt { get; init; }

    public required bool CancelAtPeriodEnd { get; init; }

    public required int RenewalCount { get; init; }
}
