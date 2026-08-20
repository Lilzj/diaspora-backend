using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Financial.V1.Events;

/// <summary>
/// A settled payment has been recognised as buying a community membership.
/// <para>
/// <b>This is what a paid membership activates on.</b> Not the client's success screen, not an HTTP
/// 200 on the binding route, and not the capture call's own response — a capture that times out has
/// an unknown outcome, and the platform reads the intent back before it publishes this. The
/// consuming module moves the membership to active when it arrives, and does so idempotently,
/// because at-least-once is the delivery contract and this event is deliberately replayed by the
/// reconciliation sweep.
/// </para>
/// <para>
/// <b>No balance, no wallet, no receipt.</b> What travels is shape: an amount that moved, against a
/// membership, at a moment. Everything else stays where it is owned.
/// </para>
/// </summary>
public sealed record MembershipPaymentSettledIntegrationEvent : IntegrationEvent
{
    public required Guid BindingId { get; init; }

    /// <summary>The owning module's membership identifier. Its own, echoed back.</summary>
    public required Guid MembershipId { get; init; }

    public required Guid PaymentIntentId { get; init; }

    public required Guid PayerUserId { get; init; }

    public Guid? OrganizationId { get; init; }

    public required long AmountMinorUnits { get; init; }

    public required string Currency { get; init; }

    /// <summary>When the platform judged the money taken.</summary>
    public required DateTimeOffset SettledAt { get; init; }

    public required DateTimeOffset BoundAt { get; init; }

    /// <summary>
    /// Whether this is the reconciliation sweep repairing a missed activation rather than the first
    /// announcement. Consumers treat both identically; it is here so an operator can tell them apart.
    /// </summary>
    public bool IsReplay { get; init; }
}

/// <summary>
/// A fundraising campaign moved from one state to another.
/// <para>
/// <b><see cref="CauseCode"/> is present from publication onwards and null before it.</b> Its
/// arrival is the signal that the campaign can now collect — a consumer that wants to know when a
/// campaign became fundable watches for it rather than for a particular status name.
/// </para>
/// </summary>
public sealed record CampaignStatusChangedIntegrationEvent : IntegrationEvent
{
    public required Guid CampaignId { get; init; }

    public required Guid OrganizerUserId { get; init; }

    public Guid? OrganizationId { get; init; }

    public required string PreviousStatus { get; init; }

    public required string CurrentStatus { get; init; }

    public string? CauseCode { get; init; }

    public string? Reason { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }
}

/// <summary>A campaign published an update to the people supporting it.</summary>
public sealed record CampaignUpdatePublishedIntegrationEvent : IntegrationEvent
{
    public required Guid CampaignId { get; init; }

    public required Guid UpdateId { get; init; }

    public required Guid AuthorUserId { get; init; }

    /// <summary>The update's own title. The body is not published — a notification is not a copy.</summary>
    public required string Title { get; init; }

    public string? CauseCode { get; init; }

    public required DateTimeOffset PublishedAt { get; init; }
}

/// <summary>
/// A campaign crossed one of its declared funding milestones.
/// <para>
/// Raised once per milestone, from the server's own stamp, so a redelivery does not produce a second
/// celebration and two readers cannot disagree about when it happened.
/// </para>
/// </summary>
public sealed record CampaignMilestoneReachedIntegrationEvent : IntegrationEvent
{
    public required Guid CampaignId { get; init; }

    public required Guid MilestoneId { get; init; }

    public required string Label { get; init; }

    public required long TargetMinorUnits { get; init; }

    public required long RaisedMinorUnits { get; init; }

    public required string Currency { get; init; }

    public string? CauseCode { get; init; }

    public required DateTimeOffset ReachedAt { get; init; }
}

/// <summary>
/// A recurring donation took its money.
/// <para>
/// The donor is always present: an anonymous donation cannot recur, because there would be nobody
/// to charge and nobody to tell.
/// </para>
/// </summary>
public sealed record RecurringDonationChargedIntegrationEvent : IntegrationEvent
{
    public required Guid RecurringDonationId { get; init; }

    public required Guid DonorUserId { get; init; }

    /// <summary>The donation this cycle produced. Its receipt is the donor's copy.</summary>
    public required Guid DonationId { get; init; }

    public required string CauseCode { get; init; }

    public required long AmountMinorUnits { get; init; }

    public required string Currency { get; init; }

    /// <summary>Which charge this was. One for the first repeat, and so on.</summary>
    public required int OccurrenceNumber { get; init; }

    public DateTimeOffset? NextChargeAt { get; init; }

    public required DateTimeOffset ChargedAt { get; init; }
}

/// <summary>
/// A recurring donation tried and failed.
/// <para>
/// <b><see cref="WillRetry"/> is what the message to the donor turns on.</b> "We could not take your
/// donation and will try again on Friday" and "we have stopped trying" are different sentences, and
/// a consumer that could not tell them apart would send the wrong one.
/// </para>
/// </summary>
public sealed record RecurringDonationFailedIntegrationEvent : IntegrationEvent
{
    public required Guid RecurringDonationId { get; init; }

    public required Guid DonorUserId { get; init; }

    public required string CauseCode { get; init; }

    public required long AmountMinorUnits { get; init; }

    public required string Currency { get; init; }

    public required string FailureReason { get; init; }

    public required bool WillRetry { get; init; }

    public required int ConsecutiveFailures { get; init; }

    public DateTimeOffset? NextAttemptAt { get; init; }

    public required DateTimeOffset FailedAt { get; init; }
}
