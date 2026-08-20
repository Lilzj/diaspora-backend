using Diaspora.Modules.Marketplace.Domain.Errors;
using Diaspora.Modules.Marketplace.Domain.Policies;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Marketplace.Domain.ValueObjects;

/// <summary>
/// What the seller is asking.
/// <para>
/// <b>No conversion happens here, ever.</b> A stored figure is the seller's figure in the
/// seller's currency. Converting for display or for a range filter needs a rate, a rate has a
/// date, and a price silently restated at yesterday's rate is a number a buyer would act on and
/// be wrong about. The multi-currency requirement in the brief is met by carrying the currency
/// everywhere and comparing only within one — a filter that matches nothing is better than one
/// that matches wrongly.
/// </para>
/// <para>
/// <b>Free and "ask me" are first-class states, not nulls.</b> A giveaway is priced at zero and
/// says so; a wanted advert with no budget publishes no figure and says that. Modelling either as
/// "no price object" makes every read path do a null dance and every filter guess.
/// </para>
/// <para>
/// <b><see cref="MinimumOffer"/> is never published.</b> It is the floor the seller will
/// entertain, used to refuse an insulting offer before it reaches them; the mapper does not put
/// it on a buyer-facing response, and the integration event does not carry it. A visible floor is
/// just a lower asking price.
/// </para>
/// <para>
/// The factories are properties returning a fresh instance rather than cached statics. B6 records
/// why: an owned instance shared between two aggregates in one <c>DbContext</c> is tracked
/// against whichever owner EF saw first, and the second silently writes nulls into NOT NULL
/// columns.
/// </para>
/// </summary>
public sealed class ListingPrice : ValueObject
{
    private ListingPrice(
        bool isDisclosed,
        decimal? amount,
        string? currencyCode,
        bool isNegotiable,
        decimal? minimumOffer)
    {
        IsDisclosed = isDisclosed;
        Amount = amount;
        CurrencyCode = currencyCode;
        IsNegotiable = isNegotiable;
        MinimumOffer = minimumOffer;
    }

    private ListingPrice() { }

    /// <summary>Whether the seller published a figure at all.</summary>
    public bool IsDisclosed { get; private set; }

    /// <summary>The asking price. Zero on a giveaway, null when nothing was published.</summary>
    public decimal? Amount { get; private set; }

    /// <summary>Upper-case ISO 4217. Null only when nothing was published.</summary>
    public string? CurrencyCode { get; private set; }

    /// <summary>Whether the seller invites offers.</summary>
    public bool IsNegotiable { get; private set; }

    /// <summary>The floor the seller will entertain. Never rendered to a buyer.</summary>
    public decimal? MinimumOffer { get; private set; }

    /// <summary>Nothing published. What a wanted advert with no budget carries.</summary>
    public static ListingPrice OnRequest =>
        new(false, null, null, isNegotiable: true, null);

    /// <summary>Whether the item is free.</summary>
    public bool IsFree => IsDisclosed && Amount == 0m;

    /// <summary>A stated price, taken or left.</summary>
    public static Result<ListingPrice> Fixed(decimal amount, string? currencyCode) =>
        Build(amount, currencyCode, isNegotiable: false, minimumOffer: null);

    /// <summary>A stated price that invites offers, with an optional private floor.</summary>
    public static Result<ListingPrice> Negotiable(
        decimal amount, string? currencyCode, decimal? minimumOffer = null) =>
        Build(amount, currencyCode, isNegotiable: true, minimumOffer);

    /// <summary>Free to a good home. Still carries a currency, so the card can say "Free".</summary>
    public static Result<ListingPrice> Free(string? currencyCode) =>
        Build(0m, currencyCode, isNegotiable: false, minimumOffer: null);

    /// <summary>
    /// The general factory the handlers use, after they have parsed the wire request.
    /// </summary>
    public static Result<ListingPrice> Create(
        decimal? amount, string? currencyCode, bool isNegotiable, decimal? minimumOffer)
    {
        if (amount is null)
        {
            // Nothing published. A private floor without a public figure is meaningless, so it
            // is dropped rather than silently kept somewhere nobody will look for it.
            return Result.Success(new ListingPrice(false, null, null, isNegotiable, null));
        }

        return Build(amount.Value, currencyCode, isNegotiable, minimumOffer);
    }

    /// <summary>Whether an offer of this amount clears the seller's floor.</summary>
    public bool Accepts(decimal offered) =>
        MinimumOffer is not { } floor || offered >= floor;

    /// <summary>Whether an offer in this currency can be compared with the asking price at all.</summary>
    public bool SharesCurrencyWith(string? offeredCurrency) =>
        CurrencyCode is { } currency
        && string.Equals(
            currency, NormalizeCurrency(offeredCurrency), StringComparison.Ordinal);

    /// <summary>Upper-cases and validates an ISO 4217 code. Null when it is not one.</summary>
    public static string? NormalizeCurrency(string? currencyCode)
    {
        if (string.IsNullOrWhiteSpace(currencyCode))
        {
            return null;
        }

        var trimmed = currencyCode.Trim().ToUpperInvariant();

        return trimmed.Length == MarketplaceLimits.MaxCurrencyCodeLength
            && trimmed.All(char.IsAsciiLetterUpper)
            ? trimmed
            : null;
    }

    private static Result<ListingPrice> Build(
        decimal amount, string? currencyCode, bool isNegotiable, decimal? minimumOffer)
    {
        if (amount < 0m || amount > MarketplaceLimits.MaxPrice)
        {
            return Result.Failure<ListingPrice>(MarketplaceErrors.PriceInvalid);
        }

        var currency = NormalizeCurrency(currencyCode);

        if (currency is null)
        {
            return Result.Failure<ListingPrice>(MarketplaceErrors.CurrencyInvalid);
        }

        // A floor above the asking price is a seller who has confused the two fields, and
        // accepting it would refuse every offer including one at the full asking price.
        if (minimumOffer is { } floor && (floor < 0m || floor > amount))
        {
            return Result.Failure<ListingPrice>(MarketplaceErrors.PriceInvalid);
        }

        // A floor on a non-negotiable price is meaningless; dropping it is kinder than refusing
        // the save over a field the seller cannot see.
        return Result.Success(
            new ListingPrice(true, amount, currency, isNegotiable, isNegotiable ? minimumOffer : null));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return IsDisclosed;
        yield return Amount;
        yield return CurrencyCode;
        yield return IsNegotiable;
        yield return MinimumOffer;
    }
}
