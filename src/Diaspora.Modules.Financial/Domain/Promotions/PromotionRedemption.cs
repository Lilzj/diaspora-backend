using Diaspora.Modules.Financial.Domain.Events;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Promotions;

/// <summary>
/// One use of one promotion by one member.
/// <para>
/// <b>Its own aggregate rather than a collection on the promotion.</b> A campaign discount used by
/// forty thousand people would make the promotion aggregate unloadable, and the question actually
/// asked at redemption time — "has <em>this</em> member used <em>this</em> code" — is an indexed
/// lookup rather than a scan of a collection.
/// </para>
/// <para>
/// The row is also the audit trail: what a promotion cost the platform is the sum of this table,
/// not a number recomputed from a discount that may since have been edited.
/// </para>
/// </summary>
public sealed class PromotionRedemption : AggregateRoot<PromotionRedemptionId>
{
    private PromotionRedemption(
        PromotionRedemptionId id,
        PromotionId promotionId,
        string promotionCode,
        Guid redeemedByUserId,
        Money discount,
        DateTimeOffset redeemedAt) : base(id)
    {
        PromotionId = promotionId;
        PromotionCode = promotionCode;
        RedeemedByUserId = redeemedByUserId;
        DiscountMinorUnits = discount.MinorUnits;
        Currency = discount.Currency;
        Exponent = discount.Exponent;
        RedeemedAt = redeemedAt;
    }

    private PromotionRedemption() { }

    public PromotionId PromotionId { get; private set; } = default!;

    public string PromotionCode { get; private set; } = string.Empty;

    public Guid RedeemedByUserId { get; private set; }

    public long DiscountMinorUnits { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public int Exponent { get; private set; } = Money.DefaultExponent;

    public MonetizationPurchaseId? PurchaseId { get; private set; }

    public string? ProductCode { get; private set; }

    public string? CountryCode { get; private set; }

    public DateTimeOffset RedeemedAt { get; private set; }

    public Money Discount => Money.FromMinorUnits(DiscountMinorUnits, Currency, Exponent);

    public static PromotionRedemption Record(
        PromotionRedemptionId id,
        PromotionId promotionId,
        string promotionCode,
        Guid redeemedByUserId,
        Money discount,
        DateTimeOffset redeemedAt,
        MonetizationPurchaseId? purchaseId = null,
        string? productCode = null,
        string? countryCode = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(promotionId);
        ArgumentNullException.ThrowIfNull(discount);

        var redemption = new PromotionRedemption(
            id, promotionId, promotionCode.Trim().ToUpperInvariant(),
            redeemedByUserId, discount, redeemedAt)
        {
            PurchaseId = purchaseId,
            ProductCode = string.IsNullOrWhiteSpace(productCode)
                ? null
                : productCode.Trim().ToLowerInvariant(),
            CountryCode = string.IsNullOrWhiteSpace(countryCode)
                ? null
                : countryCode.Trim().ToUpperInvariant()
        };

        redemption.RaiseDomainEvent(new PromotionRedeemedDomainEvent
        {
            RedemptionId = id,
            PromotionId = promotionId,
            PromotionCode = redemption.PromotionCode,
            RedeemedByUserId = redeemedByUserId,
            DiscountMinorUnits = discount.MinorUnits,
            Currency = discount.Currency,
            ProductCode = redemption.ProductCode,
            RedeemedAt = redeemedAt
        });

        return redemption;
    }
}
