using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Housing.V1.Events;

/// <summary>
/// A member took a hold on a property.
/// <para>
/// <see cref="RemainingUnits"/> is what a search index acts on: a hostel with beds left stays
/// discoverable, and one with none should stop being offered. A consumer that de-indexed on every
/// reservation would hide a twelve-bed dormitory the moment one person booked into it.
/// </para>
/// </summary>
public sealed record HousingReservedIntegrationEvent : IntegrationEvent
{
    public required Guid ReservationId { get; init; }

    public required Guid ListingId { get; init; }

    public required Guid LandlordId { get; init; }

    public required Guid SeekerUserId { get; init; }

    public required int Units { get; init; }

    /// <summary>What is left after this hold. See the class summary.</summary>
    public required int RemainingUnits { get; init; }

    public DateTimeOffset? MoveInDate { get; init; }

    /// <summary>When the hold lapses if nothing happens.</summary>
    public DateTimeOffset? HoldExpiresAt { get; init; }

    public required DateTimeOffset ReservedAt { get; init; }
}
