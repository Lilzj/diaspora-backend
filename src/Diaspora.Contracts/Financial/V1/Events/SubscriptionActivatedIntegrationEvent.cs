using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Financial.V1.Events;

/// <summary>
/// A subscription started, or renewed into a new period.
/// <para>
/// <see cref="IsRenewal"/> tells the two apart. They are the same fact — this member is entitled to
/// these things until this date — and two contracts would make every consumer handle both
/// identically while getting the ordering wrong once.
/// </para>
/// <para>
/// <b><see cref="Entitlements"/> is the field consumers should act on.</b> A consumer switching on
/// <see cref="PlanCode"/> has to ship again the day somebody inserts a plan between two existing
/// ones.
/// </para>
/// </summary>
public sealed record SubscriptionActivatedIntegrationEvent : IntegrationEvent
{
    public required Guid SubscriptionId { get; init; }

    public required Guid SubscriberUserId { get; init; }

    public required string PlanCode { get; init; }

    /// <summary><c>Free</c>, <c>Premium</c>, <c>Professional</c>, <c>Organization</c>, <c>Provider</c>.</summary>
    public required string Tier { get; init; }

    public required string Status { get; init; }

    public required string Interval { get; init; }

    /// <summary>The capability keys this plan grants.</summary>
    public required IReadOnlyList<string> Entitlements { get; init; }

    public required bool IsTrial { get; init; }

    public required bool IsRenewal { get; init; }

    public required DateTimeOffset PeriodStartsAt { get; init; }

    /// <summary>Null on a lifetime plan.</summary>
    public DateTimeOffset? PeriodEndsAt { get; init; }

    public required DateTimeOffset ActivatedAt { get; init; }
}
