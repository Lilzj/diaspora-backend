using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Events;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Purchases;

/// <summary>
/// One member buying one product once.
/// <para>
/// <b>The list price, the discount and what was actually paid are three separate stored numbers.</b>
/// Keeping only the paid amount would make "how much did promotions cost us" unanswerable, and
/// recomputing it later from today's catalogue would answer it wrongly — the product may have been
/// repriced twice since. The purchase records what the transaction was at the moment it happened,
/// including the policy version that decided the price.
/// </para>
/// <para>
/// <b>A purchase is recorded before it is paid.</b> It starts pending, moves to completed when the
/// payment captures, and grants nothing before that — because an authorisation can still fail and a
/// boost granted on authorisation is a boost somebody got for free.
/// </para>
/// </summary>
public sealed class MonetizationPurchase : AggregateRoot<MonetizationPurchaseId>, IAuditableEntity
{
    private MonetizationPurchase(
        MonetizationPurchaseId id,
        Guid buyerUserId,
        FinancialAccountId accountId,
        MonetizationProductId productId,
        string productCode,
        MonetizationProductCategory category,
        SubjectReference subject,
        Money listPrice,
        Money paidPrice,
        DateTimeOffset createdAt) : base(id)
    {
        BuyerUserId = buyerUserId;
        AccountId = accountId;
        ProductId = productId;
        ProductCode = productCode;
        Category = category;
        Subject = subject;
        ListMinorUnits = listPrice.MinorUnits;
        PaidMinorUnits = paidPrice.MinorUnits;
        DiscountMinorUnits = listPrice.MinorUnits - paidPrice.MinorUnits;
        Currency = listPrice.Currency;
        Exponent = listPrice.Exponent;
        Status = PurchaseStatus.Pending;
        CreatedAt = createdAt;
    }

    private MonetizationPurchase() { }

    public Guid BuyerUserId { get; private set; }

    public FinancialAccountId AccountId { get; private set; } = default!;

    public MonetizationProductId ProductId { get; private set; } = default!;

    /// <summary>Denormalised so a purchase reads without a join, and stable if the row is retired.</summary>
    public string ProductCode { get; private set; } = string.Empty;

    public MonetizationProductCategory Category { get; private set; }

    /// <summary>What was bought for. Never resolved here.</summary>
    public SubjectReference Subject { get; private set; } = default!;

    public PurchaseStatus Status { get; private set; }

    public long ListMinorUnits { get; private set; }

    public long DiscountMinorUnits { get; private set; }

    public long PaidMinorUnits { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public int Exponent { get; private set; } = Money.DefaultExponent;

    public PaymentIntentId? PaymentIntentId { get; private set; }

    public PromotionId? PromotionId { get; private set; }

    public string? PromotionCode { get; private set; }

    public PlacementId? PlacementId { get; private set; }

    /// <summary>
    /// Set when this purchase renews an existing placement rather than granting a new one.
    /// <para>
    /// <b>The one bit that tells the capture handler to extend rather than activate.</b> Renewing has
    /// to produce a longer window on the placement that exists, not a second placement — B15 refuses
    /// overlapping placements, so a renewal that created one would either be refused or start when the
    /// first ended, and neither is what "renew" means. Storing the days here rather than re-deriving
    /// them from the product at capture time keeps the renewal honest if the product is repriced or
    /// retired in between.
    /// </para>
    /// </summary>
    public int? RenewalDays { get; private set; }

    /// <summary>The policy version that decided the price, when a policy did.</summary>
    public string? PolicyVersion { get; private set; }

    public string? CountryCode { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public string? FailureReason { get; private set; }

    /// <summary>Bumped on every change. The optimistic-concurrency token.</summary>
    public int Revision { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public Money ListPrice => Money.FromMinorUnits(ListMinorUnits, Currency, Exponent);

    public Money Paid => Money.FromMinorUnits(PaidMinorUnits, Currency, Exponent);

    public Money Discount => Money.FromMinorUnits(DiscountMinorUnits, Currency, Exponent);

    public static MonetizationPurchase Record(
        MonetizationPurchaseId id,
        Guid buyerUserId,
        FinancialAccountId accountId,
        MonetizationProductId productId,
        string productCode,
        MonetizationProductCategory category,
        SubjectReference subject,
        Money listPrice,
        Money paidPrice,
        DateTimeOffset createdAt,
        string? policyVersion = null,
        string? countryCode = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(listPrice);
        ArgumentNullException.ThrowIfNull(paidPrice);

        if (!listPrice.SharesCurrencyWith(paidPrice))
        {
            throw new DomainException(
                "A discount cannot change the currency of a price.",
                FinancialErrorCodes.CurrencyMismatch);
        }

        if (paidPrice > listPrice)
        {
            throw new DomainException(
                "Paying more than the list price is not a purchase this platform can express.",
                FinancialErrorCodes.AmountInvalid);
        }

        return new MonetizationPurchase(
            id, buyerUserId, accountId, productId, productCode.Trim().ToLowerInvariant(),
            category, subject, listPrice, paidPrice, createdAt)
        {
            PolicyVersion = string.IsNullOrWhiteSpace(policyVersion) ? null : policyVersion.Trim(),
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

    public void AttachPromotion(PromotionId promotionId, string promotionCode)
    {
        ArgumentNullException.ThrowIfNull(promotionId);

        PromotionId = promotionId;
        PromotionCode = promotionCode?.Trim().ToUpperInvariant();
        Revision++;
    }

    /// <summary>
    /// Marks this purchase as a renewal of an existing placement, and by how many days.
    /// </summary>
    public void AttachRenewal(PlacementId placementId, int days)
    {
        ArgumentNullException.ThrowIfNull(placementId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(days);

        PlacementId = placementId;
        RenewalDays = days;
        Revision++;
    }

    public void AttachPlacement(PlacementId placementId)
    {
        ArgumentNullException.ThrowIfNull(placementId);

        PlacementId = placementId;
        Revision++;
    }

    public void Complete(DateTimeOffset now)
    {
        if (Status == PurchaseStatus.Completed)
        {
            return;
        }

        if (Status is PurchaseStatus.Refunded or PurchaseStatus.Cancelled)
        {
            throw new DomainException(
                $"A {Status} purchase cannot be completed.",
                FinancialErrorCodes.PurchaseAlreadySettled);
        }

        Status = PurchaseStatus.Completed;
        CompletedAt = now;
        Touch(now);

        RaiseDomainEvent(new MonetizationProductPurchasedDomainEvent
        {
            PurchaseId = Id,
            BuyerUserId = BuyerUserId,
            ProductCode = ProductCode,
            Category = Category,
            SubjectType = Subject.SubjectType,
            SubjectId = Subject.SubjectId,
            PaidMinorUnits = PaidMinorUnits,
            ListMinorUnits = ListMinorUnits,
            Currency = Currency,
            PromotionCode = PromotionCode,
            PolicyVersion = PolicyVersion,
            CountryCode = CountryCode,
            PurchasedAt = now
        });
    }

    public void Fail(string reason, DateTimeOffset now)
    {
        if (Status == PurchaseStatus.Completed)
        {
            throw new DomainException(
                "A completed purchase cannot be failed. Refund it.",
                FinancialErrorCodes.PurchaseAlreadySettled);
        }

        Status = PurchaseStatus.Failed;
        FailureReason = string.IsNullOrWhiteSpace(reason) ? "Failed." : reason.Trim();
        Touch(now);
    }

    public void Cancel(DateTimeOffset now)
    {
        if (Status == PurchaseStatus.Completed)
        {
            throw new DomainException(
                "A completed purchase cannot be cancelled. Refund it.",
                FinancialErrorCodes.PurchaseAlreadySettled);
        }

        Status = PurchaseStatus.Cancelled;
        Touch(now);
    }

    public void MarkRefunded(DateTimeOffset now)
    {
        Status = PurchaseStatus.Refunded;
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
