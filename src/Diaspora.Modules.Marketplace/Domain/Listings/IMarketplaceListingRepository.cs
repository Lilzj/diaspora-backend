using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.Modules.Marketplace.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Marketplace.Domain.Listings;

/// <summary>
/// How the module loads and stores a listing it is about to change.
/// <para>
/// Identity-based access, the slug uniqueness check that guards it, and the two batch loads whose
/// callers mutate what they loaded — expiry and the publish sweep. Those return tracked
/// aggregates and are writes, whatever their names suggest.
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

    Task AddAsync(MarketplaceListing listing, CancellationToken cancellationToken = default);

    void Remove(MarketplaceListing listing);
}

/// <summary>
/// The browse path: a page of listings and how many match.
/// <para>
/// Changes when a screen gains a filter or a sort, which is not a reason for the aggregate's
/// persistence contract to change. <see cref="GetManyAsync"/> is declared here as well, because
/// hydrating the ids a search returned is the same operation as loading a batch by id.
/// </para>
/// </summary>
public interface IMarketplaceListingSearch
{
    /// <summary>A page of cards, cursor-paged.</summary>
    Task<CursorPagedResult<MarketplaceListing>> QueryAsync(
        ListingQueryFilter filter,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default);

    Task<int> CountAsync(
        ListingQueryFilter filter, CancellationToken cancellationToken = default);

    /// <summary>Card graphs for a set of identifiers. What a search engine's answer is hydrated with.</summary>
    Task<IReadOnlyList<MarketplaceListing>> GetManyAsync(
        IReadOnlyCollection<MarketplaceListingId> ids,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// What a seller currently has on the marketplace.
/// <para>
/// The active-listing questions a create, duplicate or publish command asks before it writes, and
/// the seller's own list. Domain invariants rather than reports: they change when the selling
/// rules change.
/// </para>
/// </summary>
public interface IMarketplaceListingSellerFacts
{
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
}

/// <summary>
/// How many listings are in a place.
/// <para>
/// What B4's resource counters ask when they compose an area's page. <see cref="QueryAsync"/> is
/// declared here as well as on the search interface, because the counter reaches a place's
/// listings through the same filtered query a browse screen uses.
/// </para>
/// </summary>
public interface IMarketplaceListingStatistics
{
    /// <summary>
    /// Live listings in an area, counted by materialised path prefix. Location's neighbourhood hub
    /// reads this.
    /// </summary>
    Task<int> CountInAreaAsync(string areaPath, CancellationToken cancellationToken = default);

    Task<int> CountInCountryAsync(string countryCode, CancellationToken cancellationToken = default);

    /// <summary>A page of cards, cursor-paged.</summary>
    Task<CursorPagedResult<MarketplaceListing>> QueryAsync(
        ListingQueryFilter filter,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default);
}
