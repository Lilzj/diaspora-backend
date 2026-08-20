using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Contracts.V1.Requests;
using Diaspora.Modules.Housing.Contracts.V1.Responses;

namespace Diaspora.Modules.Housing.Application.Reservations;

/// <summary>
/// Moves a reservation along: accept, decline, record a signed lease, mark occupied, cancel or
/// extend the hold.
/// <para>
/// Transactional: every exit settles the units on the listing, and a status change that committed
/// without its settlement would leave a home nobody can let.
/// </para>
/// </summary>
[Authorize(Permission = HousingPermissions.ReservationManage)]
[Transactional(Reason = "Settles listing units alongside the reservation status change.")]
[Auditable]
public sealed record UpdateHousingReservationCommand : ICommand<HousingReservationResponse>
{
    public required Guid ReservationId { get; init; }

    public required UpdateReservationRequest Request { get; init; }
}
