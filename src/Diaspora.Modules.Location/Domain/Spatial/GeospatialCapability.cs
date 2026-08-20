namespace Diaspora.Modules.Location.Domain.Spatial;

/// <summary>
/// A spatial operation a query provider may or may not be able to perform.
/// <para>
/// Declared rather than assumed. The default relational provider can answer radius and
/// bounding-box queries on any database; polygon containment it can only do in memory over a
/// bounded candidate set, and true nearest-neighbour ordering it cannot do at scale at all
/// without a spatial index. A caller asking for something the active provider cannot do gets
/// <c>location.spatial.unsupported</c> and a clear reason, rather than a query that quietly
/// scans the table or silently returns fewer results than exist.
/// </para>
/// </summary>
[Flags]
public enum GeospatialCapability
{
    None = 0,

    /// <summary>Points within a distance of a centre.</summary>
    RadiusSearch = 1,

    /// <summary>Points inside an axis-aligned rectangle.</summary>
    BoundingBoxSearch = 2,

    /// <summary>Points inside an arbitrary polygon.</summary>
    PolygonSearch = 4,

    /// <summary>Ordering by true distance, pushed into the datastore.</summary>
    NearestNeighbor = 8,

    /// <summary>Whether one stored geometry contains another.</summary>
    GeometryContainment = 16
}
