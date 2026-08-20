using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Marketplace.Domain.Conversations;

/// <summary>How the application layer reaches conversation references.</summary>
public interface IListingConversationRepository
{
    Task<ListingConversation?> GetAsync(
        ListingConversationId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// The reference for this listing and this buyer, if one exists. Asking twice returns the same
    /// reference rather than opening a second conversation about the same item.
    /// </summary>
    Task<ListingConversation?> FindAsync(
        MarketplaceListingId listingId,
        UserId buyerUserId,
        CancellationToken cancellationToken = default);

    /// <summary>A member's conversations, whichever side of them they are on.</summary>
    Task<CursorPagedResult<ListingConversation>> QueryForUserAsync(
        UserId userId,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>Every open reference on a listing. What a sale closes.</summary>
    Task<IReadOnlyList<ListingConversation>> GetOpenForListingAsync(
        MarketplaceListingId listingId, CancellationToken cancellationToken = default);

    Task AddAsync(ListingConversation conversation, CancellationToken cancellationToken = default);
}
