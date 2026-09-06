using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Events;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Giving;

/// <summary>
/// One donation.
/// <para>
/// <b>Anonymity is structural, not a flag on a row that still holds the name.</b> When
/// <see cref="IsAnonymous"/> is set the donor identifier is not stored at all —
/// <see cref="DonorUserId"/> is null, and the published contract carries nothing either. That is the
/// platform's own rule from Housing: <em>return the narrowed value, not the value plus permission to
/// look at it</em>, because a full record beside a boolean is one careless mapper away from a
/// response body.
/// </para>
/// <para>
/// The cost is real and worth naming: an anonymous donation cannot appear in the donor's own giving
/// history, and cannot be refunded to them by the platform without them producing the receipt
/// reference. <b>That is the correct trade.</b> A platform that could de-anonymise a donation for
/// convenience is a platform whose anonymity means nothing.
/// </para>
/// <para>
/// <b>Recurring donations are declared and not implemented.</b> <see cref="RecurringIntervalMonths"/>
/// exists so the column, the contract and the client field are all present from the start; nothing
/// in this phase schedules one, and a sweep that did would be a subscription engine wearing a
/// donation's name.
/// </para>
/// </summary>
public sealed class Donation : AggregateRoot<DonationId>, IAuditableEntity
{
    private Donation(
        DonationId id,
        BeneficiaryType beneficiaryType,
        Money amount,
        string receiptReference,
        DateTimeOffset createdAt) : base(id)
    {
        BeneficiaryType = beneficiaryType;
        AmountMinorUnits = amount.MinorUnits;
        Currency = amount.Currency;
        Exponent = amount.Exponent;
        ReceiptReference = receiptReference;
        Status = GivingStatus.Pending;
        CreatedAt = createdAt;
    }

    private Donation() { }

    /// <summary>Null when anonymous. Never a placeholder — see the class summary.</summary>
    public Guid? DonorUserId { get; private set; }

    public bool IsAnonymous { get; private set; }

    public BeneficiaryType BeneficiaryType { get; private set; }

    public Guid? BeneficiaryId { get; private set; }

    public DonationCauseId? CauseId { get; private set; }

    public string? CauseCode { get; private set; }

    public long AmountMinorUnits { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public int Exponent { get; private set; } = Money.DefaultExponent;

    public GivingStatus Status { get; private set; }

    /// <summary>What the donor wanted to say. Shown with the donation unless anonymous.</summary>
    public string? Message { get; private set; }

    /// <summary>The donor's own copy of the record. Unique, and the only key an anonymous donor has.</summary>
    public string ReceiptReference { get; private set; } = string.Empty;

    public PaymentIntentId? PaymentIntentId { get; private set; }

    public TransactionId? TransactionId { get; private set; }

    /// <summary>Declared, never scheduled in this phase. See the class summary.</summary>
    public int? RecurringIntervalMonths { get; private set; }

    public string? CountryCode { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public string? FailureReason { get; private set; }

    public int Revision { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public Money Amount => Money.FromMinorUnits(AmountMinorUnits, Currency, Exponent);

    public static Donation Pledge(
        DonationId id,
        Guid? donorUserId,
        bool isAnonymous,
        BeneficiaryType beneficiaryType,
        Money amount,
        string receiptReference,
        DateTimeOffset createdAt,
        Guid? beneficiaryId = null,
        DonationCauseId? causeId = null,
        string? causeCode = null,
        string? message = null,
        string? countryCode = null,
        int? recurringIntervalMonths = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(amount);
        ArgumentException.ThrowIfNullOrWhiteSpace(receiptReference);

        if (amount.IsZero)
        {
            throw new DomainException(
                "A donation of nothing is not a donation.", FinancialErrorCodes.AmountInvalid);
        }

        return new Donation(id, beneficiaryType, amount, receiptReference.Trim(), createdAt)
        {
            // Not stored at all when anonymous. See the class summary.
            DonorUserId = isAnonymous ? null : donorUserId,
            IsAnonymous = isAnonymous,
            BeneficiaryId = beneficiaryId,
            CauseId = causeId,
            CauseCode = string.IsNullOrWhiteSpace(causeCode)
                ? null
                : causeCode.Trim().ToLowerInvariant(),
            Message = string.IsNullOrWhiteSpace(message) ? null : message.Trim(),
            CountryCode = string.IsNullOrWhiteSpace(countryCode)
                ? null
                : countryCode.Trim().ToUpperInvariant(),
            RecurringIntervalMonths = recurringIntervalMonths is > 0 ? recurringIntervalMonths : null
        };
    }

    public void AttachPayment(PaymentIntentId paymentIntentId)
    {
        ArgumentNullException.ThrowIfNull(paymentIntentId);

        PaymentIntentId = paymentIntentId;
        Revision++;
    }

    public void Complete(TransactionId transactionId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(transactionId);

        if (Status == GivingStatus.Completed)
        {
            return;
        }

        Status = GivingStatus.Completed;
        TransactionId = transactionId;
        CompletedAt = now;
        Touch(now);

        RaiseDomainEvent(new DonationCompletedDomainEvent
        {
            DonationId = Id,
            TransactionId = transactionId,
            DonorUserId = DonorUserId,
            IsAnonymous = IsAnonymous,
            BeneficiaryType = BeneficiaryType,
            BeneficiaryId = BeneficiaryId,
            CauseCode = CauseCode,
            AmountMinorUnits = AmountMinorUnits,
            Currency = Currency,
            ReceiptReference = ReceiptReference,
            CountryCode = CountryCode,
            CompletedAt = now
        });
    }

    public void Fail(string reason, DateTimeOffset now)
    {
        Status = GivingStatus.Failed;
        FailureReason = string.IsNullOrWhiteSpace(reason) ? "Failed." : reason.Trim();
        Touch(now);
    }

    public void MarkRefunded(DateTimeOffset now)
    {
        Status = GivingStatus.Refunded;
        Touch(now);
    }

    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }

    private void Touch(DateTimeOffset now)
    {
        Revision++;
        ModifiedAt = now;
    }
}
