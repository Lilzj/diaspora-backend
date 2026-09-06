using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Events;

/// <summary>
/// A donation completed.
/// <para>
/// <b>The donor's identity is absent when the donation is anonymous, rather than present with a flag
/// beside it.</b> That is Housing's rule from the platform handbook — return the narrowed value, not
/// the value plus permission to look at it — and it is the difference between a privacy promise and
/// a privacy convention one careless mapper away from a response body.
/// </para>
/// </summary>
public sealed record DonationCompletedDomainEvent : DomainEvent
{
    public required DonationId DonationId { get; init; }

    /// <summary>The ledger transaction the capture posted.</summary>
    public TransactionId? TransactionId { get; init; }

    /// <summary>Null when the donation is anonymous. Never a placeholder.</summary>
    public Guid? DonorUserId { get; init; }

    public required bool IsAnonymous { get; init; }

    public required BeneficiaryType BeneficiaryType { get; init; }

    public Guid? BeneficiaryId { get; init; }

    public string? CauseCode { get; init; }

    public required long AmountMinorUnits { get; init; }

    public required string Currency { get; init; }

    public required string ReceiptReference { get; init; }

    public string? CountryCode { get; init; }

    public required DateTimeOffset CompletedAt { get; init; }
}
