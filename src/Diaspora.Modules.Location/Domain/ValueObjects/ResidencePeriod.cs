using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Location.Domain.ValueObjects;

/// <summary>
/// When a member lived somewhere: an arrival date, and a departure date they may not have
/// reached yet.
/// <para>
/// An open period — <c>DepartedOn</c> null — is what the frontend renders as "Present" in the
/// experience timeline. It is a first-class state rather than a far-future sentinel date,
/// because "still there" and "leaving in 2099" are different facts and only one of them is
/// true.
/// </para>
/// <para>
/// <see cref="ExperienceLevel"/> is derived from this rather than declared, so a member's
/// standing in a country grows with the calendar and cannot be self-inflated.
/// </para>
/// </summary>
public sealed class ResidencePeriod : ValueObject
{
    public DateOnly ArrivedOn { get; }

    /// <summary>Null while the member still lives there.</summary>
    public DateOnly? DepartedOn { get; }

    private ResidencePeriod(DateOnly arrivedOn, DateOnly? departedOn)
    {
        ArrivedOn = arrivedOn;
        DepartedOn = departedOn;
    }

    public static Result<ResidencePeriod> TryCreate(
        DateOnly arrivedOn, DateOnly? departedOn, DateOnly today)
    {
        if (arrivedOn > today)
        {
            return Result.Failure<ResidencePeriod>(Error.Validation(
                LocationErrorCodes.ExperiencePeriodInvalid,
                "An arrival date cannot be in the future."));
        }

        // Nobody predates the platform's usefulness by a century, and a mistyped year is far
        // more likely than a 150-year residence. Rejecting it here keeps the derived
        // experience level from being nonsense.
        if (arrivedOn.Year < today.Year - 120)
        {
            return Result.Failure<ResidencePeriod>(Error.Validation(
                LocationErrorCodes.ExperiencePeriodInvalid,
                "That arrival date is not plausible."));
        }

        if (departedOn is null)
        {
            return Result.Success(new ResidencePeriod(arrivedOn, null));
        }

        if (departedOn < arrivedOn)
        {
            return Result.Failure<ResidencePeriod>(Error.Validation(
                LocationErrorCodes.ExperiencePeriodInvalid,
                "A departure date cannot precede the arrival date."));
        }

        return departedOn > today
            ? Result.Failure<ResidencePeriod>(Error.Validation(
                LocationErrorCodes.ExperiencePeriodInvalid,
                "A departure date cannot be in the future."))
            : Result.Success(new ResidencePeriod(arrivedOn, departedOn));
    }

    /// <summary>True while the member has not left.</summary>
    public bool IsOpen => DepartedOn is null;

    /// <summary>
    /// Whether two periods in the same country overlap.
    /// <para>
    /// Enforced because two overlapping periods in one country are not a richer history, they
    /// are a data-entry mistake — and left in, they double every years-of-residence figure
    /// derived from them.
    /// </para>
    /// </summary>
    public bool Overlaps(ResidencePeriod other)
    {
        ArgumentNullException.ThrowIfNull(other);

        var thisEnd = DepartedOn ?? DateOnly.MaxValue;
        var otherEnd = other.DepartedOn ?? DateOnly.MaxValue;

        return ArrivedOn <= otherEnd && other.ArrivedOn <= thisEnd;
    }

    /// <summary>Whole years spent in residence as at <paramref name="today"/>.</summary>
    public int YearsAsAt(DateOnly today)
    {
        var end = DepartedOn ?? today;
        var years = end.Year - ArrivedOn.Year;

        if (end < ArrivedOn.AddYears(years))
        {
            years--;
        }

        return Math.Max(0, years);
    }

    /// <summary>Whole months spent in residence as at <paramref name="today"/>.</summary>
    public int MonthsAsAt(DateOnly today)
    {
        var end = DepartedOn ?? today;
        var months = ((end.Year - ArrivedOn.Year) * 12) + end.Month - ArrivedOn.Month;

        if (end.Day < ArrivedOn.Day)
        {
            months--;
        }

        return Math.Max(0, months);
    }

    /// <summary>
    /// The experience level this period earns.
    /// <para>
    /// The bands are deliberately coarse. A finer scale would imply that the platform can
    /// tell the difference between three and four years of local knowledge, which it cannot.
    /// </para>
    /// </summary>
    public ExperienceLevel ExperienceAsAt(DateOnly today) => MonthsAsAt(today) switch
    {
        < 6 => ExperienceLevel.Newcomer,
        < 24 => ExperienceLevel.Settling,
        < 60 => ExperienceLevel.Established,
        < 120 => ExperienceLevel.Seasoned,
        _ => ExperienceLevel.Native
    };

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return ArrivedOn;
        yield return DepartedOn;
    }

    public override string ToString() =>
        string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{ArrivedOn:yyyy-MM-dd} – {(DepartedOn is null ? "present" : DepartedOn.Value.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture))}");
}
