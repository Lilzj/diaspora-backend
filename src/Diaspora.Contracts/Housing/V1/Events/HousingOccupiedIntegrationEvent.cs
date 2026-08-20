using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Housing.V1.Events;

/// <summary>
/// Somebody moved in.
/// <para>
/// <b>The platform success event, and the one worth building analytics on.</b>
/// <see cref="TimeToLetDays"/> is the figure a neighbourhood market statistic is made of, and it is
/// carried here because it is computable exactly once - at the moment of occupation, from a
/// publication date the listing still holds.
/// </para>
/// <para>
/// The rent is carried with its currency and its period and is never converted or normalised. A
/// consumer computing an average must group by both first.
/// </para>
/// </summary>
public sealed record HousingOccupiedIntegrationEvent : IntegrationEvent
{
    public required Guid ListingId { get; init; }

    public required Guid LandlordId { get; init; }

    public Guid? OccupantUserId { get; init; }

    public Guid? ReservationId { get; init; }

    public required int Units { get; init; }

    /// <summary>Whether every unit is now taken.</summary>
    public required bool IsFullyOccupied { get; init; }

    public decimal? LetAtAmount { get; init; }

    public string? CurrencyCode { get; init; }

    public string? RentPeriod { get; init; }

    public required string AccommodationTypeKey { get; init; }

    public required string CountryCode { get; init; }

    /// <summary>The Location area, so a market statistic can be grouped by neighbourhood.</summary>
    public Guid? NeighborhoodAreaId { get; init; }

    /// <summary>Publication to occupation. Null when the listing was never published.</summary>
    public double? TimeToLetDays { get; init; }

    public required DateTimeOffset OccupiedAt { get; init; }
}
