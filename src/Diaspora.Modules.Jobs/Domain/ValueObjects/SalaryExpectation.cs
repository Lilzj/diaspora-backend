using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Errors;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Jobs.Domain.ValueObjects;

/// <summary>
/// What a candidate is looking to earn.
/// <para>
/// <b>Never disclosed to an employer by the platform.</b> This is a filter input the candidate
/// uses on their own searches and alerts, and nothing else reads it. Publishing a candidate's
/// expectation to the employer they are applying to would hand one side of a negotiation the
/// other side's floor — which is a thing job products do, and a thing this one will not.
/// </para>
/// <para>
/// The same currency-and-period rule as <see cref="SalaryRange"/> applies, for the same
/// cross-border reason, and no conversion happens here either.
/// </para>
/// </summary>
public sealed class SalaryExpectation : ValueObject
{
    private SalaryExpectation(
        decimal? minimum, string? currencyCode, SalaryPeriod? period, bool isFlexible)
    {
        Minimum = minimum;
        CurrencyCode = currencyCode;
        Period = period;
        IsFlexible = isFlexible;
    }

    private SalaryExpectation() { }

    /// <summary>The least the candidate will consider. Null when unstated.</summary>
    public decimal? Minimum { get; private set; }

    /// <summary>Upper-case ISO 4217. Null when unstated.</summary>
    public string? CurrencyCode { get; private set; }

    public SalaryPeriod? Period { get; private set; }

    /// <summary>Whether the candidate says the figure is movable.</summary>
    public bool IsFlexible { get; private set; }

    /// <summary>Unstated. The default, and the most common.</summary>
    // Fresh per call: a shared owned instance is tracked against one owner only. See SalaryRange.
    public static SalaryExpectation Unstated => new(null, null, null, isFlexible: true);

    public static Result<SalaryExpectation> Create(
        decimal? minimum, string? currencyCode, SalaryPeriod? period, bool isFlexible = false)
    {
        if (minimum is null)
        {
            return Result.Success(Unstated);
        }

        if (minimum < 0)
        {
            return Result.Failure<SalaryExpectation>(JobErrors.SalaryInvalid);
        }

        if (period is null)
        {
            return Result.Failure<SalaryExpectation>(JobErrors.SalaryInvalid);
        }

        if (string.IsNullOrWhiteSpace(currencyCode))
        {
            return Result.Failure<SalaryExpectation>(JobErrors.CurrencyInvalid);
        }

        var currency = currencyCode.Trim().ToUpperInvariant();

        if (currency.Length != 3 || !currency.All(char.IsAsciiLetterUpper))
        {
            return Result.Failure<SalaryExpectation>(JobErrors.CurrencyInvalid);
        }

        return Result.Success(new SalaryExpectation(
            decimal.Round(minimum.Value, 2), currency, period, isFlexible));
    }

    /// <summary>Whether the candidate said anything at all.</summary>
    public bool IsStated => Minimum is not null;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Minimum;
        yield return CurrencyCode;
        yield return Period;
        yield return IsFlexible;
    }
}
