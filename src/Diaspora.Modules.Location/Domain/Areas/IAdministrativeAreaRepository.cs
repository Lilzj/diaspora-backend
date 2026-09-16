using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.Modules.Location.Domain.Search;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Location.Domain.Areas;

/// <summary>
/// The hierarchy's persistence boundary.
/// <para>
/// Note what is not here: no method takes a coordinate. Spatial questions go through
/// <see cref="Spatial.IGeospatialQueryProvider"/> instead, because the answer may not come
/// from this database at all — an Elasticsearch or Places-backed provider satisfies the same
/// question without touching a row. Keeping the two apart is what makes the provider seam
/// real rather than decorative.
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

    /// <summary>Everything beneath a path. Backs city-wide and country-wide scopes.</summary>
    Task<IReadOnlyList<AdministrativeArea>> GetDescendantsAsync(
        HierarchyPath path,
        IReadOnlyCollection<AdministrativeLevel> levels,
        int limit,
        CancellationToken cancellationToken = default);

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
