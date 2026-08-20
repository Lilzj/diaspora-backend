using Diaspora.Modules.Location.Domain.Areas;
using Diaspora.Modules.Location.Domain.Countries;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Location.Integration;

/// <summary>
/// The read surface other modules use to look geography up.
/// <para>
/// Deliberately narrower than the repositories behind it. A vertical needs to resolve a
/// place, render a breadcrumb, and measure a distance; it does not need to relocate a
/// subtree or rewrite a path, and handing it
/// <see cref="IAdministrativeAreaRepository"/> would give it both. The narrow interface is
/// also the one that can stay stable if the storage behind it changes.
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
    Task<Country?> FindCountryAsync(
        CountryCode code, CancellationToken cancellationToken = default);

    /// <summary>A catalogue node by identifier, or null.</summary>
    Task<AdministrativeArea?> FindAreaAsync(
        AreaId areaId, CancellationToken cancellationToken = default);

    /// <summary>Several nodes at once. The N+1 guard for any screen rendering a list.</summary>
    Task<IReadOnlyDictionary<AreaId, AdministrativeArea>> FindAreasAsync(
        IReadOnlyCollection<AreaId> areaIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// A node's breadcrumb, root first and including the node itself. What a listing card
    /// renders as "Peckham, London, United Kingdom".
    /// </summary>
    Task<IReadOnlyList<AdministrativeArea>> GetBreadcrumbAsync(
        AreaId areaId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The nearest catalogue node to a point, at or below a level.
    /// <para>
    /// The primitive a vertical uses when it has coordinates and needs a place: a listing
    /// created from a map pin becomes a listing in Peckham without the vertical owning a
    /// gazetteer.
    /// </para>
    /// </summary>
    Task<AdministrativeArea?> FindNearestAreaAsync(
        GeoCoordinate coordinate,
        Domain.Enums.AdministrativeLevel? level = null,
        CancellationToken cancellationToken = default);

    /// <summary>The distance between two points, by the platform's active earth model.</summary>
    Distance DistanceBetween(GeoCoordinate origin, GeoCoordinate destination);
}
