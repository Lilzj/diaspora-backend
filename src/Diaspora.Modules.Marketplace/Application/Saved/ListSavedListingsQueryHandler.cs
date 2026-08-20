using Diaspora.Application.Abstractions;
using Diaspora.Modules.Marketplace.Application.Mapping;
using Diaspora.Modules.Marketplace.Application.Services;
using Diaspora.Modules.Marketplace.Contracts.V1.Responses;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.Modules.Marketplace.Domain.Policies;
using Diaspora.Modules.Marketplace.Domain.Saved;
using Diaspora.Modules.Marketplace.Domain.Sellers;
using Diaspora.SharedKernel.Pagination;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Marketplace.Application.Saved;

/// <summary>
/// Reads the caller's watchlist, with each listing's card.
/// <para>
/// <b>Listings the caller can no longer see are dropped rather than rendered as blanks.</b> A saved
/// item that has been deleted or hidden is one the platform should stop showing — but the saved row
/// survives, so if the seller restores the listing it reappears in the watchlist without the member
/// having to save it again.
/// </para>
/// </summary>
public sealed class ListSavedListingsQueryHandler
    : IQueryHandler<ListSavedListingsQuery, CursorPagedResult<SavedListingResponse>>
{
    private readonly ISavedListingRepository _saved;
    private readonly IMarketplaceListingRepository _listings;
    private readonly ISellerProfileRepository _sellers;
    private readonly MarketplaceAuthorizationService _authorization;

    public ListSavedListingsQueryHandler(
        ISavedListingRepository saved,
        IMarketplaceListingRepository listings,
        ISellerProfileRepository sellers,
        MarketplaceAuthorizationService authorization)
    {
        _saved = saved;
        _listings = listings;
        _sellers = sellers;
        _authorization = authorization;
    }

    public async Task<Result<CursorPagedResult<SavedListingResponse>>> Handle(
        ListSavedListingsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<CursorPagedResult<SavedListingResponse>>(caller.Error);
        }

        var limit = Math.Clamp(
            query.Limit ?? MarketplaceLimits.DefaultPageSize, 1, MarketplaceLimits.MaxPageSize);

        var page = await _saved.QueryForUserAsync(
            caller.Value, query.Cursor, limit, cancellationToken);

        var listings = await _listings.GetManyAsync(
            [.. page.Items.Select(saved => saved.ListingId)], cancellationToken);

        var byId = listings.ToDictionary(listing => listing.Id);

        var sellers = await _sellers.GetManyAsync(
            [.. listings.Select(listing => listing.SellerId).Distinct()], cancellationToken);

        var items = new List<SavedListingResponse>(page.Items.Count);

        foreach (var saved in page.Items)
        {
            if (!byId.TryGetValue(saved.ListingId, out var listing))
            {
                // Deleted, or no longer visible. Dropped from the render; the row stays, so a
                // restored listing reappears here on its own.
                continue;
            }

            items.Add(new SavedListingResponse
            {
                SavedListingId = saved.Id.Value,
                Listing = ListingMapper.ToCard(
                    listing, sellers.GetValueOrDefault(listing.SellerId), isSaved: true),
                Note = saved.Note,
                NotifyOnPriceChange = saved.NotifyOnPriceChange,
                SavedAt = saved.CreatedAt
            });
        }

        return Result.Success(
            CursorPagedResult<SavedListingResponse>.Create(
                items, page.NextCursor, page.HasMore));
    }
}
