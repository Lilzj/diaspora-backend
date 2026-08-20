using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Housing.Domain.Events;

/// <summary>
/// Raised when a member takes a hold on a property.
/// <para>
/// The units are already held on the listing by the time this is raised, in the same unit of work.
/// A reservation that existed for even a moment before its hold is a window in which a second
/// member takes the same room.
/// </para>
/// </summary>
public sealed record HousingReservationCreatedDomainEvent : DomainEvent
{
    public required HousingReservationId ReservationId { get; init; }

    public required HousingListingId ListingId { get; init; }

    public required LandlordProfileId LandlordId { get; init; }

    public required UserId SeekerUserId { get; init; }

    public required int Units { get; init; }

    public DateTimeOffset? MoveInDate { get; init; }

    /// <summary>When the hold lapses if nothing happens. The sweep's key.</summary>
    public DateTimeOffset? HoldExpiresAt { get; init; }

    public required DateTimeOffset ReservedAt { get; init; }
}
