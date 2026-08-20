using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Housing.Domain.Events;

/// <summary>
/// Raised when somebody moves in.
/// <para>
/// <b>The module's success event.</b> Everything else here measures activity; this measures a
/// member who has somewhere to live. <see cref="TimeToLet"/> is the figure the analytics guide is
/// really about - how long a property in this neighbourhood, at this rent, takes to let - and it
/// is carried on the event because it is computable exactly once, here, from the publication date
/// the listing still holds.
/// </para>
/// </summary>
public sealed record HousingListingOccupiedDomainEvent : DomainEvent
{
    public required HousingListingId ListingId { get; init; }

    public required LandlordProfileId LandlordId { get; init; }

    /// <summary>The member who moved in. Null when the landlord recorded it without a reservation.</summary>
    public UserId? OccupantUserId { get; init; }

    public HousingReservationId? ReservationId { get; init; }

    public required int Units { get; init; }

    /// <summary>Whether every unit is now taken.</summary>
    public required bool IsFullyOccupied { get; init; }

    /// <summary>Publication to occupation. Null when the listing was never published.</summary>
    public TimeSpan? TimeToLet { get; init; }

    public required DateTimeOffset OccupiedAt { get; init; }
}
