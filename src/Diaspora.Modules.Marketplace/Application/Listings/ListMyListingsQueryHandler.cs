using Diaspora.Application.Abstractions;
using Diaspora.Modules.Marketplace.Application.Mapping;
using Diaspora.Modules.Marketplace.Application.Services;
using Diaspora.Modules.Marketplace.Contracts.V1.Responses;
using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.Modules.Marketplace.Domain.Policies;
using Diaspora.SharedKernel.Pagination;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Marketplace.Application.Listings;

/// <summary>
/// Reads the caller's own listings.
/// <para>
/// A caller with no seller profile gets an empty page rather than an error: they have simply never
/// listed anything, which is not a failure.
/// </para>
/// </summary>
public sealed class ListMyListingsQueryHandler
    : IQueryHandler<ListMyListingsQuery, CursorPagedResult<ListingCardResponse>>
{
    private readonly IMarketplaceListingRepository _listings;
    private readonly MarketplaceAuthorizationService _authorization;

    public ListMyListingsQueryHandler(
        IMarketplaceListingRepository listings,
        MarketplaceAuthorizationService authorization)
    {
        _listings = listings;
        _authorization = authorization;
    }

    public async Task<Result<CursorPagedResult<ListingCardResponse>>> Handle(
        ListMyListingsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var seller = await _authorization.FindCallerSellerAsync(cancellationToken);

        if (seller is null)
        {
            return Result.Success(
                CursorPagedResult<ListingCardResponse>.Create([], null, hasMore: false));
        }

        var limit = Math.Clamp(
            query.Limit ?? MarketplaceLimits.DefaultPageSize, 1, MarketplaceLimits.MaxPageSize);

        var status = MarketplaceWireNames.ParseListingStatus(query.Status);

        var filter = await _authorization.ApplyAudienceAsync(
            new ListingQueryFilter
            {
                SellerId = seller.Id,
                Statuses = status is { } value ? [value] : null,
                Sort = ListingSortOrder.Newest,
                Limit = limit
            },
            includeOwn: true,
            cancellationToken);

        var page = await _listings.QueryAsync(filter, query.Cursor, limit, cancellationToken);

        var cards = page.Items
            .Select(listing => ListingMapper.ToCard(listing, seller))
            .ToList();

        return Result.Success(
            CursorPagedResult<ListingCardResponse>.Create(
                cards, page.NextCursor, page.HasMore));
    }
}
