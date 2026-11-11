using Diaspora.Application.Abstractions;
using Diaspora.Modules.Marketplace.Contracts.V1.Requests;
using Diaspora.Modules.Marketplace.Contracts.V1.Responses;

namespace Diaspora.Modules.Marketplace.Application.Reservations;

/// <summary>
/// Moves a reservation along: acknowledge, complete or cancel.
/// <para>
/// The permission is the low-floor one and is granted under restriction, because cancelling must
/// never be blocked — a reservation nobody could cancel would hold somebody else's stock hostage
/// until its window lapsed.
/// </para>
/// </summary>
[RequirePermission(MarketplacePermissions.ReservationManage)]
[Auditable]
public sealed record UpdateReservationCommand : ICommand<ReservationResponse>
{
    public required Guid ReservationId { get; init; }

    public required UpdateReservationRequest Request { get; init; }
}
