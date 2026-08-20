using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.Modules.Marketplace.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Marketplace.Domain.Listings;

/// <summary>
/// How the application layer reaches listings.
/// <para>
/// <b>Three read shapes, because the graphs genuinely differ.</b> <c>GetAsync</c> loads everything,
/// because a command needs an aggregate that can enforce its own invariants — a partially loaded
/// listing cannot validate an attribute replacement against attributes it never loaded.
/// <c>GetForReadAsync</c> loads the same graph untracked, because the detail screen renders all of
/// it. <c>QueryAsync</c> loads the <em>card</em> graph: the cover image and the tags, and nothing
/// else. A results page of twenty cards that eagerly loaded twenty full listings would be several
/// thousand rows to render twenty titles.
/// </para>
/// </summary>
public interface IMarketplaceListingRepository
{
    /// <summary>The full graph, tracked. For a command.</summary>
    Task<MarketplaceListing?> GetAsync(
        MarketplaceListingId id, CancellationToken cancellationToken = default);

    /// <summary>The full graph, untracked. For the detail screen.</summary>
    Task<MarketplaceListing?> GetForReadAsync(
        MarketplaceListingId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// The full graph including soft-deleted rows. The one read an undelete can be built on —
    /// the global query filter hides the very row a restore has to load.
    /// </summary>
    Task<MarketplaceListing?> GetIncludingDeletedAsync(
        MarketplaceListingId id, CancellationToken cancellationToken = default);

    Task<MarketplaceListing?> GetBySlugAsync(
        ListingSlug slug, CancellationToken cancellationToken = default);

    /// <summary>Card graphs for a set of identifiers. What a search engine's answer is hydrated with.</summary>
    Task<IReadOnlyList<MarketplaceListing>> GetManyAsync(
        IReadOnlyCollection<MarketplaceListingId> ids,
        CancellationToken cancellationToken = default);

    /// <summary>The listing a Trust outcome names, found by the case identifier it was stamped with.</summary>
    Task<MarketplaceListing?> GetByVerificationIdAsync(
        Guid verificationId, CancellationToken cancellationToken = default);

    /// <summary>A page of cards, cursor-paged.</summary>
    Task<CursorPagedResult<MarketplaceListing>> QueryAsync(
        ListingQueryFilter filter,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default);

    Task<int> CountAsync(
        ListingQueryFilter filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether a handle is taken, deleted rows included: a deleted listing keeps its slug, and
    /// handing it to a new listing would break every link ever shared to the old one.
    /// </summary>
    Task<bool> SlugExistsAsync(
        ListingSlug slug, CancellationToken cancellationToken = default);

    /// <summary>The expiry sweep's work list, oldest first.</summary>
    Task<IReadOnlyList<MarketplaceListing>> GetExpiredAsync(
        DateTimeOffset asOf, int limit, CancellationToken cancellationToken = default);

    /// <summary>Everything published since an instant. What a reindex or a standing search reads.</summary>
    Task<IReadOnlyList<MarketplaceListing>> GetPublishedSinceAsync(
        DateTimeOffset since, int limit, CancellationToken cancellationToken = default);

    /// <summary>Every live listing of one seller. What a suspension cascade acts on.</summary>
    Task<IReadOnlyList<MarketplaceListing>> GetActiveBySellerAsync(
        SellerProfileId sellerId, int limit, CancellationToken cancellationToken = default);

    /// <summary>How many live listings a seller holds. The abuse bound the create command checks.</summary>
    Task<int> CountActiveBySellerAsync(
        SellerProfileId sellerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether the seller already has a live listing with this exact title. The brief's duplicate
    /// detection, at the only point where refusing is kinder than flagging.
    /// </summary>
    Task<bool> HasDuplicateAsync(
        SellerProfileId sellerId,
        string title,
        MarketplaceListingId? excluding = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Live listings in an area, counted by materialised path prefix. Location's neighbourhood hub
    /// reads this.
    /// </summary>
    Task<int> CountInAreaAsync(string areaPath, CancellationToken cancellationToken = default);

    Task<int> CountInCountryAsync(string countryCode, CancellationToken cancellationToken = default);

    Task AddAsync(MarketplaceListing listing, CancellationToken cancellationToken = default);

    void Remove(MarketplaceListing listing);
}
