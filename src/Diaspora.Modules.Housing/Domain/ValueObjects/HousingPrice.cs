using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Errors;
using Diaspora.Modules.Housing.Domain.Policies;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Housing.Domain.ValueObjects;

/// <summary>
/// What it costs to live there.
/// <para>
/// <b>Nothing in this module converts a currency, and nothing normalises a period.</b> A
/// conversion needs a rate, a rate has a date, and a rent silently restated at yesterday's rate is
/// a figure a member cannot act on and a landlord did not write. A price filter that matches
/// nothing is better than one that matches wrongly. B10 states the same rule for goods; it matters
/// more here, because the number is the largest recurring commitment most members will make.
/// </para>
/// <para>
/// <b>The deposit is part of the price, not a separate concern.</b> "Rent 800, deposit 2400" is
/// the single most common way a newcomer discovers they cannot afford somewhere they had already
/// arranged to view, and a model that carried the rent and left the deposit to the description
/// would hide it from every filter and every card. It is held in the same currency as the rent and
/// the aggregate refuses a mismatch.
/// </para>
/// <para>
/// <b>Price-on-application is a real state, not a zero.</b> Corporate and relocation lettings
/// genuinely do not publish figures, and storing zero for them would put them at the top of every
/// cheapest-first page.
/// </para>
/// </summary>
public sealed class HousingPrice : ValueObject
{
    private HousingPrice(
        bool isDisclosed,
        decimal? amount,
        string? currencyCode,
        RentPeriod period,
        decimal? depositAmount,
        bool isNegotiable,
        bool utilitiesIncluded,
        bool internetIncluded)
    {
        IsDisclosed = isDisclosed;
        Amount = amount;
        CurrencyCode = currencyCode;
        Period = period;
        DepositAmount = depositAmount;
        IsNegotiable = isNegotiable;
        UtilitiesIncluded = utilitiesIncluded;
        InternetIncluded = internetIncluded;
    }

    private HousingPrice() { }

    /// <summary>Whether a figure was published at all.</summary>
    public bool IsDisclosed { get; private set; }

    /// <summary>The rent, for the stated <see cref="Period"/>. Null when undisclosed.</summary>
    public decimal? Amount { get; private set; }

    /// <summary>ISO 4217, upper case. Null when undisclosed.</summary>
    public string? CurrencyCode { get; private set; }

    public RentPeriod Period { get; private set; }

    /// <summary>The deposit, in the same currency as the rent. Null when there is none.</summary>
    public decimal? DepositAmount { get; private set; }

    public bool IsNegotiable { get; private set; }

    /// <summary>Whether the rent covers utilities. A filter, not prose.</summary>
    public bool UtilitiesIncluded { get; private set; }

    /// <summary>Whether the rent covers internet. Its own flag, because remote workers filter on it.</summary>
    public bool InternetIncluded { get; private set; }

    /// <summary>Whether a deposit is required at all.</summary>
    public bool DepositRequired => DepositAmount is > 0m;

    /// <summary>
    /// Everything payable before moving in, where both figures are known. What a member is
    /// actually being asked for on day one, and the number no listing states plainly.
    /// </summary>
    public decimal? MoveInTotal =>
        IsDisclosed && Amount is { } rent ? rent + (DepositAmount ?? 0m) : null;

    public static HousingPrice OnApplication(
        bool utilitiesIncluded = false, bool internetIncluded = false) =>
        new(false, null, null, RentPeriod.None, null, false, utilitiesIncluded, internetIncluded);

    public static Result<HousingPrice> Create(
        decimal? amount,
        string? currencyCode,
        RentPeriod period,
        decimal? depositAmount = null,
        bool isNegotiable = false,
        bool utilitiesIncluded = false,
        bool internetIncluded = false)
    {
        if (amount is null)
        {
            return Result.Success(OnApplication(utilitiesIncluded, internetIncluded));
        }

        if (amount < 0m || amount > HousingLimits.MaxAmount)
        {
            return Result.Failure<HousingPrice>(HousingErrors.PriceInvalid);
        }

        var currency = NormalizeCurrency(currencyCode);

        if (currency is null)
        {
            return Result.Failure<HousingPrice>(HousingErrors.CurrencyInvalid);
        }

        if (period == RentPeriod.None)
        {
            return Result.Failure<HousingPrice>(HousingErrors.PriceRequired);
        }

        if (depositAmount is { } deposit && (deposit < 0m || deposit > HousingLimits.MaxAmount))
        {
            return Result.Failure<HousingPrice>(HousingErrors.DepositInvalid);
        }

        return Result.Success(new HousingPrice(
            true,
            decimal.Round(amount.Value, 2),
            currency,
            period,
            depositAmount is { } value ? decimal.Round(value, 2) : null,
            isNegotiable,
            utilitiesIncluded,
            internetIncluded));
    }

    public static string? NormalizeCurrency(string? currencyCode)
    {
        if (string.IsNullOrWhiteSpace(currencyCode))
        {
            return null;
        }

        var trimmed = currencyCode.Trim().ToUpperInvariant();

        return trimmed.Length == HousingLimits.CurrencyCodeLength
               && trimmed.All(char.IsAsciiLetterUpper)
            ? trimmed
            : null;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return IsDisclosed;
        yield return Amount;
        yield return CurrencyCode;
        yield return Period;
        yield return DepositAmount;
        yield return IsNegotiable;
        yield return UtilitiesIncluded;
        yield return InternetIncluded;
    }
}
