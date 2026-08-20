using Diaspora.Modules.Location.Domain.ValueObjects;

namespace Diaspora.Modules.Location.Domain.Spatial;

/// <summary>
/// Great-circle distance on a sphere, by the haversine formula.
/// <para>
/// The platform default, and chosen to match the frontend exactly. <c>src/utils/geo.ts</c>
/// already computes distances this way with an earth radius of 6371 km, and its output is
/// rendered on every listing card as "1.2 km away". A server that used a different radius or
/// a different formula would produce a subtly different number for the same two points, and
/// the member would see the distance change when a screen refreshed from the API instead of
/// from the cache. The constant below is asserted equal to the client's by test.
/// </para>
/// <para>
/// Haversine is numerically well-conditioned for the short distances this platform actually
/// asks about — the naive spherical law of cosines loses precision under a kilometre, which
/// is exactly the range the "In Your Neighborhood" badge lives in.
/// </para>
/// </summary>
public sealed class HaversineDistanceCalculator : IGeoDistanceCalculator
{
    /// <summary>
    /// Mean earth radius in kilometres. Identical to the frontend's <c>const R = 6371</c>.
    /// </summary>
    public const double EarthRadiusKilometers = 6371d;

    public string Name => "haversine";

    public Distance Between(GeoCoordinate origin, GeoCoordinate destination)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(destination);

        // The client short-circuits identical points; matching that keeps the two
        // implementations bit-identical at zero rather than merely close.
        if (origin.Latitude == destination.Latitude && origin.Longitude == destination.Longitude)
        {
            return Distance.Zero;
        }

        var deltaLatitude = ToRadians(destination.Latitude - origin.Latitude);
        var deltaLongitude = ToRadians(destination.Longitude - origin.Longitude);

        var a = (Math.Sin(deltaLatitude / 2d) * Math.Sin(deltaLatitude / 2d)) +
                (Math.Cos(ToRadians(origin.Latitude)) *
                 Math.Cos(ToRadians(destination.Latitude)) *
                 Math.Sin(deltaLongitude / 2d) * Math.Sin(deltaLongitude / 2d));

        var c = 2d * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1d - a));

        return Distance.FromKilometers(EarthRadiusKilometers * c);
    }

    /// <summary>
    /// The enclosing box for a radius, widened by one part in a thousand.
    /// <para>
    /// The widening is not superstition. The box is computed from the circle's tangents, and
    /// double-precision rounding can put a point that is exactly on the circle a nanometre
    /// outside the box — which drops it before the exact distance test ever runs. Rounding
    /// outward makes the coarse filter conservative, which is the only direction it is
    /// allowed to be wrong in.
    /// </para>
    /// </summary>
    public BoundingBox BoundingBoxAround(GeoCoordinate center, Distance radius)
    {
        ArgumentNullException.ThrowIfNull(center);
        ArgumentNullException.ThrowIfNull(radius);

        var angular = radius.Kilometers / EarthRadiusKilometers * 1.001d;

        var latitudeDelta = ToDegrees(angular);

        var north = Math.Min(GeoCoordinate.MaximumLatitude, center.Latitude + latitudeDelta);
        var south = Math.Max(GeoCoordinate.MinimumLatitude, center.Latitude - latitudeDelta);

        // A degree of longitude narrows toward the poles. When the circle reaches far enough
        // that it encloses a pole, every longitude is inside it and the only correct box is
        // the full band. The test for that is sin(angular) >= cos(latitude): at or past
        // equality the longitude offset would be a quarter turn or more, which means the
        // circle has wrapped the pole rather than being a lune around the centre.
        var cosine = Math.Cos(ToRadians(center.Latitude));

        if (cosine <= 1e-9 || Math.Sin(angular) >= cosine)
        {
            return BoundingBox.Create(
                north, south, GeoCoordinate.MaximumLongitude, GeoCoordinate.MinimumLongitude);
        }

        var longitudeDelta = ToDegrees(Math.Asin(Math.Sin(angular) / cosine));

        if (double.IsNaN(longitudeDelta) || longitudeDelta >= 180d)
        {
            return BoundingBox.Create(
                north, south, GeoCoordinate.MaximumLongitude, GeoCoordinate.MinimumLongitude);
        }

        var east = Normalize(center.Longitude + longitudeDelta);
        var west = Normalize(center.Longitude - longitudeDelta);

        return BoundingBox.Create(north, south, east, west);
    }

    private static double Normalize(double longitude)
    {
        var wrapped = (longitude + 180d) % 360d;

        if (wrapped < 0d)
        {
            wrapped += 360d;
        }

        return wrapped - 180d;
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180d;

    private static double ToDegrees(double radians) => radians * 180d / Math.PI;
}
