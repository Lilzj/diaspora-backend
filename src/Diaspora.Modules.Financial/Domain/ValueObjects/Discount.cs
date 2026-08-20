using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.ValueObjects;

/// <summary>
/// How much a promotion takes off, and how.
/// <para>
/// <b>Flat rather than nesting a <see cref="Money"/>, and that is a persistence decision made in the
/// domain on purpose.</b> A discount is an owned value on a promotion; nesting an owned money inside
/// an owned discount is two levels of owned type, which EF supports and which the platform has twice
/// found produces column names nobody predicted. The fixed amount is therefore minor units plus a
/// currency, in the shape everything else in this module stores money.
/// </para>
/// <para>
/// <b>A percentage discount is capped or it is not a discount.</b> Ten percent off a credit bundle is
/// a promotion; ten percent off an unbounded purchase with no ceiling is an open cheque, and
/// <see cref="MaximumMinorUnits"/> exists so an operator cannot forget to write one.
/// </para>
/// </summary>
public sealed class Discount : ValueObject
{
    private Discount(
        DiscountKind kind, decimal percentage, long fixedMinorUnits, string? currency, long? maximumMinorUnits)
    {
        Kind = kind;
        Percentage = percentage;
        FixedMinorUnits = fixedMinorUnits;
        Currency = currency;
        MaximumMinorUnits = maximumMinorUnits;
    }

    private Discount() { }

    public DiscountKind Kind { get; private set; }

    /// <summary>0 to 100. Zero unless the kind is a percentage.</summary>
    public decimal Percentage { get; private set; }

    /// <summary>Zero unless the kind is a fixed amount.</summary>
    public long FixedMinorUnits { get; private set; }

    /// <summary>Required for a fixed amount, null otherwise. A fixed discount cannot cross currency.</summary>
    public string? Currency { get; private set; }

    /// <summary>The ceiling on a percentage discount. Null means uncapped, which is rarely intended.</summary>
    public long? MaximumMinorUnits { get; private set; }

    public static Discount OfPercentage(decimal percentage, long? maximumMinorUnits = null) =>
        new(DiscountKind.Percentage, Math.Clamp(percentage, 0m, 100m), 0, null, maximumMinorUnits);

    public static Discount Fixed(Money amount)
    {
        ArgumentNullException.ThrowIfNull(amount);

        return new Discount(DiscountKind.FixedAmount, 0m, amount.MinorUnits, amount.Currency, null);
    }

    public static Discount Free() => new(DiscountKind.Free, 100m, 0, null, null);

    /// <summary>
    /// What this discount takes off a price.
    /// <para>
    /// Never more than the price itself, and never in a currency the price is not in: a fixed
    /// discount denominated in another currency applies nothing rather than converting, which is the
    /// platform's rule everywhere money meets money.
    /// </para>
    /// </summary>
    public Money AmountOff(Money price)
    {
        ArgumentNullException.ThrowIfNull(price);

        var raw = Kind switch
        {
            DiscountKind.Free => price,
            DiscountKind.Percentage => Cap(price.MultiplyBy(Percentage / 100m), price),
            DiscountKind.FixedAmount when string.Equals(Currency, price.Currency, StringComparison.Ordinal) =>
                Money.FromMinorUnits(FixedMinorUnits, price.Currency, price.Exponent),
            _ => Money.Zero(price.Currency, price.Exponent)
        };

        return raw.LesserOf(price);
    }

    private Money Cap(Money computed, Money price) =>
        MaximumMinorUnits is { } ceiling
            ? computed.LesserOf(Money.FromMinorUnits(ceiling, price.Currency, price.Exponent))
            : computed;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Kind;
        yield return Percentage;
        yield return FixedMinorUnits;
        yield return Currency;
        yield return MaximumMinorUnits;
    }
}
