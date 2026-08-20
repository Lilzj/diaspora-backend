namespace Diaspora.Modules.Marketplace.Contracts.V1.Responses;

/// <summary>
/// How a listing has performed.
/// <para>
/// <b>Every rate is nullable, and none of them is zero when nothing happened.</b> A listing nobody
/// has viewed has an undefined conversion rate, not a zero-percent one, and rendering it as zero
/// would tell a seller their listing is failing when in fact it has not been seen. That distinction
/// is the difference between a dashboard that helps and one that discourages.
/// </para>
/// </summary>
public sealed record ListingAnalyticsResponse
{
    public required Guid ListingId { get; init; }

    public required string Title { get; init; }

    public required long TotalViews { get; init; }

    public required long UniqueViews { get; init; }

    public required long Saves { get; init; }

    public required long Shares { get; init; }

    public required long Offers { get; init; }

    public required long Reservations { get; init; }

    public required long Sales { get; init; }

    /// <summary>Times the listing appeared in a result set.</summary>
    public required long Impressions { get; init; }

    /// <summary>Offers per unique viewer. Null when nobody has looked.</summary>
    public double? OfferRate { get; init; }

    /// <summary>Sales per unique viewer.</summary>
    public double? ConversionRate { get; init; }

    /// <summary>Reservations per unique viewer.</summary>
    public double? ReservationRate { get; init; }

    /// <summary>Views per result-set appearance. How well the card earns its slot.</summary>
    public double? ClickThroughRate { get; init; }

    public DateTimeOffset? FirstPublishedAt { get; init; }

    public DateTimeOffset? FirstOfferAt { get; init; }

    public DateTimeOffset? FirstSaleAt { get; init; }

    /// <summary>Days from publication to the first sale. The brief's time to sale.</summary>
    public double? TimeToSaleDays { get; init; }

    /// <summary>The daily series over the window asked for, oldest first.</summary>
    public required IReadOnlyList<ListingDailyMetricResponse> Daily { get; init; }
}
