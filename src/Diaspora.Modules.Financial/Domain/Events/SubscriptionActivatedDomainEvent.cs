using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Events;

/// <summary>
/// A subscription started, or renewed into a new period.
/// <para>
/// <see cref="IsRenewal"/> is what tells the two apart. They are the same fact — this member is
/// entitled to these things until this date — and splitting them into two contracts would make
/// every consumer handle both identically while getting the ordering wrong once.
/// </para>
/// </summary>
public sealed record SubscriptionActivatedDomainEvent : DomainEvent
{
    public required SubscriptionId SubscriptionId { get; init; }

    public required Guid SubscriberUserId { get; init; }

    public required string PlanCode { get; init; }

    public required SubscriptionTier Tier { get; init; }

    public required SubscriptionStatus Status { get; init; }

    public required BillingInterval Interval { get; init; }

    /// <summary>The capability keys this plan grants. What a client renders.</summary>
    public required IReadOnlyList<string> Entitlements { get; init; }

    public required bool IsTrial { get; init; }

    public required bool IsRenewal { get; init; }

    public required DateTimeOffset PeriodStartsAt { get; init; }

    public DateTimeOffset? PeriodEndsAt { get; init; }

    public required DateTimeOffset ActivatedAt { get; init; }
}
