namespace Diaspora.Modules.Financial.Contracts.V1.Responses;

/// <summary>
/// One donation.
/// <para>
/// <b>An anonymous donation carries no donor identifier, because none was stored.</b> The cost is
/// real and deliberate: it cannot appear in the donor's own history and cannot be refunded to them
/// without the receipt reference. A platform that could de-anonymise a donation for convenience is a
/// platform whose anonymity means nothing.
/// </para>
/// </summary>
public sealed record DonationResponse
{
    public required Guid DonationId { get; init; }

    /// <summary>Absent on an anonymous donation. Never a placeholder.</summary>
    public Guid? DonorUserId { get; init; }

    public required bool IsAnonymous { get; init; }

    public required string BeneficiaryType { get; init; }

    public Guid? BeneficiaryId { get; init; }

    public string? CauseCode { get; init; }

    public required MoneyResponse Amount { get; init; }

    public required string Status { get; init; }

    public string? Message { get; init; }

    /// <summary>The donor's own copy of the record, and an anonymous donor's only key to it.</summary>
    public required string ReceiptReference { get; init; }

    public string? CountryCode { get; init; }

    /// <summary>The payment, when one is still outstanding.</summary>
    public PaymentIntentResponse? Payment { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? CompletedAt { get; init; }
}
