using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.CommunityProgramme.Domain.ValueObjects;

/// <summary>
/// What a tier costs.
/// <para>
/// <b>A price this module expresses, and never money it moves.</b> B15 is the platform's only
/// financial truth: it takes the payment, it owns the intent, it issues the receipt. What a
/// membership tier carries is an <em>intent to charge</em> — an amount and the currency it is
/// denominated in — and the currency is validated against B15's catalogue before it is stored, so
/// there is exactly one currency source on this platform.
/// </para>
/// <para>
/// <b>Minor units, like B15's own <c>Money</c>.</b> A price stored as a decimal is a price that
/// eventually gets rounded twice; storing 1250 and an exponent of 2 means "£12.50" is the same
/// number everywhere it is read. The exponent comes from B15's currency facts and is stored beside
/// the amount so a historical price still renders correctly if a currency's exponent is ever
/// corrected.
/// </para>
/// <para>
/// <b>Absent is not zero.</b> A free tier has no price at all — <c>null</c> on the column and
/// <c>null</c> on the wire. The register is explicit that "the absence is the meaning, not a zero",
/// and a zero-priced tier would render a "£0.00" nobody wants to see beside a benefit list.
/// </para>
/// </summary>
public sealed class ProgrammePrice : ValueObject
{
    private ProgrammePrice(long minorUnits, string currency, int exponent)
    {
        MinorUnits = minorUnits;
        Currency = currency;
        Exponent = exponent;
    }

    /// <summary>EF's constructor. Never called by this module's own code.</summary>
    private ProgrammePrice() { Currency = string.Empty; }

    /// <summary>The amount in the currency's smallest unit — 1250 for £12.50.</summary>
    public long MinorUnits { get; private set; }

    /// <summary>ISO 4217, upper case. Validated against B15's catalogue at the point of write.</summary>
    public string Currency { get; private set; }

    /// <summary>How many minor units make one major one. Two for GBP, zero for JPY.</summary>
    public int Exponent { get; private set; }

    /// <summary>The amount as a decimal, for rendering only.</summary>
    public decimal Amount => MinorUnits / (decimal)Math.Pow(10, Exponent);

    /// <summary>
    /// A price from a major-unit amount — what a client sends and a form collects.
    /// <para>
    /// <b>Rounds away from zero at the currency's own precision</b>, so 12.345 in a two-decimal
    /// currency becomes 1235 rather than 1234. A member is never charged more than the number they
    /// were shown, because the number they were shown is rendered back from this value.
    /// </para>
    /// </summary>
    public static ProgrammePrice FromAmount(decimal amount, string currency, int exponent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);

        var safeExponent = Math.Clamp(exponent, 0, 4);
        var scale = (decimal)Math.Pow(10, safeExponent);

        return new ProgrammePrice(
            (long)Math.Round(amount * scale, MidpointRounding.AwayFromZero),
            currency.Trim().ToUpperInvariant(),
            safeExponent);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return MinorUnits;
        yield return Currency;
        yield return Exponent;
    }

    public override string ToString() => $"{Amount} {Currency}";
}
