using Diaspora.Contracts.Location.V1;
using Diaspora.Modules.Location.Domain.Search;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Location.Domain.Areas;

/// <summary>
/// How the module loads and stores one node of the hierarchy: identity by id, slug or batch, the
/// ancestor path, the set-based descendant rewrite a relocation performs, and the import's two
/// writes.
/// <para>
/// Note what is not here: no method takes a coordinate. Spatial questions go through
/// <see cref="Spatial.IGeospatialQueryProvider"/> instead, because the answer may not come
/// from this database at all — an Elasticsearch or Places-backed provider satisfies the same
/// question without touching a row. Keeping the two apart is what makes the provider seam
/// real rather than decorative; the candidate rows such a provider refines are on
/// <see cref="IAdministrativeAreaGeometry"/>, which is a different thing from answering the
/// question.
/// </para>
/// </summary>
public interface IAdministrativeAreaRepository
{
    Task<AdministrativeArea?> GetByIdAsync(AreaId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves a node by its slug, optionally within a country.
    /// <para>
    /// Slugs are unique within a parent, not globally, so an unqualified lookup can be
    /// ambiguous. The most prominent match wins — which is what makes <c>/peckham</c> resolve
    /// to the London one, and is exactly how the frontend's existing links behave.
    /// </para>
    /// </summary>
    Task<AdministrativeArea?> GetBySlugAsync(
        LocationSlug slug,
        AdministrativeLevel? level = null,
        CountryCode? countryCode = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdministrativeArea>> GetManyAsync(
        IReadOnlyCollection<AreaId> ids, CancellationToken cancellationToken = default);

    /// <summary>Every node on the path from the root down to — and excluding — this one.</summary>
    Task<IReadOnlyList<AdministrativeArea>> GetAncestorsAsync(
        AdministrativeArea area, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rewrites the path prefix of every descendant after a relocation.
    /// <para>
    /// A set-based update rather than a load-and-save loop: a redrawn boundary can move
    /// thousands of nodes, and materialising them all to change one string each is how a rare
    /// operation becomes an outage.
    /// </para>
    /// </summary>
    Task<int> RewriteDescendantPathsAsync(
        AreaRelocation relocation, CancellationToken cancellationToken = default);

    Task AddAsync(AdministrativeArea area, CancellationToken cancellationToken = default);

    Task<bool> AnyAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Walking the hierarchy for a screen: the children of a node, every node at a level, a name
/// search, and the exact-name match that files a row under the place a member typed.
/// <para>
/// <see cref="GetAncestorsAsync"/> is declared here too. Twelve of the hierarchy's fifteen
/// consumers ask for the ancestor path, and a screen that has just listed a node's children needs
/// it to render the breadcrumb above them -- taking the write side to do that would be the defect
/// this split exists to remove.
/// </para>
/// </summary>
public interface IAdministrativeAreaSearch
{
    /// <summary>Every node on the path from the root down to — and excluding — this one.</summary>
    Task<IReadOnlyList<AdministrativeArea>> GetAncestorsAsync(
        AdministrativeArea area, CancellationToken cancellationToken = default);

    /// <summary>The direct children of a node, or of a country when <paramref name="parentId"/> is null.</summary>
    Task<PagedResult<AdministrativeArea>> GetChildrenAsync(
        CountryId countryId,
        AreaId? parentId,
        AdministrativeLevel? level,
        PaginationRequest pagination,
        CancellationToken cancellationToken = default);

    /// <summary>Every node at a level within a country. Backs the cities and regions endpoints.</summary>
    Task<PagedResult<AdministrativeArea>> GetByLevelAsync(
        CountryCode countryCode,
        AdministrativeLevel level,
        string? term,
        PaginationRequest pagination,
        CancellationToken cancellationToken = default);

    /// <summary>Name search. Ranking is applied by the caller; this returns candidates.</summary>
    Task<IReadOnlyList<AdministrativeArea>> SearchAsync(
        LocationTextQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Nodes whose name is exactly this, ignoring case, at any of these levels.
    /// <para>
    /// Not a substring search: this backs filing a row under the place a member named, where
    /// "London" must find London and must not find "New London" or "Londonderry". Matching is
    /// lowered on both sides rather than done with ILike, which is Npgsql-only and does not
    /// translate on the SQLite test host — the lesson the housing and community repositories
    /// both record.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<AdministrativeArea>> FindByNameAsync(
        string name,
        IReadOnlyCollection<AdministrativeLevel> levels,
        CountryCode? countryCode,
        int limit,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// The candidate sets a spatial provider refines: centres inside a box, stored outlines
/// overlapping one, and everything beneath a path.
/// <para>
/// Separate because these change with the map, and because the interface's own note says spatial
/// questions belong to <c>IGeospatialQueryProvider</c> -- these three are the rows it reads, not
/// the question it answers.
/// </para>
/// </summary>
public interface IAdministrativeAreaGeometry
{
    /// <summary>
    /// Candidates for a spatial query: everything with a coordinate inside the box, after the
    /// level, country and path filters. The set an in-process provider refines by exact
    /// distance.
    /// </summary>
    Task<IReadOnlyList<AdministrativeArea>> GetWithinBoundsAsync(
        BoundingBox bounds,
        IReadOnlyCollection<AdministrativeLevel> levels,
        CountryCode? countryCode,
        HierarchyPath? withinPath,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Nodes with a stored outline whose extent overlaps the box, after the level and country
    /// filters. The extent is the only test here: whether a point is inside the ring is the
    /// caller's to decide, in process, against the ring itself.
    /// <para>
    /// Separate from <see cref="GetWithinBoundsAsync"/> because that one asks where a node's
    /// <em>centre</em> is. A town's outline reaches well past a box drawn around the town's centre,
    /// and a map showing the edge of Quatre Bornes needs the Quatre Bornes line although the town's
    /// centre is off screen.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<AdministrativeArea>> GetOutlinedOverlappingAsync(
        BoundingBox bounds,
        IReadOnlyCollection<AdministrativeLevel> levels,
        CountryCode? countryCode,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>Everything beneath a path. Backs city-wide and country-wide scopes.</summary>
    Task<IReadOnlyList<AdministrativeArea>> GetDescendantsAsync(
        HierarchyPath path,
        IReadOnlyCollection<AdministrativeLevel> levels,
        int limit,
        CancellationToken cancellationToken = default);
}
