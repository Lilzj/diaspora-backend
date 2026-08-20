using Diaspora.Application.Abstractions;
using Diaspora.Modules.Marketplace.Application.Mapping;
using Diaspora.Modules.Marketplace.Application.Services;
using Diaspora.Modules.Marketplace.Contracts.V1.Responses;
using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.Modules.Marketplace.Domain.Offers;
using Diaspora.Modules.Marketplace.Domain.Policies;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Marketplace.Application.Offers;

/// <summary>
/// Reads the caller's negotiations from whichever side they asked for.
/// <para>
/// Selling resolves the caller's seller profile and scopes to it; buying scopes to their member
/// identity. A caller with no seller profile asking for the selling side gets an empty page rather
/// than an error — they have simply never sold anything.
/// </para>
/// </summary>
public sealed class ListOffersQueryHandler
    : IQueryHandler<ListOffersQuery, CursorPagedResult<OfferCardResponse>>
{
    private readonly IListingOfferRepository _offers;
    private readonly IMarketplaceListingRepository _listings;
    private readonly MarketplaceAuthorizationService _authorization;

    public ListOffersQueryHandler(
        IListingOfferRepository offers,
        IMarketplaceListingRepository listings,
        MarketplaceAuthorizationService authorization)
    {
        _offers = offers;
        _listings = listings;
        _authorization = authorization;
    }

    public async Task<Result<CursorPagedResult<OfferCardResponse>>> Handle(
        ListOffersQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<CursorPagedResult<OfferCardResponse>>(caller.Error);
        }

        var selling = string.Equals(query.Side, "Selling", StringComparison.OrdinalIgnoreCase);

        var seller = selling
            ? await _authorization.FindCallerSellerAsync(cancellationToken)
            : null;

        if (selling && seller is null)
        {
            return Result.Success(
                CursorPagedResult<OfferCardResponse>.Create([], null, hasMore: false));
        }

        var limit = Math.Clamp(
            query.Limit ?? MarketplaceLimits.DefaultPageSize, 1, MarketplaceLimits.MaxPageSize);

        var filter = new OfferQueryFilter
        {
            ListingId = query.ListingId is { } listingId
                ? MarketplaceListingId.From(listingId)
                : null,

            // Exactly one of these is set, which is what scopes the read to the caller.
            SellerId = selling ? seller!.Id : null,
            BuyerUserId = selling ? null : caller.Value,
            OpenOnly = query.OpenOnly,
            AwaitingParty = query.AwaitingMeOnly
                ? selling ? OfferParty.Seller : OfferParty.Buyer
                : null,
            Limit = limit
        };

        var page = await _offers.QueryAsync(filter, query.Cursor, limit, cancellationToken);

        var listings = await _listings.GetManyAsync(
            [.. page.Items.Select(offer => offer.ListingId).Distinct()], cancellationToken);

        var byId = listings.ToDictionary(listing => listing.Id);

        var cards = page.Items
            .Select(offer => OfferMapper.ToCard(
                offer, caller.Value, byId.GetValueOrDefault(offer.ListingId)))
            .ToList();

        return Result.Success(
            CursorPagedResult<OfferCardResponse>.Create(
                cards, page.NextCursor, page.HasMore));
    }
}
