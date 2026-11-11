using Diaspora.Application.Abstractions;
using Diaspora.Modules.Marketplace.Contracts.V1.Requests;
using Diaspora.Modules.Marketplace.Contracts.V1.Responses;

namespace Diaspora.Modules.Marketplace.Application.Reservations;

/// <summary>
/// Holds stock against a buyer.
/// <para>
/// <c>[Transactional]</c>, because the hold on the listing and the reservation that owns it must
/// commit together. A reservation that existed for even a moment without its hold is a window in
/// which a second buyer takes the same item.
/// </para>
/// </summary>
[RequirePermission(MarketplacePermissions.ReservationCreate)]
[Auditable]
[Transactional(Reason = "Holds the listing's stock and opens the reservation together.")]
public sealed record CreateReservationCommand : ICommand<ReservationResponse>
{
    public required Guid ListingId { get; init; }

    public required CreateReservationRequest Request { get; init; }
}
