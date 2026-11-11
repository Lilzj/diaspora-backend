using Diaspora.Application.Abstractions;
using Diaspora.Modules.Marketplace.Contracts.V1.Requests;
using Diaspora.Modules.Marketplace.Contracts.V1.Responses;

namespace Diaspora.Modules.Marketplace.Application.Offers;

/// <summary>Opens a negotiation on a listing.</summary>
[RequirePermission(MarketplacePermissions.OfferMake)]
[Auditable]
public sealed record MakeOfferCommand : ICommand<OfferResponse>
{
    public required Guid ListingId { get; init; }

    public required MakeOfferRequest Request { get; init; }
}
