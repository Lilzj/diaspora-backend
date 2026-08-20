namespace Diaspora.Modules.Housing.Integration;

/// <summary>
/// The published way to ask what a neighbourhood is like.
/// <para>
/// <b>This is the contract the B11 success criterion turns on.</b> The brief asks that
/// neighbourhoods become reusable, first-class platform concepts consumable by Housing, Jobs,
/// Marketplace, Providers, Guides, Community and future modules without any of them duplicating
/// ownership. That is three claims, and this interface is how each is kept:
/// </para>
/// <para>
/// <b>Reusable</b> — a consumer references this one interface and a flat summary record. It does
/// not reference Housing's aggregates, its identifiers, its repositories or its enums, so nothing
/// it depends on can be changed by a refactor inside a Housing aggregate.
/// </para>
/// <para>
/// <b>First-class</b> — a neighbourhood is addressed by B4's <c>AreaId</c>, the same identifier a
/// listing, a job, a provider and a post already carry. No module needs to know that Housing keeps
/// the profile in order to render one, and a consumer that already has an area identifier is
/// already holding the key.
/// </para>
/// <para>
/// <b>Not duplicated</b> — the <em>place</em> belongs to Location and always did: its name, its
/// slug, its hierarchy, its centroid, its parent city. What this returns is the <em>profile</em> —
/// the living summaries, the liveability bands, the community insights — which is the only part
/// Housing owns. A consumer wanting geography still asks B4; a consumer wanting to know what living
/// there is like asks this.
/// </para>
/// <para>
/// <b>Why the seam runs this way round rather than through Location.</b> Putting the profile in B4
/// was the obvious alternative and it is wrong: the content is editorial and moderated, with
/// authors, approvers, review dates and a submission queue, and none of that belongs in a
/// geographic catalogue whose value is being an authoritative reference. B4 stays a reference. B11
/// owns an opinion about it and publishes that opinion here.
/// </para>
/// <para>
/// <b>Every method degrades rather than throws.</b> A neighbourhood nobody has described yet is an
/// ordinary state — most neighbourhoods, for a long time — so a missing profile returns null and a
/// consumer renders the place without the extras.
/// </para>
/// </summary>
public interface INeighborhoodDirectory
{
    /// <summary>
    /// The profile for a B4 area, or null when nobody has written one.
    /// </summary>
    Task<NeighborhoodSummary?> GetAsync(
        Guid areaId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Profiles for several areas at once, keyed by area.
    /// <para>
    /// Batched because the calling shape is always a page: twenty jobs, twenty listings or twenty
    /// providers each wanting the neighbourhood chip beside them. A consumer looping over
    /// <see cref="GetAsync"/> would make that page a fan of queries.
    /// </para>
    /// </summary>
    Task<IReadOnlyDictionary<Guid, NeighborhoodSummary>> GetManyAsync(
        IReadOnlyCollection<Guid> areaIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// The profile behind a slug, optionally scoped to a country.
    /// <para>
    /// A slug is unique within its country rather than globally — B4's own note about there being a
    /// Springfield in twelve states — so a consumer resolving a link with no country in it gets the
    /// first match, which is what such a link can honestly resolve to.
    /// </para>
    /// </summary>
    Task<NeighborhoodSummary?> GetBySlugAsync(
        string slug, string? countryCode = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Neighbourhoods in a country or beneath an area, most active first.
    /// <para>
    /// Bounded rather than paged, because every consumer of this is rendering a picker or a
    /// discovery strip. A consumer that genuinely wants to page the whole set is asking a Housing
    /// question and should use Housing's own endpoint.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<NeighborhoodSummary>> FindAsync(
        string? countryCode = null,
        string? areaPathPrefix = null,
        int limit = 20,
        CancellationToken cancellationToken = default);
}
