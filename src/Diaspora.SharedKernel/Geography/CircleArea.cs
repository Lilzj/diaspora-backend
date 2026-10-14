namespace Diaspora.SharedKernel.Geography;

/// <summary>
/// A circle on the earth's surface: a centre and a radius, with the rectangle that encloses it.
/// <para>
/// In the shared kernel rather than in Location so a module can measure a circle without depending
/// on the module that is retiring its place catalogue (14 Oct 2026): it is arithmetic, not geography
/// anybody owns.
/// </para>
/// <para>
/// <b>The rectangle is the indexed prefilter, the haversine is the answer.</b> Both are computed
/// here, once, so every vertical draws the same circle. The rectangle is clamped to ±90° and ±180°
/// rather than wrapped: a circle that crosses the antimeridian loses the part beyond it, which
/// affects a handful of Pacific islands and is stated rather than handled.
/// </para>
/// </summary>
public sealed record CircleArea
{
    /// <summary>Mean earth radius in kilometres, as <c>HaversineDistanceCalculator</c> uses.</summary>
    private const double EarthRadiusKilometres = 6371d;

    private const double KilometresPerDegreeLatitude = 111.32d;

    private CircleArea() { }

    public double Latitude { get; private init; }

    public double Longitude { get; private init; }

    public double RadiusKm { get; private init; }

    public double South { get; private init; }

    public double North { get; private init; }

    public double West { get; private init; }

    public double East { get; private init; }

    /// <summary>
    /// The circle of <paramref name="radiusKm"/> around a point. The caller validates the point and
    /// clamps the radius; this only rejects what cannot be a circle at all.
    /// </summary>
    public static CircleArea Around(double latitude, double longitude, double radiusKm)
    {
        if (double.IsNaN(latitude) || latitude is < -90d or > 90d)
        {
            throw new ArgumentOutOfRangeException(nameof(latitude));
        }

        if (double.IsNaN(longitude) || longitude is < -180d or > 180d)
        {
            throw new ArgumentOutOfRangeException(nameof(longitude));
        }

        if (double.IsNaN(radiusKm) || radiusKm <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(radiusKm));
        }

        var latitudeSpan = radiusKm / KilometresPerDegreeLatitude;

        // Longitude degrees shrink toward the poles. Floored so the span cannot run away there;
        // over-reaching is harmless because the exact test follows.
        var shrink = Math.Max(Math.Cos(ToRadians(Math.Min(Math.Abs(latitude) + latitudeSpan, 90d))), 0.01d);
        var longitudeSpan = latitudeSpan / shrink;

        return new CircleArea
        {
            Latitude = latitude,
            Longitude = longitude,
            RadiusKm = radiusKm,
            South = Math.Max(latitude - latitudeSpan, -90d),
            North = Math.Min(latitude + latitudeSpan, 90d),
            West = Math.Max(longitude - longitudeSpan, -180d),
            East = Math.Min(longitude + longitudeSpan, 180d)
        };
    }

    /// <summary>Whether a point lies within the circle. A point with no coordinates never does.</summary>
    public bool Contains(double? latitude, double? longitude) =>
        latitude is { } lat && longitude is { } lng && DistanceKm(lat, lng) <= RadiusKm;

    /// <summary>The great-circle distance from the centre, in kilometres.</summary>
    public double DistanceKm(double latitude, double longitude) =>
        Haversine(Latitude, Longitude, latitude, longitude);

    /// <summary>The great-circle distance between two points, in kilometres.</summary>
    public static double Haversine(double fromLatitude, double fromLongitude, double toLatitude, double toLongitude)
    {
        if (fromLatitude == toLatitude && fromLongitude == toLongitude)
        {
            return 0d;
        }

        var deltaLatitude = ToRadians(toLatitude - fromLatitude);
        var deltaLongitude = ToRadians(toLongitude - fromLongitude);

        var a = (Math.Sin(deltaLatitude / 2d) * Math.Sin(deltaLatitude / 2d))
            + (Math.Cos(ToRadians(fromLatitude)) * Math.Cos(ToRadians(toLatitude))
                * Math.Sin(deltaLongitude / 2d) * Math.Sin(deltaLongitude / 2d));

        return EarthRadiusKilometres * 2d * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1d - a));
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180d;
}
