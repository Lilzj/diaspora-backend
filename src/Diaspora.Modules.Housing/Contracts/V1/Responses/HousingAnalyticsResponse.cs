namespace Diaspora.Modules.Housing.Contracts.V1.Responses;

/// <summary>
/// A property measured record. Readable by its landlord and by staff.
/// <para>
/// The pairing that matters is viewing requests against views: a property with three thousand
/// views and no viewing requests is priced wrong, photographed badly, or not real.
/// </para>
/// </summary>
public sealed record HousingAnalyticsResponse
{
    public required Guid ListingId { get; init; }

    public required long TotalViews { get; init; }

    public required long UniqueViews { get; init; }

    public required long ViewingRequests { get; init; }

    public required long ViewingsCompleted { get; init; }

    public required long Reservations { get; init; }

    /// <summary>Reservations that ended without anybody moving in.</summary>
    public required long ReservationsAbandoned { get; init; }

    public required long Occupations { get; init; }

    public required long Saves { get; init; }

    /// <summary>The share of viewers who asked to see it. Null below a meaningful sample.</summary>
    public double? ViewingConversionRate { get; init; }

    /// <summary>
    /// The share of reservations that ended with somebody moving in. What tells a landlord whether
    /// their asking rent survives contact with a member budget.
    /// </summary>
    public double? ReservationCompletionRate { get; init; }

    /// <summary>Days from publication to somebody moving in.</summary>
    public double? DaysToLet { get; init; }

    public decimal? LetAtAmount { get; init; }

    public string? LetAtCurrency { get; init; }

    public string? LetAtPeriod { get; init; }

    /// <summary>Up to ninety days. Older activity survives in the totals, not in this list.</summary>
    public IReadOnlyList<HousingDailyMetricResponse> Daily { get; init; } = [];
}
