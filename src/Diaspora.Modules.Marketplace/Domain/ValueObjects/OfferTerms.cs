using Diaspora.Modules.Marketplace.Domain.Errors;
using Diaspora.Modules.Marketplace.Domain.Policies;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Marketplace.Domain.ValueObjects;

/// <summary>
/// What is on the table right now.
/// <para>
/// <b>The currency is carried on every offer, and it is checked against the listing's.</b>
/// Nothing in this module converts, so an offer in a currency the listing is not priced in is
/// refused rather than silently compared — a seller reading "1,200" against an asking price of
/// "1,500" has no way to know the first was in a different unit.
/// </para>
/// <para>
/// <b>Quantity is part of the terms, not a separate negotiation.</b> "Three of them for ninety"
/// is one offer; splitting it into a price and a quantity that can be accepted independently
/// would let a seller accept the price and change the number.
/// </para>
/// </summary>
public sealed class OfferTerms : ValueObject
{
    private OfferTerms(decimal amount, string currencyCode, int quantity, string? message)
    {
        Amount = amount;
        CurrencyCode = currencyCode;
        Quantity = quantity;
        Message = message;
    }

    private OfferTerms() { }

    /// <summary>The total offered, for the whole quantity.</summary>
    public decimal Amount { get; private set; }

    /// <summary>Upper-case ISO 4217. Always the listing's currency.</summary>
    public string CurrencyCode { get; private set; } = string.Empty;

    public int Quantity { get; private set; }

    /// <summary>What the party said when they made the move. Plain text.</summary>
    public string? Message { get; private set; }

    /// <summary>The per-unit figure, for a card that wants to show one.</summary>
    public decimal UnitAmount => Quantity <= 0 ? Amount : Amount / Quantity;

    public static Result<OfferTerms> Create(
        decimal amount, string? currencyCode, int quantity, string? message)
    {
        if (amount <= 0m || amount > MarketplaceLimits.MaxPrice)
        {
            return Result.Failure<OfferTerms>(MarketplaceErrors.OfferAmountInvalid);
        }

        if (quantity < 1 || quantity > MarketplaceLimits.MaxQuantity)
        {
            return Result.Failure<OfferTerms>(MarketplaceErrors.OfferQuantityInvalid);
        }

        var currency = ListingPrice.NormalizeCurrency(currencyCode);

        if (currency is null)
        {
            return Result.Failure<OfferTerms>(MarketplaceErrors.CurrencyInvalid);
        }

        var trimmed = string.IsNullOrWhiteSpace(message) ? null : message.Trim();

        if (trimmed is { Length: > MarketplaceLimits.MaxOfferMessage })
        {
            return Result.Failure<OfferTerms>(MarketplaceErrors.OfferAmountInvalid);
        }

        return Result.Success(new OfferTerms(amount, currency, quantity, trimmed));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Amount;
        yield return CurrencyCode;
        yield return Quantity;
        yield return Message;
    }
}
