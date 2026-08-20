using Diaspora.Modules.Jobs.Domain.Errors;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Jobs.Domain.ValueObjects;

/// <summary>
/// A span on a candidate's history — a job held, a course studied, a certificate's validity.
/// <para>
/// <b>Dates, not instants.</b> Nobody knows or cares what time on the 3rd of March they started
/// a job, and storing an instant forces a time zone decision that will render the start date a
/// day early for half the platform. A <see cref="DateOnly"/> means the same thing everywhere.
/// </para>
/// <para>
/// <b>An open end is a state, not a null to be interpreted.</b> <see cref="IsCurrent"/> says the
/// candidate is still there; a null <see cref="End"/> without it is a gap in the record, and
/// those are different facts. Keeping them apart is what lets "current role" be a query rather
/// than a guess.
/// </para>
/// </summary>
public sealed class EmploymentPeriod : ValueObject
{
    private EmploymentPeriod(DateOnly start, DateOnly? end, bool isCurrent)
    {
        Start = start;
        End = end;
        IsCurrent = isCurrent;
    }

    private EmploymentPeriod() { }

    public DateOnly Start { get; private set; }

    /// <summary>When it ended. Null while it is still going, or unknown.</summary>
    public DateOnly? End { get; private set; }

    /// <summary>Whether this is ongoing. Distinct from simply having no end date.</summary>
    public bool IsCurrent { get; private set; }

    public static Result<EmploymentPeriod> Create(
        DateOnly start, DateOnly? end, bool isCurrent = false)
    {
        if (end is { } finish && finish < start)
        {
            return Result.Failure<EmploymentPeriod>(JobErrors.PeriodInverted);
        }

        // "Still there" and "ended on this date" cannot both be true. The end date is the
        // more specific claim, so it wins and the flag is dropped.
        var current = isCurrent && end is null;

        return Result.Success(new EmploymentPeriod(start, end, current));
    }

    /// <summary>Whole months spanned, measured to <paramref name="asOf"/> when still open.</summary>
    public int MonthsElapsed(DateOnly asOf)
    {
        var finish = End ?? (IsCurrent ? asOf : Start);

        if (finish < Start)
        {
            return 0;
        }

        var months = ((finish.Year - Start.Year) * 12) + finish.Month - Start.Month;

        return Math.Max(0, finish.Day < Start.Day ? months - 1 : months);
    }

    /// <summary>Whether the span covers a given date.</summary>
    public bool Covers(DateOnly date) =>
        date >= Start && (End is null ? IsCurrent : date <= End.Value);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Start;
        yield return End;
        yield return IsCurrent;
    }
}
