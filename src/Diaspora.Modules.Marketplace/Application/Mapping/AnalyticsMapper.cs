using Diaspora.Modules.Marketplace.Contracts.V1.Responses;
using Diaspora.Modules.Marketplace.Domain.Analytics;

namespace Diaspora.Modules.Marketplace.Application.Mapping;

/// <summary>
/// Turns analytics rollups into wire responses.
/// <para>
/// <b>Every rate stays nullable all the way to the wire.</b> A listing nobody has viewed has an
/// undefined conversion rate, not a zero-percent one, and flattening the distinction here would
/// tell a seller their listing is failing when in fact it has not been seen.
/// </para>
/// </summary>
internal static class AnalyticsMapper
{
    public static ListingAnalyticsResponse ToResponse(
        ListingAnalytics analytics, string title)
    {
        ArgumentNullException.ThrowIfNull(analytics);

        return new ListingAnalyticsResponse
        {
            ListingId = analytics.ListingId.Value,
            Title = title,
            TotalViews = analytics.TotalViews,
            UniqueViews = analytics.TotalUniqueViews,
            Saves = analytics.TotalSaves,
            Shares = analytics.TotalShares,
            Offers = analytics.TotalOffers,
            Reservations = analytics.TotalReservations,
            Sales = analytics.TotalSales,
            Impressions = analytics.TotalImpressions,
            OfferRate = analytics.OfferRate,
            ConversionRate = analytics.ConversionRate,
            ReservationRate = analytics.ReservationRate,
            ClickThroughRate = analytics.ClickThroughRate,
            FirstPublishedAt = analytics.FirstPublishedAt,
            FirstOfferAt = analytics.FirstOfferAt,
            FirstSaleAt = analytics.FirstSaleAt,
            TimeToSaleDays = analytics.TimeToFirstSale?.TotalDays,
            Daily = [.. analytics.Daily.Select(ToDaily)]
        };
    }

    public static ListingDailyMetricResponse ToDaily(ListingDailyMetric metric)
    {
        ArgumentNullException.ThrowIfNull(metric);

        return new ListingDailyMetricResponse
        {
            Day = metric.Day,
            Views = metric.Views,
            UniqueViews = metric.UniqueViews,
            Saves = metric.Saves,
            Offers = metric.Offers,
            Reservations = metric.Reservations,
            Sales = metric.Sales,
            Impressions = metric.Impressions
        };
    }

    public static MarketplaceTrendResponse ToTrend(MarketplaceTrendRow row, string? label = null)
    {
        ArgumentNullException.ThrowIfNull(row);

        return new MarketplaceTrendResponse
        {
            Key = row.Key,
            Label = label,
            Listings = row.Listings,
            Views = row.Views,
            Offers = row.Offers,
            Reservations = row.Reservations,
            Sales = row.Sales,
            ConversionRate = row.ConversionRate
        };
    }
}
