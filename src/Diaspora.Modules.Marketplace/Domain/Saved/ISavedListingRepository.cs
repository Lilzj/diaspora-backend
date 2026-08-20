using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Marketplace.Domain.Saved;

/// <summary>How the application layer reaches saved listings.</summary>
public interface ISavedListingRepository
{
    Task<SavedListing?> GetAsync(
        SavedListingId id, CancellationToken cancellationToken = default);

    Task<SavedListing?> FindAsync(
        UserId userId,
        MarketplaceListingId listingId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Which of these listings the viewer has saved.
    /// <para>
    /// Batched deliberately: rendering twenty cards with a saved-state heart needs one query, not
    /// twenty. Returns raw <c>Guid</c>s because the caller checks membership rather than
    /// identity.
    /// </para>
    /// </summary>
    Task<IReadOnlySet<Guid>> FindSavedListingIdsAsync(
        UserId userId,
        IReadOnlyCollection<MarketplaceListingId> listingIds,
        CancellationToken cancellationToken = default);

    Task<CursorPagedResult<SavedListing>> QueryForUserAsync(
        UserId userId,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default);

    Task<int> CountForUserAsync(UserId userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Everyone watching a listing who asked to hear about price changes. What a price-drop
    /// notification is fanned out to.
    /// </summary>
    Task<IReadOnlyList<SavedListing>> GetPriceWatchersAsync(
        MarketplaceListingId listingId, CancellationToken cancellationToken = default);

    Task AddAsync(SavedListing saved, CancellationToken cancellationToken = default);

    void Remove(SavedListing saved);
}
