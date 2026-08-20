using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Contracts.V1.Requests;
using Diaspora.Modules.Housing.Contracts.V1.Responses;

namespace Diaspora.Modules.Housing.Application.Reservations;

/// <summary>
/// Takes a hold on a property.
/// <para>
/// Transactional: the units come off the listing and the reservation is written in one unit of
/// work. A reservation that existed for even a moment before its hold is a window in which a second
/// member takes the same room.
/// </para>
/// </summary>
[Authorize(Permission = HousingPermissions.ReservationCreate)]
[Transactional(Reason = "Holds units on the listing and writes the reservation atomically.")]
[Auditable]
public sealed record CreateHousingReservationCommand : ICommand<HousingReservationResponse>
{
    public required Guid ListingId { get; init; }

    public required CreateHousingReservationRequest Request { get; init; }
}
