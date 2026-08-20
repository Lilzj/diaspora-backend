using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Events;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Giving;

/// <summary>
/// One contribution to a community fund.
/// <para>
/// <b>Only a monetary contribution has an amount, and the aggregate refuses to pretend
/// otherwise.</b> A pledge of six Saturdays at a food bank carries a <see cref="PledgeDetail"/> and
/// no <see cref="Money"/>; a box of school books carries a quantity and a unit. Forcing every
/// contribution through an amount would have produced a table full of zeroes with the real content
/// in a description nobody can count — and four fifths of the client's own support types would have
/// been unrepresentable.
/// </para>
/// <para>
/// <b>A non-monetary contribution completes immediately.</b> Nothing else has to happen for a pledge
/// to count; only the monetary kind waits for a payment.
/// </para>
/// </summary>
public sealed class CommunityContribution : AggregateRoot<CommunityContributionId>, IAuditableEntity
{
    private CommunityContribution(
        CommunityContributionId id,
        ContributionFundId fundId,
        string fundCode,
        ContributionKind kind,
        string receiptReference,
        DateTimeOffset createdAt) : base(id)
    {
        FundId = fundId;
        FundCode = fundCode;
        Kind = kind;
        ReceiptReference = receiptReference;
        Status = GivingStatus.Pending;
        CreatedAt = createdAt;
    }

    private CommunityContribution() { }

    public ContributionFundId FundId { get; private set; } = default!;

    public string FundCode { get; private set; } = string.Empty;

    /// <summary>Null when anonymous. Never a placeholder.</summary>
    public Guid? ContributorUserId { get; private set; }

    public bool IsAnonymous { get; private set; }

    public ContributionKind Kind { get; private set; }

    public GivingStatus Status { get; private set; }

    /// <summary>Zero and meaningless for every kind except <c>Monetary</c>.</summary>
    public long AmountMinorUnits { get; private set; }

    /// <summary>Null for every kind except <c>Monetary</c>.</summary>
    public string? Currency { get; private set; }

    public int Exponent { get; private set; } = Money.DefaultExponent;

    /// <summary>How much was pledged, for the four non-monetary kinds. Null for a monetary gift.</summary>
    public decimal? PledgeQuantity { get; private set; }

    /// <summary>What the organiser counts in. <c>items</c>, <c>hours</c>, <c>boxes</c>.</summary>
    public string? PledgeUnit { get; private set; }

    public string? PledgeDescription { get; private set; }

    /// <summary>
    /// The pledge as a value object, rebuilt on read.
    /// <para>
    /// Three nullable columns rather than an optional owned type, for the reason recorded on
    /// <c>LedgerEntry.Subject</c>: a monetary contribution genuinely has no pledge, and EF cannot
    /// tell an absent optional dependent from one whose properties are all null.
    /// </para>
    /// </summary>
    public PledgeDetail? Pledge =>
        PledgeQuantity is { } quantity && PledgeUnit is { } unit
            ? PledgeDetail.Create(quantity, unit, PledgeDescription ?? string.Empty)
            : null;

    public string? Message { get; private set; }

    public string ReceiptReference { get; private set; } = string.Empty;

    public PaymentIntentId? PaymentIntentId { get; private set; }

    public TransactionId? TransactionId { get; private set; }

    public string? CountryCode { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public string? FailureReason { get; private set; }

    public int Revision { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public bool IsMonetary => Kind == ContributionKind.Monetary;

    public Money? Amount => IsMonetary && Currency is not null
        ? Money.FromMinorUnits(AmountMinorUnits, Currency, Exponent)
        : null;

    /// <summary>A gift of money. Waits for a payment before it counts.</summary>
    public static CommunityContribution Monetary(
        CommunityContributionId id,
        ContributionFundId fundId,
        string fundCode,
        Guid? contributorUserId,
        bool isAnonymous,
        Money amount,
        string receiptReference,
        DateTimeOffset createdAt,
        string? message = null,
        string? countryCode = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(fundId);
        ArgumentNullException.ThrowIfNull(amount);
        ArgumentException.ThrowIfNullOrWhiteSpace(receiptReference);

        if (amount.IsZero)
        {
            throw new DomainException(
                "A monetary contribution of nothing is not a contribution.",
                FinancialErrorCodes.ContributionAmountRequired);
        }

        return new CommunityContribution(
            id, fundId, fundCode.Trim().ToLowerInvariant(),
            ContributionKind.Monetary, receiptReference.Trim(), createdAt)
        {
            ContributorUserId = isAnonymous ? null : contributorUserId,
            IsAnonymous = isAnonymous,
            AmountMinorUnits = amount.MinorUnits,
            Currency = amount.Currency,
            Exponent = amount.Exponent,
            Message = string.IsNullOrWhiteSpace(message) ? null : message.Trim(),
            CountryCode = string.IsNullOrWhiteSpace(countryCode)
                ? null
                : countryCode.Trim().ToUpperInvariant()
        };
    }

    /// <summary>
    /// A gift of goods, time or skills. Complete the moment it is made — see the class summary.
    /// </summary>
    public static CommunityContribution Pledged(
        CommunityContributionId id,
        ContributionFundId fundId,
        string fundCode,
        Guid? contributorUserId,
        bool isAnonymous,
        ContributionKind kind,
        PledgeDetail pledge,
        string receiptReference,
        DateTimeOffset createdAt,
        string? message = null,
        string? countryCode = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(fundId);
        ArgumentNullException.ThrowIfNull(pledge);
        ArgumentException.ThrowIfNullOrWhiteSpace(receiptReference);

        if (kind == ContributionKind.Monetary)
        {
            throw new DomainException(
                "A monetary contribution needs an amount, not a pledge.",
                FinancialErrorCodes.ContributionAmountRequired);
        }

        return new CommunityContribution(
            id, fundId, fundCode.Trim().ToLowerInvariant(), kind, receiptReference.Trim(), createdAt)
        {
            ContributorUserId = isAnonymous ? null : contributorUserId,
            IsAnonymous = isAnonymous,
            PledgeQuantity = pledge.Quantity,
            PledgeUnit = pledge.Unit,
            PledgeDescription = pledge.Description,
            Message = string.IsNullOrWhiteSpace(message) ? null : message.Trim(),
            CountryCode = string.IsNullOrWhiteSpace(countryCode)
                ? null
                : countryCode.Trim().ToUpperInvariant()
        };
    }

    public void AttachPayment(PaymentIntentId paymentIntentId)
    {
        ArgumentNullException.ThrowIfNull(paymentIntentId);

        PaymentIntentId = paymentIntentId;
        Revision++;
    }

    public void Complete(DateTimeOffset now, TransactionId? transactionId = null)
    {
        if (Status == GivingStatus.Completed)
        {
            return;
        }

        if (IsMonetary && transactionId is null)
        {
            throw new DomainException(
                "A monetary contribution completes against a transaction.",
                FinancialErrorCodes.ContributionAmountRequired);
        }

        Status = GivingStatus.Completed;
        TransactionId = transactionId;
        CompletedAt = now;
        Touch(now);

        RaiseDomainEvent(new CommunityContributionCompletedDomainEvent
        {
            ContributionId = Id,
            FundId = FundId,
            FundCode = FundCode,
            ContributorUserId = ContributorUserId,
            IsAnonymous = IsAnonymous,
            Kind = Kind,
            AmountMinorUnits = IsMonetary ? AmountMinorUnits : null,
            Currency = Currency,
            PledgeQuantity = PledgeQuantity,
            PledgeUnit = PledgeUnit,
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
