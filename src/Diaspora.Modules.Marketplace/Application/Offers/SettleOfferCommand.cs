using Diaspora.Application.Abstractions;
using Diaspora.Modules.Marketplace.Contracts.V1.Requests;
using Diaspora.Modules.Marketplace.Contracts.V1.Responses;

namespace Diaspora.Modules.Marketplace.Application.Offers;

/// <summary>
/// Ends a negotiation: accept, decline or withdraw.
/// <para>
/// <c>[Transactional]</c>, because accepting writes three aggregates: the negotiation settles, the
/// listing's stock is held, and a reservation is created to hold it. A negotiation that accepted
/// without its reservation would be a buyer who won a deal the platform then forgot about.
/// </para>
/// </summary>
[RequirePermission(MarketplacePermissions.OfferRespond)]
[Auditable]
[Transactional(Reason = "Accepting settles the offer, holds the stock and opens the reservation.")]
public sealed record SettleOfferCommand : ICommand<OfferResponse>
{
    public required Guid OfferId { get; init; }

    public required SettleOfferRequest Request { get; init; }
}
