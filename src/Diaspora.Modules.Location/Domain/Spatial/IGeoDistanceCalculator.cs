using Diaspora.Modules.Location.Domain.ValueObjects;

namespace Diaspora.Modules.Location.Domain.Spatial;

/// <summary>
/// How far apart two points are.
/// <para>
/// An interface over one line of trigonometry, which needs justifying. The reason is that
/// the answer depends on an earth model: the spherical formula the platform uses today is
/// accurate to about 0.5%, which is invisible at neighbourhood scale and is a three-kilometre
/// error across a continent. When that starts to matter — for aviation-grade routing, or for
/// a country whose geodetic survey disagrees — the switch to an ellipsoidal model must be one
/// registration, not a search-and-replace across every query in the module.
/// </para>
/// <para>
/// The same seam is what lets a PostGIS-backed provider defer the calculation to the database
/// entirely, so the domain never learns whether the number came from C# or from SQL.
/// </para>
/// </summary>
public interface IGeoDistanceCalculator
{
    /// <summary>A name for diagnostics and for the capability report.</summary>
    string Name { get; }

    /// <summary>The great-circle distance between two points.</summary>
    Distance Between(GeoCoordinate origin, GeoCoordinate destination);

    /// <summary>
    /// The smallest box guaranteed to contain every point within
    /// <paramref name="radius"/> of <paramref name="center"/>.
    /// <para>
    /// The coarse filter every radius query starts with. It must never be too small — a box
    /// that clips the circle silently drops results — so implementations round outward.
    /// </para>
    /// </summary>
    BoundingBox BoundingBoxAround(GeoCoordinate center, Distance radius);
}
