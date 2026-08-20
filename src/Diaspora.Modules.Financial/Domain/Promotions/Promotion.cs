using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Promotions;

/// <summary>
/// A coupon, campaign discount or referral reward, as reference data.
/// <para>
/// <b>The refusal is the interesting part.</b> <see cref="Evaluate"/> answers <em>why</em> a
/// promotion does not apply — expired, exhausted, wrong country, wrong product, below the minimum
/// spend, already used by this member — because a coupon that silently does nothing is worse than
/// one that is refused. Every one of those is a different message on the client and a different
/// action for the member.
/// </para>
/// <para>
/// <b><see cref="RedeemedCount"/> is maintained here and reconciled from the redemption table.</b>
/// A counter is the only way to enforce a usage limit without counting rows on the hot path, and it
/// is also the only field on this aggregate that can drift. The drift is bounded, cosmetic and
/// corrected by a sweep — B6 recorded the same trade for its own counters.
/// </para>
/// </summary>
public sealed class Promotion : AggregateRoot<PromotionId>, IAuditableEntity
{
    private Promotion(
        PromotionId id,
        string code,
        string name,
        PromotionKind kind,
        Discount discount,
        DateWindow window,
        DateTimeOffset createdAt) : base(id)
    {
        Code = code;
        Name = name;
        Kind = kind;
        Discount = discount;
        Window = window;
        Status = PromotionStatus.Draft;
        CreatedAt = createdAt;
    }

    private Promotion() { }

    /// <summary>What somebody types. Upper case, unique.</summary>
    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public PromotionKind Kind { get; private set; }

    public PromotionStatus Status { get; private set; }

    public Discount Discount { get; private set; } = default!;

    /// <summary>When it can be redeemed.</summary>
    public DateWindow Window { get; private set; } = default!;

    /// <summary>Total redemptions allowed. Null means unlimited.</summary>
    public int? MaxRedemptions { get; private set; }

    /// <summary>Redemptions allowed per member. Null means unlimited.</summary>
    public int? MaxPerSubject { get; private set; }

    public int RedeemedCount { get; private set; }

    /// <summary>Comma-separated ISO country codes. Empty means everywhere.</summary>
    public string CountryCodes { get; private set; } = string.Empty;

    /// <summary>Comma-separated product codes. Empty means every product.</summary>
    public string ProductCodes { get; private set; } = string.Empty;

    /// <summary>The smallest purchase this applies to. Zero means any.</summary>
    public long MinimumSpendMinorUnits { get; private set; }

    /// <summary>Required when a minimum spend or a fixed discount is set.</summary>
    public string? Currency { get; private set; }

    public int Revision { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public static Promotion Define(
        PromotionId id,
        string code,
        string name,
        PromotionKind kind,
        Discount discount,
        DateWindow window,
        DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(discount);
        ArgumentNullException.ThrowIfNull(window);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new Promotion(
            id, code.Trim().ToUpperInvariant(), name.Trim(), kind, discount, window, createdAt);
    }

    public void SetLimits(int? maxRedemptions, int? maxPerSubject, DateTimeOffset now)
    {
        MaxRedemptions = maxRedemptions is > 0 ? maxRedemptions : null;
        MaxPerSubject = maxPerSubject is > 0 ? maxPerSubject : null;
        Touch(now);
    }

    public void SetScope(
        IEnumerable<string>? countryCodes,
        IEnumerable<string>? productCodes,
        Money? minimumSpend,
        DateTimeOffset now)
    {
        CountryCodes = string.Join(
            ',',
            (countryCodes ?? [])
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Select(code => code.Trim().ToUpperInvariant())
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal));

        ProductCodes = string.Join(
            ',',
            (productCodes ?? [])
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Select(code => code.Trim().ToLowerInvariant())
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal));

        MinimumSpendMinorUnits = minimumSpend?.MinorUnits ?? 0;
        Currency = minimumSpend?.Currency ?? Discount.Currency;

        Touch(now);
    }

    public void Activate(DateTimeOffset now)
    {
        Status = PromotionStatus.Active;
        Touch(now);
    }

    public void Pause(DateTimeOffset now)
    {
        if (Status == PromotionStatus.Active)
        {
            Status = PromotionStatus.Paused;
            Touch(now);
        }
    }

    public void Expire(DateTimeOffset now)
    {
        if (Status is PromotionStatus.Expired or PromotionStatus.Exhausted)
        {
            return;
        }

        Status = PromotionStatus.Expired;
        Touch(now);
    }

    /// <summary>
    /// Whether this promotion applies, and if not, which refusal it is.
    /// <para>
    /// Takes the member's prior redemption count rather than looking it up, because the aggregate
    /// cannot query and a value passed in is a value the caller can be tested against.
    /// </para>
    /// </summary>
    public string? Evaluate(
        Money price,
        string? productCode,
        string? countryCode,
        int redemptionsBySubject,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(price);

        if (Status is PromotionStatus.Draft or PromotionStatus.Paused)
        {
            return FinancialErrorCodes.PromotionNotApplicable;
        }

        if (Status == PromotionStatus.Exhausted)
        {
            return FinancialErrorCodes.PromotionExhausted;
        }

        if (Status == PromotionStatus.Expired || !Window.Contains(now))
        {
            return FinancialErrorCodes.PromotionExpired;
        }

        if (MaxRedemptions is { } total && RedeemedCount >= total)
        {
            return FinancialErrorCodes.PromotionExhausted;
        }

        if (MaxPerSubject is { } perSubject && redemptionsBySubject >= perSubject)
        {
            return FinancialErrorCodes.PromotionAlreadyRedeemed;
        }

        if (CountryCodes.Length > 0 && !ListContains(CountryCodes, countryCode))
        {
            return FinancialErrorCodes.PromotionCountryRestricted;
        }

        if (ProductCodes.Length > 0 && !ListContains(ProductCodes, productCode))
        {
            return FinancialErrorCodes.PromotionNotApplicable;
        }

        if (MinimumSpendMinorUnits > 0)
        {
            if (!string.Equals(Currency, price.Currency, StringComparison.Ordinal))
            {
                return FinancialErrorCodes.CurrencyMismatch;
            }

            if (price.MinorUnits < MinimumSpendMinorUnits)
            {
                return FinancialErrorCodes.PromotionNotApplicable;
            }
        }

        return null;
    }

    /// <summary>Records a redemption and exhausts the promotion if that was the last one.</summary>
    public void RecordRedemption(DateTimeOffset now)
    {
        RedeemedCount++;

        if (MaxRedemptions is { } total && RedeemedCount >= total)
        {
            Status = PromotionStatus.Exhausted;
        }

        Touch(now);
    }

    /// <summary>Sets the counter from a reconciliation sweep. The only way it can go down.</summary>
    public void ReconcileRedemptionCount(int actual, DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(actual);

        if (RedeemedCount == actual)
        {
            return;
        }

        RedeemedCount = actual;
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

    private static bool ListContains(string list, string? candidate) =>
        !string.IsNullOrWhiteSpace(candidate)
        && list.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains(candidate.Trim(), StringComparer.OrdinalIgnoreCase);

    private void Touch(DateTimeOffset now)
    {
        Revision++;
        ModifiedAt = now;
    }
}
