using Diaspora.Application.Abstractions;
using Diaspora.Modules.Marketplace.Application.Services;
using Diaspora.Modules.Marketplace.Contracts.V1.Responses;
using Diaspora.Modules.Marketplace.Domain.Conversations;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.Modules.Marketplace.Domain.Policies;
using Diaspora.Modules.Marketplace.Domain.Sellers;
using Diaspora.SharedKernel.Pagination;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Marketplace.Application.Conversations;

/// <summary>Reads the caller's conversation references, most recently active first.</summary>
public sealed class ListConversationsQueryHandler
    : IQueryHandler<ListConversationsQuery, CursorPagedResult<ConversationReferenceResponse>>
{
    private readonly IListingConversationRepository _conversations;
    private readonly IMarketplaceListingRepository _listings;
    private readonly ISellerProfileRepository _sellers;
    private readonly MarketplaceAuthorizationService _authorization;

    public ListConversationsQueryHandler(
        IListingConversationRepository conversations,
        IMarketplaceListingRepository listings,
        ISellerProfileRepository sellers,
        MarketplaceAuthorizationService authorization)
    {
        _conversations = conversations;
        _listings = listings;
        _sellers = sellers;
        _authorization = authorization;
    }

    public async Task<Result<CursorPagedResult<ConversationReferenceResponse>>> Handle(
        ListConversationsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<CursorPagedResult<ConversationReferenceResponse>>(
                caller.Error);
        }

        var limit = Math.Clamp(
            query.Limit ?? MarketplaceLimits.DefaultPageSize, 1, MarketplaceLimits.MaxPageSize);

        var page = await _conversations.QueryForUserAsync(
            caller.Value, query.Cursor, limit, cancellationToken);

        var listings = await _listings.GetManyAsync(
            [.. page.Items.Select(conversation => conversation.ListingId).Distinct()],
            cancellationToken);

        var byId = listings.ToDictionary(listing => listing.Id);

        var sellers = await _sellers.GetManyAsync(
            [.. page.Items.Select(conversation => conversation.SellerId).Distinct()],
            cancellationToken);

        var items = page.Items
            .Select(conversation =>
            {
                var listing = byId.GetValueOrDefault(conversation.ListingId);

                return new ConversationReferenceResponse
                {
                    ConversationId = conversation.Id.Value,
                    ListingId = conversation.ListingId.Value,
                    ListingTitle = listing?.Title.Value ?? string.Empty,
                    ListingCoverMediaId = listing?.CoverImage?.MediaId,
                    SellerId = conversation.SellerId.Value,
                    SellerName = sellers.GetValueOrDefault(conversation.SellerId)
                        ?.Identity.DisplayName ?? string.Empty,
                    BuyerUserId = conversation.BuyerUserId.Value,
                    Status = conversation.Status.ToString(),
                    ExternalConversationId = conversation.ExternalConversationId,
                    Provider = conversation.Provider,
                    ContactPreference =
                        (listing?.ContactPreference ?? Domain.Enums.ContactPreference.InApp)
                            .ToString(),
                    CreatedAt = conversation.CreatedAt,
                    LastActivityAt = conversation.LastActivityAt
                };
            })
            .ToList();

        return Result.Success(
            CursorPagedResult<ConversationReferenceResponse>.Create(
                items, page.NextCursor, page.HasMore));
    }
}
