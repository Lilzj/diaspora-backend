using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.ValueObjects;

/// <summary>
/// An amount in a currency, stored exactly.
/// <para>
/// <b>Minor units in a 64-bit integer, never a decimal.</b> <c>decimal</c> is exact in .NET and is
/// not exact once it has been through a JSON parser, a JavaScript client, an analytics pipeline or a
/// column somebody declared with the wrong scale. A financial platform that stored a display format
/// and reconciled it later would be storing the wrong thing; every ledger entry, every balance and
/// every price on this platform is an integer count of the smallest unit the currency has.
/// <see cref="Amount"/> is derived on the way out for the clients that read it.
/// </para>
/// <para>
/// <b>Always non-negative.</b> A negative amount is not a thing that exists — the direction of a
/// movement is <c>LedgerDirection</c>, carried on the entry beside the amount. Allowing a signed
/// amount would give the platform two ways to express one debit and a guarantee that somebody would
/// eventually combine them.
/// </para>
/// <para>
/// <b>No conversion, on any surface, ever.</b> Two amounts in different currencies cannot be added,
/// compared or summed, and the type refuses rather than answering. That is Marketplace's rule stated
/// in <c>ListingPrice</c> — <em>a price silently restated at yesterday's rate is a number somebody
/// would act on and be wrong about</em> — and B15 is the module that would have been tempted to
/// break it. <c>IExchangeRateProvider</c> is a declared seam with no implementation.
/// </para>
/// <para>
/// <b>The exponent travels with the amount.</b> 2500 minor units is £25.00 in GBP, ¥2500 in JPY and
/// 2.500 KWD in Kuwaiti dinar; a client that assumed two decimal places would be wrong about a
/// quarter of the world. The exponent comes from the currency catalogue and is carried here so
/// nothing has to look it up to render a figure it already holds.
/// </para>
/// <para>
/// Instances are created through factories rather than cached statics. B6 and the platform's
/// <c>ValueObjectSharingArchitectureTests</c> record why: one owned instance shared between two
/// aggregates in a single <c>DbContext</c> is tracked against whichever owner EF saw first, and the
/// second silently writes nulls into NOT NULL columns.
/// </para>
/// </summary>
public sealed class Money : ValueObject, IComparable<Money>
{
    /// <summary>The exponent used when the currency catalogue has nothing to say. Two decimals.</summary>
    public const int DefaultExponent = 2;

    /// <summary>ISO 4217 codes are three letters. Enforced here and in the column width.</summary>
    public const int CurrencyCodeLength = 3;

    private Money(long minorUnits, string currency, int exponent)
    {
        MinorUnits = minorUnits;
        Currency = currency;
        Exponent = exponent;
    }

    private Money() { }

    /// <summary>
    /// The exact amount, as a count of the currency's smallest unit. The stored value, and the only
    /// one arithmetic is ever done on.
    /// </summary>
    public long MinorUnits { get; private set; }

    /// <summary>ISO 4217, upper case. Never converted.</summary>
    public string Currency { get; private set; } = string.Empty;

    /// <summary>
    /// How many minor units make one major unit, as a power of ten. 2 for GBP, 0 for JPY, 3 for KWD.
    /// </summary>
    public int Exponent { get; private set; } = DefaultExponent;

    /// <summary>
    /// The figure a client renders. Derived, never stored, and never the input to arithmetic.
    /// </summary>
    public decimal Amount => MinorUnits / Scale(Exponent);

    public bool IsZero => MinorUnits == 0;

    /// <summary>A fresh zero. A method rather than a cached property — see the class summary.</summary>
    public static Money Zero(string currency, int exponent = DefaultExponent) =>
        FromMinorUnits(0, currency, exponent);

    /// <summary>
    /// The canonical factory. Refuses a negative amount, an unusable currency code or an exponent
    /// outside the range ISO 4217 actually uses.
    /// </summary>
    public static Money FromMinorUnits(long minorUnits, string currency, int exponent = DefaultExponent)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(minorUnits);

        var normalized = NormalizeCurrency(currency)
            ?? throw new ArgumentException(
                $"'{currency}' is not an ISO 4217 currency code.", nameof(currency));

        if (exponent is < 0 or > 4)
        {
            throw new ArgumentOutOfRangeException(
                nameof(exponent), exponent, "No ISO 4217 currency has more than four minor digits.");
        }

        return new Money(minorUnits, normalized, exponent);
    }

    /// <summary>
    /// Builds from a major-unit figure — a price an operator typed, a request body, a seeded
    /// catalogue row. Rounds half away from zero, which is the rounding a person doing it by hand
    /// would use and the only one that does not surprise them.
    /// </summary>
    public static Money FromAmount(decimal amount, string currency, int exponent = DefaultExponent)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);

        var minorUnits = decimal.ToInt64(
            Math.Round(amount * Scale(exponent), 0, MidpointRounding.AwayFromZero));

        return FromMinorUnits(minorUnits, currency, exponent);
    }

    /// <summary>Upper-cases and validates an ISO 4217 code. Null when it is not one.</summary>
    public static string? NormalizeCurrency(string? currency)
    {
        if (string.IsNullOrWhiteSpace(currency))
        {
            return null;
        }

        var trimmed = currency.Trim().ToUpperInvariant();

        return trimmed.Length == CurrencyCodeLength && trimmed.All(char.IsAsciiLetterUpper)
            ? trimmed
            : null;
    }

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);

        return new Money(checked(MinorUnits + other.MinorUnits), Currency, Exponent);
    }

    /// <summary>
    /// Subtracts, refusing to go below zero.
    /// <para>
    /// A negative <see cref="Money"/> cannot be represented, so this throws rather than clamping.
    /// Clamping would turn "this wallet does not have enough" into "this wallet now has zero", which
    /// is a balance error that survives every test because it never fails loudly.
    /// </para>
    /// </summary>
    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);

        if (other.MinorUnits > MinorUnits)
        {
            throw new InvalidOperationException(
                $"Subtracting {other} from {this} would produce a negative amount.");
        }

        return new Money(MinorUnits - other.MinorUnits, Currency, Exponent);
    }

    /// <summary>
    /// Applies a rate — a commission percentage, a discount fraction.
    /// <para>
    /// Rounds half away from zero on the minor unit, so a 7.5% commission on 1&#160;333 minor units
    /// is 100 rather than 99.975 of something that does not exist. The residual stays with the payer,
    /// which is the conventional direction and is stated here because the alternative is equally
    /// arbitrary and must not be chosen twice.
    /// </para>
    /// </summary>
    public Money MultiplyBy(decimal factor)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(factor);

        var scaled = Math.Round(MinorUnits * factor, 0, MidpointRounding.AwayFromZero);

        return new Money(decimal.ToInt64(scaled), Currency, Exponent);
    }

    /// <summary>The smaller of two amounts in the same currency.</summary>
    public Money LesserOf(Money other)
    {
        EnsureSameCurrency(other);

        return MinorUnits <= other.MinorUnits ? this : other;
    }

    public bool SharesCurrencyWith(Money? other) =>
        other is not null && string.Equals(Currency, other.Currency, StringComparison.Ordinal);

    public int CompareTo(Money? other)
    {
        if (other is null)
        {
            return 1;
        }

        EnsureSameCurrency(other);

        return MinorUnits.CompareTo(other.MinorUnits);
    }

    /// <summary>
    /// Sums amounts that must already share a currency.
    /// <para>
    /// Returns <c>null</c> for an empty sequence rather than a zero in an invented currency. "No
    /// entries" and "entries totalling nothing" are different answers and a caller has to be able to
    /// tell them apart.
    /// </para>
    /// </summary>
    public static Money? Sum(IEnumerable<Money> amounts)
    {
        ArgumentNullException.ThrowIfNull(amounts);

        Money? total = null;

        foreach (var amount in amounts)
        {
            total = total is null ? amount : total.Add(amount);
        }

        return total;
    }

    public bool Equals(Money? other) => base.Equals(other);

    public override bool Equals(object? obj) => base.Equals(obj);

    public override int GetHashCode() => base.GetHashCode();

    public static bool operator ==(Money? left, Money? right) => Equals(left, right);

    public static bool operator !=(Money? left, Money? right) => !Equals(left, right);

    public static bool operator <(Money? left, Money? right) =>
        left is null ? right is not null : left.CompareTo(right) < 0;

    public static bool operator <=(Money? left, Money? right) =>
        left is null || left.CompareTo(right) <= 0;

    public static bool operator >(Money? left, Money? right) =>
        left is not null && left.CompareTo(right) > 0;

    public static bool operator >=(Money? left, Money? right) =>
        left is null ? right is null : left.CompareTo(right) >= 0;

    public static Money operator +(Money left, Money right)
    {
        ArgumentNullException.ThrowIfNull(left);

        return left.Add(right);
    }

    public static Money operator -(Money left, Money right)
    {
        ArgumentNullException.ThrowIfNull(left);

        return left.Subtract(right);
    }

    public override string ToString() => $"{Amount.ToString($"F{Exponent}", null)} {Currency}";

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return MinorUnits;
        yield return Currency;
        yield return Exponent;
    }

    private static decimal Scale(int exponent) => exponent switch
    {
        0 => 1m,
        1 => 10m,
        2 => 100m,
        3 => 1000m,
        _ => 10000m
    };

    private void EnsureSameCurrency(Money other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (!string.Equals(Currency, other.Currency, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Cannot combine {Currency} with {other.Currency}. The platform never converts.");
        }
    }
}
