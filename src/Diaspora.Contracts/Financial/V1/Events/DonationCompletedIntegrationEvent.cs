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

    public required long AmountMinorUnits { get; init; }

    public required string Currency { get; init; }

    /// <summary>The donor's own copy of the record, and an anonymous donor's only key to it.</summary>
    public required string ReceiptReference { get; init; }

    public string? CountryCode { get; init; }

    public required DateTimeOffset CompletedAt { get; init; }
}
