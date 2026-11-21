using Diaspora.Contracts.Location.V1;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Location.Integration;

/// <summary>
/// The read surface other modules use to look geography up.
/// <para>
/// Deliberately narrower than the repositories behind it. A vertical needs to resolve a
/// place, render a breadcrumb, and measure a distance; it does not need to relocate a
/// subtree or rewrite a path, and handing it <c>IAdministrativeAreaRepository</c> would give
/// it both. The narrow interface is also the one that can stay stable if the storage behind
/// it changes.
/// </para>
/// <para>
/// <b>It answers with projections, and until 21 November 2026 it did not.</b> Every method
/// below returned <c>Country</c> or <c>AdministrativeArea</c> — B4's own aggregates — so the
/// seam written to stop a vertical reading B4's internals was handing them over, and three
/// modules had given up on it and injected <c>IAdministrativeAreaRepository</c> directly,
/// which is the one thing the paragraph above says it exists to prevent. The projections are
/// <see cref="AreaDescriptor"/> and <see cref="CountryDescriptor"/>, both published.
/// </para>
/// <para>
/// The change cost no consumer anything, which is its own small lesson: <b>every module
/// outside B4 called only <see cref="DistanceBetween"/></b>, whose types were already
/// published. The aggregate-returning methods had no external caller at all — the seam leaked
/// in principle for months before anyone could have noticed in practice, and the modules that
/// did need areas had routed around it rather than complain.
/// </para>
/// <para>
/// Every method is batched or single-fetch by design. There is no "get all areas" — a
/// vertical that wants to iterate the catalogue is about to do something Location should be
/// doing for it.
/// </para>
/// </summary>
public interface ILocationDirectory
{
    /// <summary>A country by ISO code, or null.</summary>
    Task<CountryDescriptor?> FindCountryAsync(
        CountryCode code, CancellationToken cancellationToken = default);

    /// <summary>A catalogue node by identifier, or null.</summary>
    Task<AreaDescriptor?> FindAreaAsync(
        AreaId areaId, CancellationToken cancellationToken = default);

    /// <summary>Several nodes at once. The N+1 guard for any screen rendering a list.</summary>
    Task<IReadOnlyDictionary<AreaId, AreaDescriptor>> FindAreasAsync(
        IReadOnlyCollection<AreaId> areaIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// A node by its slug, most prominent match first.
    /// <para>
    /// Slugs are not unique across the catalogue — there is a Peckham in London and a Richmond
    /// in several countries — so <paramref name="level"/> and <paramref name="countryCode"/>
    /// are how a caller says which one it means. With neither, the most prominent wins, which
    /// is what makes a bare <c>/peckham</c> resolve the way a reader expects.
    /// </para>
    /// </summary>
    Task<AreaDescriptor?> FindAreaBySlugAsync(
        LocationSlug slug,
        AdministrativeLevel? level = null,
        CountryCode? countryCode = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The direct children of a node.
    /// <para>
    /// Takes the parent's identifier alone, rather than the country and the parent the
    /// repository behind it wants: a consumer that had to supply a <c>CountryId</c> would need
    /// a second lookup to find one, and the country of a node's children is never in doubt.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<AreaDescriptor>> GetChildAreasAsync(
        AreaId parentId,
        AdministrativeLevel? level = null,
        int limit = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// A node's breadcrumb, root first and including the node itself. What a listing card
    /// renders as "Peckham, London, United Kingdom".
    /// </summary>
    Task<IReadOnlyList<AreaDescriptor>> GetBreadcrumbAsync(
        AreaId areaId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The nearest catalogue node to a point, at or below a level.
    /// <para>
    /// The primitive a vertical uses when it has coordinates and needs a place: a listing
    /// created from a map pin becomes a listing in Peckham without the vertical owning a
    /// gazetteer.
    /// </para>
    /// </summary>
    Task<AreaDescriptor?> FindNearestAreaAsync(
        GeoCoordinate coordinate,
        AdministrativeLevel? level = null,
        CancellationToken cancellationToken = default);

    /// <summary>The distance between two points, by the platform's active earth model.</summary>
    Distance DistanceBetween(GeoCoordinate origin, GeoCoordinate destination);
}
