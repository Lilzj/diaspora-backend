using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.ValueObjects;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Housing.Domain.Listings;

/// <summary>
/// How the module loads and stores a listing it is about to change.
/// <para>
/// <b>Three read shapes, and the graphs really do differ.</b> <see cref="GetAsync"/> loads
/// everything because a command needs an aggregate that can enforce its own invariants — a
/// partially loaded listing cannot validate an amenity replacement against amenities it never
/// loaded. <see cref="GetForReadAsync"/> loads the same graph untracked for the detail screen.
/// The card graph a results page needs is <see cref="IHousingListingSearch"/>'s.
/// </para>
/// <para>
/// <b><see cref="GetBecomingAvailableAsync"/> is a write, not a report.</b> Its one caller loads
/// the batch, moves each listing's status and saves — so it returns tracked aggregates and
/// belongs here rather than with the queries it superficially resembles.
/// </para>
/// </summary>
public interface IHousingListingRepository
{
    /// <summary>The full graph, tracked. What a command needs.</summary>
    Task<HousingListing?> GetAsync(
        HousingListingId id, CancellationToken cancellationToken = default);

    /// <summary>The full graph, untracked. What the detail screen renders.</summary>
    Task<HousingListing?> GetForReadAsync(
        HousingListingId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// The full graph including soft-deleted rows.
    /// <para>
    /// Required by every restore path, and the reason is the global query filter: an undelete has
    /// to load a row the filter is hiding, so a restore written against the ordinary getter finds
    /// nothing and reports the listing missing. The platform records this as a standing trap.
    /// </para>
    /// </summary>
    Task<HousingListing?> GetIncludingDeletedAsync(
        HousingListingId id, CancellationToken cancellationToken = default);

    Task<HousingListing?> GetBySlugAsync(
        HousingSlug slug, CancellationToken cancellationToken = default);

    /// <summary>The listing a Trust outcome correlates to.</summary>
    Task<HousingListing?> GetByVerificationIdAsync(
        Guid verificationId, CancellationToken cancellationToken = default);

    /// <summary>Card graphs for a set of identifiers. Batched, for Community's descriptors.</summary>
    Task<IReadOnlyList<HousingListing>> GetManyAsync(
        IReadOnlyCollection<HousingListingId> ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// Listings whose move-in date has arrived while they were still merely published.
    /// <para>
    /// The availability sweep's work list, and the reason the Published/Available split does not
    /// need a landlord to remember anything: a flat advertised in March for June becomes available
    /// on its own.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<HousingListing>> GetBecomingAvailableAsync(
        DateTimeOffset asOf, int limit, CancellationToken cancellationToken = default);

    Task AddAsync(HousingListing listing, CancellationToken cancellationToken = default);

    void Remove(HousingListing listing);
}

/// <summary>
/// The browse and discovery path: a page of listings, how many match, and the nearest few.
/// <para>
/// Separate from <see cref="IHousingListingRepository"/> because it changes for a different
/// reason — a new filter, sort or facet is a change to a screen, not to the aggregate's
/// persistence contract. <see cref="QueryAsync"/> loads the <em>card</em> graph: media and
/// amenities, and nothing else. A results page of twenty cards that eagerly loaded twenty full
/// listings would be several thousand rows to render twenty titles.
/// </para>
/// <para>
/// <see cref="GetManyAsync"/> is declared here as well as on the repository, and deliberately:
/// hydrating the ids a search returned is the same operation as loading a batch by id, and a
/// caller that does only that should not have to take the write side to get it.
/// </para>
/// </summary>
public interface IHousingListingSearch
{
    /// <summary>A page of card graphs.</summary>
    Task<CursorPagedResult<HousingListing>> QueryAsync(
        HousingListingQueryFilter filter,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default);

    Task<int> CountAsync(
        HousingListingQueryFilter filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every match with a position, as identifiers and coordinates only, up to
    /// <paramref name="limit"/>. For a "nearest first" search, which orders by a distance only
    /// Location may measure and so cannot ask the database to sort.
    /// </summary>
    Task<IReadOnlyList<HousingNearestCandidate>> QueryNearestCandidatesAsync(
        HousingListingQueryFilter filter, int limit, CancellationToken cancellationToken = default);

    /// <summary>Card graphs for a set of identifiers. Batched, for Community's descriptors.</summary>
    Task<IReadOnlyList<HousingListing>> GetManyAsync(
        IReadOnlyCollection<HousingListingId> ids, CancellationToken cancellationToken = default);
}

/// <summary>
/// The questions a listing command asks before it writes: is this slug free, is this a duplicate,
/// is this landlord within quota.
/// <para>
/// These are domain invariants rather than reports, which is why they are not on the search
/// interface, and they change when the rules change rather than when the aggregate does, which is
/// why they are not on the repository. A create handler legitimately depends on this and on the
/// repository — that pair is the dependency it actually has.
/// </para>
/// </summary>
public interface IHousingListingGuards
{
    /// <summary>
    /// Whether a slug is taken, ignoring the soft-delete filter.
    /// <para>
    /// Deleted listings keep their slugs: handing one to a new property would break every link ever
    /// shared to the old one, and on a housing platform those links are in emails between people
    /// arranging to move.
    /// </para>
    /// </summary>
    Task<bool> SlugExistsAsync(HousingSlug slug, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether the landlord already has a live listing with this exact title.
    /// <para>
    /// Exact rather than fuzzy, deliberately. Near-duplicate detection is a fraud provider's job
    /// where a false positive is a moderator's note; a fuzzy match here would be a refusal, and
    /// refusing an agency's second listing of two genuinely similar flats in one block is worse
    /// than letting a duplicate through for review.
    /// </para>
    /// </summary>
    Task<bool> HasDuplicateAsync(
        LandlordProfileId landlordId,
        string title,
        HousingListingId? excluding = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// What a landlord holds against the creation quota.
    /// <para>
    /// <b>Drafts count here, and that is the point.</b> The bound exists to stop a script creating
    /// listings for free, and a script creates drafts. It makes this a poor answer to "how many
    /// properties has this landlord on the market", which is what
    /// <see cref="CountOnMarketByLandlordAsync"/> is for — the landlord's displayed figure was once
    /// filled from this one, and counted their drafts and their let properties as advertisements.
    /// </para>
    /// </summary>
    Task<int> CountAgainstListingQuotaAsync(
        LandlordProfileId landlordId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Properties a member could find and enquire about: published, available or reserved.
    /// <para>
    /// Not drafts, which nobody but their landlord can see; not occupied or unavailable ones, which
    /// are off the market by their own definition; not archived or deleted ones.
    /// </para>
    /// </summary>
    Task<int> CountOnMarketByLandlordAsync(
        LandlordProfileId landlordId, CancellationToken cancellationToken = default);

    /// <summary>Every property the landlord has, whatever state it is in. Archived included.</summary>
    Task<int> CountByLandlordAsync(
        LandlordProfileId landlordId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HousingListing>> GetByLandlordAsync(
        LandlordProfileId landlordId, int limit, CancellationToken cancellationToken = default);
}

/// <summary>
/// How many listings are in a place, and what they cost.
/// <para>
/// The counts B4 and B21 ask for when they compose an area's page, plus the rent sample B21's
/// cost-of-living index is built from. Nothing here loads an aggregate and nothing here is part
/// of the module's own screens, so it changes when another module's reporting changes.
/// </para>
/// </summary>
public interface IHousingListingStatistics
{
    /// <summary>Live listings in an area subtree. The resource counter's query.</summary>
    Task<int> CountInAreaAsync(string areaPath, CancellationToken cancellationToken = default);

    Task<int> CountInCountryAsync(
        string countryCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Which area a property is filed under, whether or not it still exists.
    /// <para>
    /// <b>Reads past the soft-delete filter, and that is the whole reason it exists.</b> When a
    /// property is deleted its neighbourhood has one fewer home to advertise — so that is exactly
    /// the moment the count must be recomputed, and exactly the moment an ordinary read can no
    /// longer find the property to ask where it was. Null when the property is unknown or was
    /// never filed under an area.
    /// </para>
    /// </summary>
    Task<Guid?> NeighborhoodAreaOfAsync(
        HousingListingId listingId, CancellationToken cancellationToken = default);

    /// <summary>Live listings against a B4 area exactly. What a neighbourhood profile counts.</summary>
    Task<int> CountInNeighborhoodAsync(
        Guid areaId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The neighbourhood areas that currently have live listings, with their counts.
    /// <para>
    /// One query rather than one per neighbourhood, because the neighbourhood directory renders
    /// dozens at a time and a count per row would be a fan of aggregates on every page.
    /// </para>
    /// </summary>
    Task<IReadOnlyDictionary<Guid, int>> CountByNeighborhoodAsync(
        IReadOnlyCollection<Guid> areaIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// The disclosed asking rents of the live listings in a country: amount, currency, period and
    /// where each home is filed. The rent statistics' query.
    /// <para>
    /// Rows only, no arithmetic. Periods are converted and medians taken in process, because a
    /// median does not translate to SQL on both providers and the rows are a few decimals each.
    /// Capped at <paramref name="limit"/>, newest first.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<HousingRentPrice>> GetRentPricesAsync(
        string countryCode, int limit, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every match with a position, as identifiers and coordinates only, up to
    /// <paramref name="limit"/>. For a "nearest first" search, which orders by a distance only
    /// Location may measure and so cannot ask the database to sort.
    /// </summary>
    Task<IReadOnlyList<HousingNearestCandidate>> QueryNearestCandidatesAsync(
        HousingListingQueryFilter filter, int limit, CancellationToken cancellationToken = default);
}
