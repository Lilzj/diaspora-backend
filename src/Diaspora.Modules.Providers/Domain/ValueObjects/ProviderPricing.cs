using Diaspora.Modules.Providers.Domain.Enums;
using Diaspora.Modules.Providers.Domain.Errors;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Providers.Domain.ValueObjects;

/// <summary>
/// What a service costs, in a form every pricing model can express.
/// <para>
/// The client has one shape — <c>priceRange: { min, max, currency }</c> — and validates
/// nothing. The result is that a free service is priced "£0 — £0", a quoted service cannot be
/// represented at all, and an inverted range renders as "£12 — £8" with no complaint. Making
/// the model explicit and validating the amounts against it is the whole of what this type adds.
/// </para>
/// <para>
/// <b>The model decides which amounts are legal, and the check is here rather than in a
/// validator.</b> A FluentValidation rule would guard the API and leave every internal
/// construction path — seeding, imports, a future scheduling module — free to produce a
/// <see cref="PricingModel.Quote"/> with a maximum. An invariant enforced at construction
/// cannot be bypassed.
/// </para>
/// <para>
/// Amounts are <see cref="decimal"/>, and that is not a stylistic preference: money in binary
/// floating point accumulates error, and this value is multiplied by quantities in every future
/// booking and invoice feature. <see cref="Currency"/> is an ISO 4217 code held as a string
/// rather than an enum, because the platform serves diaspora communities across every currency
/// there is and an enum would need a release per country.
/// </para>
/// </summary>
public sealed class ProviderPricing : ValueObject
{
    public const int CurrencyLength = 3;

    public const int MaxUnitLength = 32;

    private ProviderPricing(
        PricingModel model,
        decimal? minAmount,
        decimal? maxAmount,
        string? currency,
        string? unit,
        bool isNegotiable)
    {
        Model = model;
        MinAmount = minAmount;
        MaxAmount = maxAmount;
        Currency = currency;
        Unit = unit;
        IsNegotiable = isNegotiable;
    }

    private ProviderPricing() { }

    public PricingModel Model { get; private set; } = PricingModel.Unspecified;

    /// <summary>The single amount for a fixed price, or the floor of a range.</summary>
    public decimal? MinAmount { get; private set; }

    /// <summary>The ceiling of a range. Null for every model that is not a range.</summary>
    public decimal? MaxAmount { get; private set; }

    /// <summary>ISO 4217, upper case. Null when no amount is published.</summary>
    public string? Currency { get; private set; }

    /// <summary>What the amount is per, for <c>PerUnit</c> and <c>Subscription</c>. "kg", "month".</summary>
    public string? Unit { get; private set; }

    /// <summary>Whether the published price is a starting point. Common and worth saying.</summary>
    public bool IsNegotiable { get; private set; }

    /// <summary>Whether any amount at all is published. Drives whether a price chip renders.</summary>
    public bool HasAmount => MinAmount is not null;

    public static ProviderPricing Unspecified =>
        new(PricingModel.Unspecified, null, null, null, null, false);

    public static ProviderPricing Free =>
        new(PricingModel.Free, null, null, null, null, false);

    public static Result<ProviderPricing> Create(
        PricingModel model,
        decimal? minAmount = null,
        decimal? maxAmount = null,
        string? currency = null,
        string? unit = null,
        bool isNegotiable = false)
    {
        if (minAmount is < 0 || maxAmount is < 0)
        {
            return Result.Failure<ProviderPricing>(ProviderErrors.PricingNegative);
        }

        var normalizedUnit = string.IsNullOrWhiteSpace(unit)
            ? null
            : unit.Trim()[..Math.Min(unit.Trim().Length, MaxUnitLength)];

        // Models that publish nothing. Any amount supplied with them is a contradiction rather
        // than extra information, and silently keeping it would let "Free — £20" reach a screen.
        if (model is PricingModel.Unspecified or PricingModel.Quote or PricingModel.Free or PricingModel.Donation)
        {
            return minAmount is not null || maxAmount is not null
                ? Result.Failure<ProviderPricing>(ProviderErrors.PricingModelMismatch)
                : Result.Success(new ProviderPricing(model, null, null, null, normalizedUnit, isNegotiable));
        }

        if (minAmount is null)
        {
            return Result.Failure<ProviderPricing>(ProviderErrors.PricingModelMismatch);
        }

        var normalizedCurrency = NormalizeCurrency(currency);

        if (normalizedCurrency is null)
        {
            return Result.Failure<ProviderPricing>(ProviderErrors.PricingCurrencyInvalid);
        }

        if (model == PricingModel.Range)
        {
            if (maxAmount is null)
            {
                return Result.Failure<ProviderPricing>(ProviderErrors.PricingModelMismatch);
            }

            if (maxAmount < minAmount)
            {
                return Result.Failure<ProviderPricing>(ProviderErrors.PricingRangeInverted);
            }

            return Result.Success(new ProviderPricing(
                model, minAmount, maxAmount, normalizedCurrency, normalizedUnit, isNegotiable));
        }

        // Every remaining model is single-amount. A maximum here is the caller conflating
        // "£40 per hour" with "£40 to £60", which are different offers.
        return maxAmount is not null
            ? Result.Failure<ProviderPricing>(ProviderErrors.PricingModelMismatch)
            : Result.Success(new ProviderPricing(
                model, minAmount, null, normalizedCurrency, normalizedUnit, isNegotiable));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Model;
        yield return MinAmount;
        yield return MaxAmount;
        yield return Currency;
        yield return Unit;
        yield return IsNegotiable;
    }

    private static string? NormalizeCurrency(string? currency)
    {
        if (string.IsNullOrWhiteSpace(currency))
        {
            return null;
        }

        var normalized = currency.Trim().ToUpperInvariant();

        return normalized.Length == CurrencyLength && normalized.All(char.IsAsciiLetterUpper)
            ? normalized
            : null;
    }
}
