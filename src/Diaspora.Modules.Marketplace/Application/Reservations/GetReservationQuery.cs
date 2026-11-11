using Diaspora.Application.Abstractions;
using Diaspora.Modules.Marketplace.Contracts.V1.Responses;

namespace Diaspora.Modules.Marketplace.Application.Reservations;

/// <summary>One reservation, with everything that has happened to it.</summary>
[RequirePermission(MarketplacePermissions.ReservationManage)]
public sealed record GetReservationQuery : IQuery<ReservationResponse>
{
    public required Guid ReservationId { get; init; }
}
