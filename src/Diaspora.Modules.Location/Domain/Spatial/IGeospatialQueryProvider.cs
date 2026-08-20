using Diaspora.Modules.Location.Domain.Areas;
using Diaspora.Modules.Location.Domain.Search;

namespace Diaspora.Modules.Location.Domain.Spatial;

/// <summary>
/// Answers spatial questions about catalogue nodes.
/// <para>
/// The seam the brief exists to require: "future support should be possible for PostGIS, SQL
/// Server Spatial, Elasticsearch, OpenSearch, Google Places, Mapbox and OpenStreetMap without
/// changing the domain". Everything above this interface deals in
/// <see cref="Domain.ValueObjects.GeoCoordinate"/>, <see cref="Domain.ValueObjects.Distance"/>
/// and <see cref="AdministrativeArea"/>; nothing above it knows whether the filtering
/// happened in a <c>WHERE</c> clause, in an ST_DWithin call, in a geo_distance aggregation, or
/// in this process.
/// </para>
/// <para>
/// Providers are discovered by assembly scan and selected by configuration, exactly as
/// Trust's verification-type handlers are. Registering a PostGIS provider is one class in one
/// assembly plus one configuration key — no edit to this module, and certainly none to the
/// domain.
/// </para>
/// </summary>
public interface IGeospatialQueryProvider
{
    /// <summary>
    /// The configuration key that selects this provider. Unique across the platform; a
    /// duplicate fails startup rather than resolving by load order.
    /// </summary>
    string Key { get; }

    /// <summary>What this provider can actually do. See <see cref="GeospatialCapability"/>.</summary>
    GeospatialCapability Capabilities { get; }

    /// <summary>Catalogue nodes within the search's radius, nearest first.</summary>
    Task<IReadOnlyList<AreaProximityResult>> FindWithinRadiusAsync(
        NearbySearch search, CancellationToken cancellationToken = default);

    /// <summary>Catalogue nodes inside the box. Ordered by prominence, then name.</summary>
    Task<IReadOnlyList<AreaProximityResult>> FindWithinBoundsAsync(
        BoundingBoxSearch search, CancellationToken cancellationToken = default);

    /// <summary>Catalogue nodes inside the polygon.</summary>
    Task<IReadOnlyList<AreaProximityResult>> FindWithinPolygonAsync(
        PolygonSearch search, CancellationToken cancellationToken = default);

    /// <summary>
    /// The single closest node at or below the requested level, or null when nothing is in
    /// range. The primitive behind reverse geocoding and "nearest city".
    /// </summary>
    Task<AreaProximityResult?> FindNearestAsync(
        NearbySearch search, CancellationToken cancellationToken = default);
}
