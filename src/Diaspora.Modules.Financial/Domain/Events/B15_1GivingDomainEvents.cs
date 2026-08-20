using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Events;

/// <summary>
/// A settled payment has been recognised as buying a community membership.
/// <para>
/// <b>This is what activates a paid membership, and an HTTP 200 is not.</b> The order is: intent,
/// authorisation, capture, the platform's own read of the settled intent, this fact, then
/// activation. A consumer that activated on anything earlier would be granting a membership for a
/// payment that can still decline.
/// </para>
/// </summary>
public sealed record MembershipPaymentSettledDomainEvent : DomainEvent
{
    public required MembershipPaymentBindingId BindingId { get; init; }

    /// <summary>B18's membership. Never resolved here.</summary>
    public required Guid MembershipId { get; init; }

    public required PaymentIntentId PaymentIntentId { get; init; }

    public required Guid PayerUserId { get; init; }

    public Guid? OrganizationId { get; init; }

    public required long AmountMinorUnits { get; init; }

    public required string Currency { get; init; }

    public required DateTimeOffset SettledAt { get; init; }

    public required DateTimeOffset BoundAt { get; init; }

    /// <summary>
    /// Whether this is the reconciliation sweep re-announcing a binding, rather than the first
    /// announcement. Consumers treat both identically — it is here so an operator reading a log can
    /// tell a repair from an ordinary activation.
    /// </summary>
    public bool IsReplay { get; init; }
}

/// <summary>A campaign moved from one lifecycle state to another.</summary>
public sealed record CampaignStatusChangedDomainEvent : DomainEvent
{
    public required CampaignId CampaignId { get; init; }

    public required Guid OrganizerUserId { get; init; }

    public Guid? OrganizationId { get; init; }

    public required string PreviousStatus { get; init; }

    public required string CurrentStatus { get; init; }

    /// <summary>Set from the moment of publication onwards. Null before it.</summary>
    public string? CauseCode { get; init; }

    public string? Reason { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }
}

/// <summary>A campaign published an update to the people supporting it.</summary>
public sealed record CampaignUpdatePublishedDomainEvent : DomainEvent
{
    public required CampaignId CampaignId { get; init; }

    public required CampaignUpdateId UpdateId { get; init; }

    public required Guid AuthorUserId { get; init; }

    public required string Title { get; init; }

    public string? CauseCode { get; init; }

    public required DateTimeOffset PublishedAt { get; init; }
}

/// <summary>A campaign crossed one of its declared funding milestones.</summary>
public sealed record CampaignMilestoneReachedDomainEvent : DomainEvent
{
    public required CampaignId CampaignId { get; init; }

    public required Guid MilestoneId { get; init; }

    public required string Label { get; init; }

    public required long TargetMinorUnits { get; init; }

    public required long RaisedMinorUnits { get; init; }

    public required string Currency { get; init; }

    public string? CauseCode { get; init; }

    public required DateTimeOffset ReachedAt { get; init; }
}

/// <summary>A recurring donation schedule took its money.</summary>
public sealed record RecurringDonationChargedDomainEvent : DomainEvent
{
    public required RecurringDonationId ScheduleId { get; init; }

    public required Guid DonorUserId { get; init; }

    public required DonationId DonationId { get; init; }

    public required string CauseCode { get; init; }

    public required long AmountMinorUnits { get; init; }

    public required string Currency { get; init; }

    public required int OccurrenceNumber { get; init; }

    public DateTimeOffset? NextChargeAt { get; init; }

    public required DateTimeOffset ChargedAt { get; init; }
}

/// <summary>A recurring donation schedule tried and failed.</summary>
public sealed record RecurringDonationFailedDomainEvent : DomainEvent
{
    public required RecurringDonationId ScheduleId { get; init; }

    public required Guid DonorUserId { get; init; }

    public required string CauseCode { get; init; }

    public required long AmountMinorUnits { get; init; }

    public required string Currency { get; init; }

    public required string FailureReason { get; init; }

    /// <summary>Whether the schedule will try again, or has given up and suspended itself.</summary>
    public required bool WillRetry { get; init; }

    public required int ConsecutiveFailures { get; init; }

    public DateTimeOffset? NextAttemptAt { get; init; }

    public required DateTimeOffset FailedAt { get; init; }
}
