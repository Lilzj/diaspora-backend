using Diaspora.Modules.Housing.Contracts.V1.Responses;
using Diaspora.Modules.Housing.Domain.Analytics;
using Diaspora.Modules.Housing.Domain.Enums;

namespace Diaspora.Modules.Housing.Application.Mapping;

/// <summary>
/// Turns the measured record into wire responses, and market rows into statistics.
/// <para>
/// <b>The market statistic groups by currency and rent period before it computes anything.</b>
/// Nothing in this module converts either, so an average across them would be a number that is
/// wrong in a way nobody reading it could see. The grouping is done here, once, rather than in each
/// caller that wants a headline figure.
/// </para>
/// <para>
/// <b>The median rather than the mean.</b> One luxury let in a neighbourhood of ordinary flats
/// moves a mean and does not move a median, and the figure members act on should describe the
/// market rather than its outliers.
/// </para>
/// </summary>
public static class HousingAnalyticsMapper
{
    /// <summary>
    /// Lets below which no statistic is published. Three: enough that a figure describes a market
    /// rather than an anecdote, low enough that a quiet neighbourhood still gets one.
    /// </summary>
    private const int MinimumSample = 3;

    public static HousingAnalyticsResponse ToResponse(HousingAnalytics analytics)
    {
        ArgumentNullException.ThrowIfNull(analytics);

        return new HousingAnalyticsResponse
        {
            ListingId = analytics.ListingId.Value,
            TotalViews = analytics.TotalViews,
            UniqueViews = analytics.UniqueViews,
            ViewingRequests = analytics.ViewingRequests,
            ViewingsCompleted = analytics.ViewingsCompleted,
            Reservations = analytics.Reservations,
            ReservationsAbandoned = analytics.ReservationsAbandoned,
            Occupations = analytics.Occupations,
            Saves = analytics.Saves,
            ViewingConversionRate = analytics.ViewingConversionRate,
            ReservationCompletionRate = analytics.ReservationCompletionRate,
            DaysToLet = analytics.TimeToLet?.TotalDays,
            LetAtAmount = analytics.LetAtAmount,
            LetAtCurrency = analytics.LetAtCurrency,
            LetAtPeriod = analytics.LetAtPeriod == RentPeriod.None
                ? null
                : analytics.LetAtPeriod.ToString(),
            Daily = [.. analytics.Daily.Select(metric => new HousingDailyMetricResponse
            {
                Day = metric.Day,
                Views = metric.Views,
                UniqueViews = metric.UniqueViews,
                Saves = metric.Saves,
                ViewingRequests = metric.ViewingRequests,
                Reservations = metric.Reservations
            })]
        };
    }

    /// <summary>
    /// Reduces market rows to one statistic per currency and period pair.
    /// <para>
    /// Rows with no disclosed rent are excluded from the money figures and still counted toward the
    /// time-to-let median: a property let at an undisclosed rent still took a knowable number of
    /// days to let.
    /// </para>
    /// </summary>
    public static IReadOnlyList<HousingMarketStatisticResponse> ToMarketStatistics(
        IReadOnlyList<HousingMarketRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        return
        [
            .. rows
                .Where(row => row.HasLet
                    && row.Amount is > 0m
                    && row.CurrencyCode is { Length: > 0 }
                    && row.Period != RentPeriod.None)
                .GroupBy(row => new { row.CurrencyCode, row.Period })
                .Where(group => group.Count() >= MinimumSample)
                .Select(group =>
                {
                    var amounts = group
                        .Select(row => row.Amount!.Value)
                        .OrderBy(amount => amount)
                        .ToList();

                    return new HousingMarketStatisticResponse
                    {
                        CurrencyCode = group.Key.CurrencyCode!,
                        Period = group.Key.Period.ToString(),
                        AccommodationTypeKey = null,
                        SampleSize = amounts.Count,
                        MedianRent = Median(amounts),
                        LowestRent = amounts[0],
                        HighestRent = amounts[^1]
                    };
                })
                .OrderByDescending(statistic => statistic.SampleSize)
        ];
    }

    /// <summary>
    /// The median number of days from publication to occupation, or null below a useful sample.
    /// </summary>
    public static double? MedianDaysToLet(IReadOnlyList<HousingMarketRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var durations = rows
            .Where(row => row.TimeToLet is not null)
            .Select(row => row.TimeToLet!.Value.TotalDays)
            .OrderBy(days => days)
            .ToList();

        return durations.Count < MinimumSample ? null : Median(durations);
    }

    private static decimal Median(List<decimal> sorted) =>
        sorted.Count % 2 == 1
            ? sorted[sorted.Count / 2]
            : (sorted[(sorted.Count / 2) - 1] + sorted[sorted.Count / 2]) / 2m;

    private static double Median(List<double> sorted) =>
        sorted.Count % 2 == 1
            ? sorted[sorted.Count / 2]
            : (sorted[(sorted.Count / 2) - 1] + sorted[sorted.Count / 2]) / 2d;
}
