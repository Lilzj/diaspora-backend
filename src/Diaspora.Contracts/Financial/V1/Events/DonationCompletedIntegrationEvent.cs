using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Financial.V1.Events;

/// <summary>
/// A donation completed.
/// <para>
/// <b>The donor's identifier is absent when the donation is anonymous, rather than present beside a
/// flag.</b> That is Housing's rule from the platform handbook — return the narrowed value, not the
/// value plus permission to look at it — and here it is the difference between a privacy promise and
/// a privacy convention one careless consumer away from a notification with a name in it.
/// </para>
/// </summary>
public sealed record DonationCompletedIntegrationEvent : IntegrationEvent
{
    public required Guid DonationId { get; init; }

    /// <summary>
    /// The ledger transaction that settled it, resolvable at
    /// <c>GET /financial/transactions/{transactionId}</c>.
    /// </summary>
    public Guid? TransactionId { get; init; }

    /// <summary>Null when the donation is anonymous. Never a placeholder.</summary>
    public Guid? DonorUserId { get; init; }

    public required bool IsAnonymous { get; init; }

    /// <summary><c>Member</c>, <c>Organization</c>, <c>Community</c>, <c>Fund</c>, <c>Platform</c>.</summary>
    public required string BeneficiaryType { get; init; }

    public Guid? BeneficiaryId { get; init; }

    public string? CauseCode { get; init; }

    /// <summary>
    /// The campaign this was given to, where the cause names one.
    /// <para>
    /// <b>Resolved here rather than left in the cause code</b>, which is a string in a format only
    /// this module knows. A consumer that wanted the campaign would have to re-implement the
    /// encoding, and a consumer that got it wrong would silently attribute nothing.
    /// </para>
    /// </summary>
    public Guid? CampaignId { get; init; }

    public required long AmountMinorUnits { get; init; }

    public required string Currency { get; init; }

    /// <summary>The donor's own copy of the record, and an anonymous donor's only key to it.</summary>
    public required string ReceiptReference { get; init; }

    public string? CountryCode { get; init; }

    public required DateTimeOffset CompletedAt { get; init; }
}
