using System.Globalization;

namespace Diaspora.Modules.Analytics.Application.Services;

/// <summary>How a cohort table is cut.</summary>
public enum AnalyticsCohortGranularity
{
    Day = 1,

    Week = 2,

    Month = 3
}

/// <summary>One cohort, counted.</summary>
/// <param name="Start">The first day of the period the cohort was first seen in.</param>
/// <param name="Size">How many people were first seen in it.</param>
/// <param name="Values">Retained share at each offset; null where the offset has not begun by the end day.</param>
/// <param name="IsIncomplete">Whether the last offset has not finished by the end day.</param>
public sealed record AnalyticsCohortRow(DateOnly Start, long Size, IReadOnlyList<double?> Values, bool IsIncomplete);

/// <summary>A cohort asked for and not counted, because its activity is no longer kept.</summary>
public sealed record AnalyticsCohortGap(DateOnly Start);

/// <summary>The counted table.</summary>
public sealed record AnalyticsCohortResult(
    IReadOnlyList<AnalyticsCohortRow> Cohorts, IReadOnlyList<AnalyticsCohortGap> Unavailable);

/// <summary>
/// Cohort retention, counted from (person, day) pairs — FRONTEND-PHASE15-GAP-004.
/// <para>
/// <b>A cohort is the people first recorded in one period</b>, and a person is retained at offset
/// <c>N</c> when anything they did was recorded in the <c>N</c>th period after it. Offset zero is the
/// cohort's own period, so it is one by definition.
/// </para>
/// <para>
/// <b>"First recorded" can only look as far back as the store does.</b> Rows before
/// <c>horizon</c> are gone, so a cohort whose period starts before it cannot be told apart from the
/// people who were already active — it is returned as unavailable, never estimated. A cohort after
/// it still counts somebody active before the horizon and again since as new; the response says so.
/// </para>
/// <para>
/// <b>Nothing is counted after <c>endDay</c></b>, and an offset whose period has not begun by then
/// is null rather than zero: a week that has not happened is not a week everybody left in.
/// </para>
/// </summary>
public static class AnalyticsCohortTable
{
    /// <summary>The upper bound on cohorts in one table.</summary>
    public const int MaximumCohorts = 24;

    /// <summary>Offsets per granularity — the same widths the client's grid is drawn for.</summary>
    public static int OffsetsFor(AnalyticsCohortGranularity granularity) => granularity switch
    {
        AnalyticsCohortGranularity.Day => 8,
        AnalyticsCohortGranularity.Week => 7,
        _ => 6
    };

    public static IReadOnlyList<string> OffsetLabels(AnalyticsCohortGranularity granularity)
    {
        var word = granularity switch
        {
            AnalyticsCohortGranularity.Day => "Day",
            AnalyticsCohortGranularity.Week => "Week",
            _ => "Month"
        };

        return Enumerable.Range(0, OffsetsFor(granularity))
            .Select(index => string.Create(CultureInfo.InvariantCulture, $"{word} {index}"))
            .ToList();
    }

    /// <summary>The first day of the period a day falls in. Weeks begin on Monday.</summary>
    public static DateOnly PeriodStart(DateOnly day, AnalyticsCohortGranularity granularity) => granularity switch
    {
        AnalyticsCohortGranularity.Day => day,
        AnalyticsCohortGranularity.Week => day.AddDays(-(((int)day.DayOfWeek + 6) % 7)),
        _ => new DateOnly(day.Year, day.Month, 1)
    };

    /// <summary>The first day of the period <paramref name="count"/> periods after the one starting <paramref name="start"/>.</summary>
    public static DateOnly Advance(DateOnly start, AnalyticsCohortGranularity granularity, int count) => granularity switch
    {
        AnalyticsCohortGranularity.Day => start.AddDays(count),
        AnalyticsCohortGranularity.Week => start.AddDays(7 * count),
        _ => start.AddMonths(count)
    };

    /// <summary>
    /// Counts the table.
    /// </summary>
    /// <param name="activity">Distinct (person, day) pairs. Pairs before <paramref name="horizon"/> or after <paramref name="endDay"/> are ignored.</param>
    /// <param name="granularity">How periods are cut.</param>
    /// <param name="cohortCount">How many cohorts, ending with the one containing <paramref name="endDay"/>.</param>
    /// <param name="endDay">The last day counted.</param>
    /// <param name="horizon">The first day the store still holds.</param>
    public static AnalyticsCohortResult Build(
        IEnumerable<(string Actor, DateOnly Day)> activity,
        AnalyticsCohortGranularity granularity,
        int cohortCount,
        DateOnly endDay,
        DateOnly horizon)
    {
        ArgumentNullException.ThrowIfNull(activity);

        var offsets = OffsetsFor(granularity);
        var count = Math.Clamp(cohortCount, 1, MaximumCohorts);

        // Each person's set of period starts, and the first one: their cohort.
        var periodsByActor = new Dictionary<string, HashSet<DateOnly>>(StringComparer.Ordinal);

        foreach (var (actor, day) in activity)
        {
            if (day < horizon || day > endDay || string.IsNullOrEmpty(actor))
            {
                continue;
            }

            if (!periodsByActor.TryGetValue(actor, out var periods))
            {
                periods = [];
                periodsByActor[actor] = periods;
            }

            periods.Add(PeriodStart(day, granularity));
        }

        var membersByCohort = periodsByActor
            .GroupBy(entry => entry.Value.Min())
            .ToDictionary(group => group.Key, group => group.Select(entry => entry.Value).ToList());

        var last = PeriodStart(endDay, granularity);
        var cohorts = new List<AnalyticsCohortRow>();
        var unavailable = new List<AnalyticsCohortGap>();

        for (var position = count - 1; position >= 0; position--)
        {
            var start = Advance(last, granularity, -position);

            if (start < horizon)
            {
                unavailable.Add(new AnalyticsCohortGap(start));
                continue;
            }

            membersByCohort.TryGetValue(start, out var members);
            var size = members?.Count ?? 0;
            var values = new double?[offsets];

            for (var offset = 0; offset < offsets; offset++)
            {
                var period = Advance(start, granularity, offset);

                if (period > endDay || size == 0)
                {
                    values[offset] = null;
                    continue;
                }

                values[offset] = members!.Count(periods => periods.Contains(period)) / (double)size;
            }

            // The last offset's period ends the day before the one after it begins.
            var lastOffsetEnds = Advance(start, granularity, offsets).AddDays(-1);

            cohorts.Add(new AnalyticsCohortRow(start, size, values, lastOffsetEnds > endDay));
        }

        return new AnalyticsCohortResult(cohorts, unavailable);
    }
}
