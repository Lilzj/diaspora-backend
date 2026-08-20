using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Location.Domain.ValueObjects;

/// <summary>
/// An axis-aligned latitude/longitude rectangle.
/// <para>
/// The workhorse of every spatial query in the module. A bounding box is the one geographic
/// predicate every relational database can answer with a plain composite index — four
/// inequalities on two columns — which is what lets the default provider serve radius,
/// nearest and viewport queries at speed on PostgreSQL, SQL Server or SQLite with no spatial
/// extension installed at all. The box is the coarse filter; exact distance refines it.
/// </para>
/// <para>
/// <b>The antimeridian.</b> A box spanning 180° has <c>West &gt; East</c>, and the naive
/// <c>west &lt;= x &lt;= east</c> test silently returns nothing for every point in it. Fiji
/// and Kiribati are real places with real members, so <see cref="Contains"/> handles the
/// wrap and <see cref="CrossesAntimeridian"/> lets a query provider split the box into two
/// index-friendly halves rather than discovering the problem in production.
/// </para>
/// </summary>
public sealed class BoundingBox : ValueObject
{
    /// <summary>Northernmost latitude.</summary>
    public double North { get; }

    /// <summary>Southernmost latitude.</summary>
    public double South { get; }

    /// <summary>Easternmost longitude.</summary>
    public double East { get; }

    /// <summary>Westernmost longitude.</summary>
    public double West { get; }

    private BoundingBox(double north, double south, double east, double west)
    {
        North = north;
        South = south;
        East = east;
        West = west;
    }

    public static Result<BoundingBox> TryCreate(double north, double south, double east, double west)
    {
        if (double.IsNaN(north) || double.IsNaN(south) || double.IsNaN(east) || double.IsNaN(west) ||
            double.IsInfinity(north) || double.IsInfinity(south) ||
            double.IsInfinity(east) || double.IsInfinity(west))
        {
            return Result.Failure<BoundingBox>(Error.Validation(
                LocationErrorCodes.BoundsInvalid, "Bounds must be finite numbers."));
        }

        if (north is < GeoCoordinate.MinimumLatitude or > GeoCoordinate.MaximumLatitude ||
            south is < GeoCoordinate.MinimumLatitude or > GeoCoordinate.MaximumLatitude)
        {
            return Result.Failure<BoundingBox>(Error.Validation(
                LocationErrorCodes.BoundsInvalid, "Latitudes must be between -90 and 90."));
        }

        if (east is < GeoCoordinate.MinimumLongitude or > GeoCoordinate.MaximumLongitude ||
            west is < GeoCoordinate.MinimumLongitude or > GeoCoordinate.MaximumLongitude)
        {
            return Result.Failure<BoundingBox>(Error.Validation(
                LocationErrorCodes.BoundsInvalid, "Longitudes must be between -180 and 180."));
        }

        // Latitude cannot wrap — there is no pole-crossing rectangle — so an inverted pair
        // here is always a caller mistake rather than a legitimate shape.
        if (north < south)
        {
            return Result.Failure<BoundingBox>(Error.Validation(
                LocationErrorCodes.BoundsInvalid,
                "The northern bound must not be south of the southern bound."));
        }

        return Result.Success(new BoundingBox(north, south, east, west));
    }

    public static BoundingBox Create(double north, double south, double east, double west)
    {
        var result = TryCreate(north, south, east, west);

        return result.IsSuccess
            ? result.Value
            : throw new ArgumentException(result.Error.Description, nameof(north));
    }

    /// <summary>
    /// Parses the wire form a map viewport travels in: <c>north,south,east,west</c>.
    /// <para>
    /// <b>One parser, in the module that owns geography, rather than one per vertical.</b> Six
    /// discovery routes accept a viewport and none of them should be parsing a coordinate string:
    /// a vertical that wrote its own would also have to decide what <c>west &gt; east</c> means,
    /// and the answer — the box crosses ±180° — is the kind of thing five of the six would get
    /// wrong and nobody would notice until somebody in Fiji opened a map.
    /// </para>
    /// <para>
    /// Invariant culture, deliberately: a client in a comma-decimal locale that sent
    /// <c>51,5;-0,1</c> would be parsing its own numbers, and the wire is not a locale.
    /// </para>
    /// </summary>
    public static Result<BoundingBox> TryParse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.Failure<BoundingBox>(Error.Validation(
                LocationErrorCodes.BoundsInvalid, "Bounds are required."));
        }

        var parts = value.Split(',', StringSplitOptions.TrimEntries);

        if (parts.Length != 4)
        {
            return Result.Failure<BoundingBox>(Error.Validation(
                LocationErrorCodes.BoundsInvalid,
                "Bounds must be four comma-separated numbers: north,south,east,west."));
        }

        var parsed = new double[4];

        for (var index = 0; index < 4; index++)
        {
            if (!double.TryParse(
                    parts[index],
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out parsed[index]))
            {
                return Result.Failure<BoundingBox>(Error.Validation(
                    LocationErrorCodes.BoundsInvalid,
                    "Bounds must be four comma-separated numbers: north,south,east,west."));
            }
        }

        return TryCreate(parsed[0], parsed[1], parsed[2], parsed[3]);
    }

    /// <summary>The whole planet. The identity element for <see cref="Union"/>.</summary>
    public static BoundingBox World => new(90d, -90d, 180d, -180d);

    /// <summary>
    /// The smallest box containing every supplied point.
    /// <para>
    /// Used to derive an area's extent from its boundary polygon, so a polygon and its box
    /// cannot disagree.
    /// </para>
    /// </summary>
    public static Result<BoundingBox> Enclosing(IReadOnlyCollection<GeoCoordinate> points)
    {
        ArgumentNullException.ThrowIfNull(points);

        if (points.Count == 0)
        {
            return Result.Failure<BoundingBox>(Error.Validation(
                LocationErrorCodes.BoundsInvalid, "At least one point is required."));
        }

        var north = points.Max(point => point.Latitude);
        var south = points.Min(point => point.Latitude);
        var east = points.Max(point => point.Longitude);
        var west = points.Min(point => point.Longitude);

        return TryCreate(north, south, east, west);
    }

    /// <summary>
    /// True when the box wraps across ±180°, which it does exactly when its western edge is
    /// numerically east of its eastern edge.
    /// </summary>
    public bool CrossesAntimeridian => West > East;

    public bool Contains(GeoCoordinate coordinate)
    {
        ArgumentNullException.ThrowIfNull(coordinate);

        var withinLatitude = coordinate.Latitude <= North && coordinate.Latitude >= South;

        if (!withinLatitude)
        {
            return false;
        }

        return CrossesAntimeridian
            ? coordinate.Longitude >= West || coordinate.Longitude <= East
            : coordinate.Longitude >= West && coordinate.Longitude <= East;
    }

    /// <summary>
    /// Splits an antimeridian-crossing box into the two ordinary boxes it is the union of.
    /// <para>
    /// A query provider calls this so its <c>WHERE</c> clause stays two plain range
    /// predicates joined by <c>OR</c> — still index-eligible — rather than a modular
    /// arithmetic expression no planner will use an index for.
    /// </para>
    /// </summary>
    public IReadOnlyList<BoundingBox> Split() =>
        CrossesAntimeridian
            ? [new BoundingBox(North, South, 180d, West), new BoundingBox(North, South, East, -180d)]
            : [this];

    /// <summary>The smallest box containing both. Falls back to the world when either wraps.</summary>
    public BoundingBox Union(BoundingBox other)
    {
        ArgumentNullException.ThrowIfNull(other);

        // Unioning wrapped boxes correctly requires interval arithmetic on a circle, and a
        // wrong answer here would silently shrink a search area. Widening to the world is
        // always correct, and the case does not arise for real administrative geography.
        if (CrossesAntimeridian || other.CrossesAntimeridian)
        {
            return World;
        }

        return new BoundingBox(
            Math.Max(North, other.North),
            Math.Min(South, other.South),
            Math.Max(East, other.East),
            Math.Min(West, other.West));
    }

    /// <summary>The box's centre point.</summary>
    public GeoCoordinate Center =>
        GeoCoordinate.Create(
            (North + South) / 2d,
            CrossesAntimeridian
                ? NormalizeLongitude((West + East + 360d) / 2d)
                : (West + East) / 2d);

    private static double NormalizeLongitude(double longitude)
    {
        var wrapped = (longitude + 180d) % 360d;

        if (wrapped < 0)
        {
            wrapped += 360d;
        }

        return wrapped - 180d;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return North;
        yield return South;
        yield return East;
        yield return West;
    }

    public override string ToString() =>
        string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"N{North:F5} S{South:F5} E{East:F5} W{West:F5}");
}
