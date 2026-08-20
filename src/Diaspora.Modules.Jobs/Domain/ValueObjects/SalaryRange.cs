using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Errors;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Jobs.Domain.ValueObjects;

/// <summary>
/// What a posting pays.
/// <para>
/// <b>Undisclosed is a first-class state, not a null.</b> Most postings in most markets do not
/// publish a figure, and modelling that as "no salary object" makes every read path do a null
/// dance and every filter guess. <see cref="Undisclosed"/> is an instance that says so out
/// loud, which also lets the client render "salary not disclosed" instead of nothing — a
/// meaningful difference to a candidate deciding whether to spend an hour on an application.
/// </para>
/// <para>
/// <b>Currency and period are required whenever a figure is present, and that is the point.</b>
/// "50,000" is unreadable across borders, and this platform's entire audience is reading across
/// borders. The pair is what lets a member in Manila and a member in Warsaw understand the same
/// posting.
/// </para>
/// <para>
/// <b>No conversion happens here, ever.</b> A stored figure is the employer's figure in the
/// employer's currency. Converting for display or for a range filter needs a rate, a rate has a
/// date, and a salary silently restated at yesterday's rate is a number nobody can act on. The
/// normalised comparison column the sort uses is computed in the search projection, where it is
/// visibly an approximation.
/// </para>
/// </summary>
public sealed class SalaryRange : ValueObject
{
    private SalaryRange(
        bool isDisclosed,
        decimal? minimum,
        decimal? maximum,
        string? currencyCode,
        SalaryPeriod? period,
        bool isNegotiable)
    {
        IsDisclosed = isDisclosed;
        Minimum = minimum;
        Maximum = maximum;
        CurrencyCode = currencyCode;
        Period = period;
        IsNegotiable = isNegotiable;
    }

    private SalaryRange() { }

    /// <summary>Whether the employer published a figure at all.</summary>
    public bool IsDisclosed { get; private set; }

    /// <summary>The bottom of the band. Null on an undisclosed salary.</summary>
    public decimal? Minimum { get; private set; }

    /// <summary>The top of the band. Null when the employer quoted a single figure.</summary>
    public decimal? Maximum { get; private set; }

    /// <summary>Upper-case ISO 4217. Null on an undisclosed salary.</summary>
    public string? CurrencyCode { get; private set; }

    /// <summary>The unit the figures are quoted in. Null on an undisclosed salary.</summary>
    public SalaryPeriod? Period { get; private set; }

    /// <summary>Whether the employer will discuss the figure.</summary>
    public bool IsNegotiable { get; private set; }

    // A fresh instance per call, not a cached one. These map as owned types, and an owned
    // instance shared between two aggregates in one DbContext is tracked against whichever
    // owner EF saw first — the second silently writes nulls into NOT NULL columns. It only
    // bites when something adds two postings in one unit of work, so it would have surfaced
    // first in a bulk import rather than anywhere a test was looking.

    /// <summary>No figure published. What most postings carry, and a real answer.</summary>
    public static SalaryRange Undisclosed =>
        new(false, null, null, null, null, isNegotiable: false);

    /// <summary>Unpaid, and explicitly so. Volunteer postings.</summary>
    public static SalaryRange Unpaid =>
        new(true, 0m, 0m, null, null, isNegotiable: false);

    /// <summary>
    /// A published salary. A single figure is expressed by leaving
    /// <paramref name="maximum"/> null rather than by repeating the minimum, so
    /// "£40,000" and "£40,000–£40,000" do not both exist as ways of saying the same thing.
    /// </summary>
    public static Result<SalaryRange> Create(
        decimal? minimum,
        decimal? maximum,
        string? currencyCode,
        SalaryPeriod? period,
        bool isNegotiable = false)
    {
        if (minimum is null && maximum is null)
        {
            // Nothing to publish. Negotiability alone is not a salary, but it is worth keeping.
            return Result.Success(isNegotiable
                ? new SalaryRange(false, null, null, null, null, isNegotiable: true)
                : Undisclosed);
        }

        if (minimum is < 0 || maximum is < 0)
        {
            return Result.Failure<SalaryRange>(JobErrors.SalaryInvalid);
        }

        if (minimum is { } low && maximum is { } high && low > high)
        {
            return Result.Failure<SalaryRange>(JobErrors.SalaryRangeInverted);
        }

        if (period is null)
        {
            return Result.Failure<SalaryRange>(JobErrors.SalaryInvalid);
        }

        var currency = NormalizeCurrency(currencyCode);

        if (currency is null)
        {
            return Result.Failure<SalaryRange>(JobErrors.CurrencyInvalid);
        }

        return Result.Success(new SalaryRange(
            isDisclosed: true,
            minimum is { } roundedLow ? decimal.Round(roundedLow, 2) : null,
            maximum is { } roundedHigh ? decimal.Round(roundedHigh, 2) : null,
            currency,
            period,
            isNegotiable));
    }

    /// <summary>
    /// The figure a sort orders by: the top of the band where there is one, the bottom
    /// otherwise. Null when nothing was published, which sorts last by convention.
    /// </summary>
    public decimal? ComparableAmount => IsDisclosed ? Maximum ?? Minimum : null;

    /// <summary>Whether the posting says the work is unpaid.</summary>
    public bool IsUnpaid => IsDisclosed && Minimum == 0m && Maximum is null or 0m;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return IsDisclosed;
        yield return Minimum;
        yield return Maximum;
        yield return CurrencyCode;
        yield return Period;
        yield return IsNegotiable;
    }

    private static string? NormalizeCurrency(string? currencyCode)
    {
        if (string.IsNullOrWhiteSpace(currencyCode))
        {
            return null;
        }

        var candidate = currencyCode.Trim().ToUpperInvariant();

        return candidate.Length == 3 && candidate.All(char.IsAsciiLetterUpper) ? candidate : null;
    }
}
