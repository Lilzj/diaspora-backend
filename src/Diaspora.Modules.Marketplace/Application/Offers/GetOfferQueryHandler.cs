using Diaspora.Application.Abstractions;
using Diaspora.Modules.Marketplace.Application.Mapping;
using Diaspora.Modules.Marketplace.Application.Services;
using Diaspora.Modules.Marketplace.Contracts.V1.Responses;
using Diaspora.Modules.Marketplace.Domain.Errors;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.Modules.Marketplace.Domain.Offers;
using Diaspora.Modules.Marketplace.Domain.Sellers;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Marketplace.Application.Offers;

/// <summary>
/// Reads one negotiation with its full history.
/// <para>
/// A caller who is not a party gets not-found rather than forbidden: a 403 would confirm that a
/// negotiation exists between two people they are not part of.
/// </para>
/// </summary>
public sealed class GetOfferQueryHandler : IQueryHandler<GetOfferQuery, OfferResponse>
{
    private readonly IListingOfferRepository _offers;
    private readonly IMarketplaceListingRepository _listings;
    private readonly ISellerProfileRepository _sellers;
    private readonly MarketplaceAuthorizationService _authorization;

    public GetOfferQueryHandler(
        IListingOfferRepository offers,
        IMarketplaceListingRepository listings,
        ISellerProfileRepository sellers,
        MarketplaceAuthorizationService authorization)
    {
        _offers = offers;
        _listings = listings;
        _sellers = sellers;
        _authorization = authorization;
    }

    public async Task<Result<OfferResponse>> Handle(
        GetOfferQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<OfferResponse>(caller.Error);
        }

        var offer = await _offers.GetForReadAsync(
            ListingOfferId.From(query.OfferId), cancellationToken);

        if (offer is null || !offer.Involves(caller.Value))
        {
            return Result.Failure<OfferResponse>(MarketplaceErrors.OfferNotFound);
        }

        var listing = await _listings.GetForReadAsync(offer.ListingId, cancellationToken);
        var seller = await _sellers.GetForReadAsync(offer.SellerId, cancellationToken);

        return Result.Success(
            OfferMapper.ToResponse(offer, caller.Value, listing, seller));
    }
}
