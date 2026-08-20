using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Location.Domain.ValueObjects;

/// <summary>
/// A point on the Earth's surface: latitude and longitude in decimal degrees, WGS 84.
/// <para>
/// The platform already ships <c>SharedKernel.ValueObjects.Coordinates</c>, and this is
/// deliberately not that type. <c>Coordinates</c> is a validated pair with no geographic
/// behaviour; the Location domain needs a point that knows how to be compared, offset,
/// bounded and rounded, and putting that behaviour into the SharedKernel would push
/// geography into a project every module references. Conversion in both directions is
/// available for interoperability, so a module holding a <c>Coordinates</c> loses nothing.
/// </para>
/// <para>
/// Distance is deliberately absent from this type. Two points do not know how far apart they
/// are — that depends on the earth model in use, which is
/// <see cref="Spatial.IGeoDistanceCalculator"/>'s business precisely so a future switch from
/// spherical to ellipsoidal maths touches one class rather than every call site.
/// </para>
/// </summary>
public sealed class GeoCoordinate : ValueObject
{
    /// <summary>Decimal places retained. Roughly 1 cm — far past any source's accuracy.</summary>
    private const int StoragePrecision = 7;

    /// <summary>Decimal places retained under <see cref="CoordinatePrecision.Approximate"/>.</summary>
    private const int ApproximatePrecision = 2;

    public const double MinimumLatitude = -90d;
    public const double MaximumLatitude = 90d;
    public const double MinimumLongitude = -180d;
    public const double MaximumLongitude = 180d;

    public double Latitude { get; }

    public double Longitude { get; }

    private GeoCoordinate(double latitude, double longitude)
    {
        Latitude = latitude;
        Longitude = longitude;
    }

    /// <summary>
    /// Creates a coordinate, or fails if either component is out of range or not a number.
    /// <para>
    /// A <see cref="Result{T}"/> rather than an exception because coordinates arrive from the
    /// network on nearly every request in this module. An out-of-range latitude is a bad
    /// request, not a bug, and it deserves a problem document rather than a stack trace.
    /// </para>
    /// </summary>
    public static SharedKernel.Results.Result<GeoCoordinate> TryCreate(double latitude, double longitude)
    {
        if (double.IsNaN(latitude) || double.IsInfinity(latitude) ||
            double.IsNaN(longitude) || double.IsInfinity(longitude))
        {
            return SharedKernel.Results.Result.Failure<GeoCoordinate>(
                SharedKernel.Results.Error.Validation(
                    LocationErrorCodes.CoordinatesInvalid,
                    "Latitude and longitude must be finite numbers."));
        }

        if (latitude is < MinimumLatitude or > MaximumLatitude)
        {
            return SharedKernel.Results.Result.Failure<GeoCoordinate>(
                SharedKernel.Results.Error.Validation(
                    LocationErrorCodes.CoordinatesInvalid,
                    $"Latitude must be between {MinimumLatitude} and {MaximumLatitude}."));
        }

        if (longitude is < MinimumLongitude or > MaximumLongitude)
        {
            return SharedKernel.Results.Result.Failure<GeoCoordinate>(
                SharedKernel.Results.Error.Validation(
                    LocationErrorCodes.CoordinatesInvalid,
                    $"Longitude must be between {MinimumLongitude} and {MaximumLongitude}."));
        }

        return SharedKernel.Results.Result.Success(
            new GeoCoordinate(
                Math.Round(latitude, StoragePrecision),
                Math.Round(longitude, StoragePrecision)));
    }

    /// <summary>
    /// Creates a coordinate, throwing on invalid input. For seed data and tests, where an
    /// invalid literal is a programming error rather than a bad request.
    /// </summary>
    public static GeoCoordinate Create(double latitude, double longitude)
    {
        var result = TryCreate(latitude, longitude);

        return result.IsSuccess
            ? result.Value
            : throw new ArgumentOutOfRangeException(
                nameof(latitude), result.Error.Description);
    }

    /// <summary>Adapts from the SharedKernel's coordinate pair.</summary>
    public static GeoCoordinate From(SharedKernel.ValueObjects.Coordinates coordinates)
    {
        ArgumentNullException.ThrowIfNull(coordinates);
        return Create(coordinates.Latitude, coordinates.Longitude);
    }

    /// <summary>Adapts to the SharedKernel's coordinate pair, for modules that hold that type.</summary>
    public SharedKernel.ValueObjects.Coordinates ToSharedKernel() =>
        SharedKernel.ValueObjects.Coordinates.Create(Latitude, Longitude);

    /// <summary>
    /// This coordinate at the requested disclosure precision.
    /// <para>
    /// Applied at the mapping boundary on every response that carries a member's own
    /// position. Rounding rather than jittering because rounding is stable: a member polled
    /// repeatedly at <see cref="CoordinatePrecision.Approximate"/> yields the same value
    /// every time, so their true position cannot be averaged out of the noise.
    /// </para>
    /// </summary>
    public GeoCoordinate AtPrecision(CoordinatePrecision precision) => precision switch
    {
        CoordinatePrecision.Approximate => new GeoCoordinate(
            Math.Round(Latitude, ApproximatePrecision),
            Math.Round(Longitude, ApproximatePrecision)),
        _ => this
    };

    /// <summary>
    /// Whether this point lies inside <paramref name="box"/>, antimeridian included.
    /// </summary>
    public bool IsInside(BoundingBox box)
    {
        ArgumentNullException.ThrowIfNull(box);
        return box.Contains(this);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Latitude;
        yield return Longitude;
    }

    public override string ToString() =>
        string.Create(
            System.Globalization.CultureInfo.InvariantCulture, $"{Latitude:F7},{Longitude:F7}");
}
