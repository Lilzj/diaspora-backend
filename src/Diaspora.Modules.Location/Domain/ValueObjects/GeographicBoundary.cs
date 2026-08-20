using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Location.Domain.ValueObjects;

/// <summary>
/// The outline of an administrative area: a closed simple polygon plus its bounding box.
/// <para>
/// Stored as an ordered ring of coordinates rather than as a provider-specific geometry.
/// That is the decision that keeps PostGIS optional: a boundary is data the domain fully
/// understands, and a spatial provider that has native geometry support may project this
/// into its own type at the persistence edge without the domain knowing. Nothing here
/// references a GIS library.
/// </para>
/// <para>
/// The bounding box is derived, never supplied, so a polygon and its extent cannot drift
/// apart. Point-in-polygon uses the ray-casting rule, which is exact for simple polygons
/// and costs nothing to carry.
/// </para>
/// </summary>
public sealed class GeographicBoundary : ValueObject
{
    /// <summary>The minimum ring size. Fewer than three points encloses no area.</summary>
    public const int MinimumVertices = 3;

    /// <summary>
    /// An upper bound on ring size. Real administrative outlines simplified for mobile
    /// rendering sit well under this; anything larger is an unsimplified import that would
    /// make every containment test a scan of thousands of segments.
    /// </summary>
    public const int MaximumVertices = 2000;

    private readonly GeoCoordinate[] _vertices;

    /// <summary>The ring, in order. Not closed — the last vertex implicitly joins the first.</summary>
    public IReadOnlyList<GeoCoordinate> Vertices => _vertices;

    /// <summary>The derived extent. Always consistent with <see cref="Vertices"/>.</summary>
    public BoundingBox Extent { get; }

    private GeographicBoundary(GeoCoordinate[] vertices, BoundingBox extent)
    {
        _vertices = vertices;
        Extent = extent;
    }

    public static Result<GeographicBoundary> TryCreate(IReadOnlyList<GeoCoordinate> vertices)
    {
        ArgumentNullException.ThrowIfNull(vertices);

        if (vertices.Count < MinimumVertices)
        {
            return Result.Failure<GeographicBoundary>(Error.Validation(
                LocationErrorCodes.PolygonInvalid,
                $"A boundary needs at least {MinimumVertices} points."));
        }

        if (vertices.Count > MaximumVertices)
        {
            return Result.Failure<GeographicBoundary>(Error.Validation(
                LocationErrorCodes.PolygonInvalid,
                $"A boundary may not exceed {MaximumVertices} points. Simplify the outline first."));
        }

        // A ring that repeats its first point at the end is the GeoJSON convention. Accepted
        // and normalised away, so callers may pass either form.
        var ring = vertices.ToArray();

        if (ring.Length > MinimumVertices && ring[0] == ring[^1])
        {
            ring = ring[..^1];
        }

        var extent = BoundingBox.Enclosing(ring);

        return extent.IsFailure
            ? Result.Failure<GeographicBoundary>(extent.Error)
            : Result.Success(new GeographicBoundary(ring, extent.Value));
    }

    /// <summary>
    /// Whether the point lies inside the ring, by the even-odd ray-casting rule.
    /// <para>
    /// The bounding-box test first is not an optimisation so much as the common path: a
    /// query has already narrowed candidates to the extent, and the ring walk only decides
    /// the fringe.
    /// </para>
    /// </summary>
    public bool Contains(GeoCoordinate point)
    {
        ArgumentNullException.ThrowIfNull(point);

        if (!Extent.Contains(point))
        {
            return false;
        }

        var inside = false;

        for (int current = 0, previous = _vertices.Length - 1;
             current < _vertices.Length;
             previous = current++)
        {
            var a = _vertices[current];
            var b = _vertices[previous];

            var straddles = a.Latitude > point.Latitude != b.Latitude > point.Latitude;

            if (!straddles)
            {
                continue;
            }

            var crossingLongitude =
                ((b.Longitude - a.Longitude) * (point.Latitude - a.Latitude) /
                 (b.Latitude - a.Latitude)) + a.Longitude;

            if (point.Longitude < crossingLongitude)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        foreach (var vertex in _vertices)
        {
            yield return vertex;
        }
    }
}
