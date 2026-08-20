using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Contracts.V1.Responses;

namespace Diaspora.Modules.Housing.Application.Reservations;

/// <summary>One reservation, with its history. Private to the two parties.</summary>
[Authorize(Permission = HousingPermissions.ReservationManage)]
public sealed record GetHousingReservationQuery : IQuery<HousingReservationResponse>
{
    public required Guid ReservationId { get; init; }
}
