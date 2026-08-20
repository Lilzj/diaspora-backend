using Diaspora.Modules.Location.Application.Services;
using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.Modules.Location.Domain.Spatial;
using Diaspora.Modules.Location.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Location;

/// <summary>
/// Distance, and its agreement with the client.
/// <para>
/// The frontend computes distances locally in <c>src/utils/geo.ts</c> and renders them on
/// every listing card. The server computes them too. Two implementations of the same figure is
/// a fact of the product — a round trip to render a badge would be absurd — so agreement
/// between them is a requirement, and this suite is what enforces it.
/// </para>
/// </summary>
public sealed class DistanceCalculationTests
{
    private readonly HaversineDistanceCalculator _calculator = new();

    /// <summary>
    /// The client's <c>const R = 6371</c>. A different radius here would make every distance
    /// on the platform disagree with itself by about a tenth of a percent — small enough to
    /// pass review, large enough to flip a "In Your Neighborhood" badge on and off.
    /// </summary>
    [Fact]
    public void The_earth_radius_matches_the_frontends()
    {
        HaversineDistanceCalculator.EarthRadiusKilometers.Should().Be(6371d);
    }

    /// <summary>The client short-circuits identical points; matching keeps the two bit-identical at zero.</summary>
    [Fact]
    public void The_distance_between_a_point_and_itself_is_exactly_zero()
    {
        var peckham = GeoCoordinate.Create(51.4742d, -0.0691d);

        _calculator.Between(peckham, peckham).Kilometers.Should().Be(0d);
    }

    /// <summary>
    /// Peckham to Brixton, the two neighbourhoods the frontend's fixtures use most. About
    /// 3.4 km by great circle.
    /// </summary>
    [Fact]
    public void Two_london_neighbourhoods_are_a_few_kilometres_apart()
    {
        var peckham = GeoCoordinate.Create(51.4742d, -0.0691d);
        var brixton = GeoCoordinate.Create(51.4624d, -0.1149d);

        _calculator.Between(peckham, brixton).Kilometers.Should().BeApproximately(3.4d, 0.2d);
    }

    /// <summary>London to Port Louis — a hemisphere apart, and the case a short-distance
    /// approximation would get badly wrong.</summary>
    [Fact]
    public void Two_continents_apart_is_thousands_of_kilometres()
    {
        var london = GeoCoordinate.Create(51.5074d, -0.1278d);
        var portLouis = GeoCoordinate.Create(-20.1609d, 57.5012d);

        _calculator.Between(london, portLouis).Kilometers.Should().BeApproximately(9700d, 200d);
    }

    [Fact]
    public void Distance_is_symmetric()
    {
        var a = GeoCoordinate.Create(51.4742d, -0.0691d);
        var b = GeoCoordinate.Create(-20.2417d, 57.4806d);

        _calculator.Between(a, b).Kilometers
            .Should().BeApproximately(_calculator.Between(b, a).Kilometers, 0.000001d);
    }

    // ── The bounding box around a radius ─────────────────────────────────

    /// <summary>
    /// The coarse filter must never be too small. A box that clips the circle drops results
    /// silently, which is the one failure mode a spatial search cannot recover from.
    /// </summary>
    [Fact]
    public void The_enclosing_box_never_clips_the_circle()
    {
        var centre = GeoCoordinate.Create(51.4742d, -0.0691d);
        var radius = Distance.FromKilometers(5d);
        var box = _calculator.BoundingBoxAround(centre, radius);

        // Sixteen points exactly on the circle, by bearing. Every one has to be inside.
        for (var bearing = 0; bearing < 360; bearing += 22)
        {
            var point = PointAtBearing(centre, radius, bearing);
            box.Contains(point).Should().BeTrue(
                $"a point at bearing {bearing}° on the circle must survive the coarse filter");
        }
    }

    /// <summary>
    /// Near the poles a degree of longitude collapses, and the enclosing box has to widen to
    /// the whole band rather than producing a sliver that excludes most of the circle.
    /// </summary>
    [Fact]
    public void A_polar_radius_widens_to_every_longitude()
    {
        var northPole = GeoCoordinate.Create(89.99d, 0d);
        var box = _calculator.BoundingBoxAround(northPole, Distance.FromKilometers(50d));

        box.East.Should().Be(180d);
        box.West.Should().Be(-180d);
    }

    // ── Presentation, against the client's own formatting ────────────────

    /// <summary>Matches <c>formatDistance</c> in <c>src/utils/geo.ts</c>, case for case.</summary>
    [Theory]
    [InlineData(0.05d, "In Your Neighborhood")]
    [InlineData(0.5d, "500m away")]
    [InlineData(1.2d, "1.2 km away")]
    [InlineData(12.34d, "12.3 km away")]
    public void Formatting_matches_the_frontends(double kilometers, string expected)
    {
        DistancePresentation.Format(Distance.FromKilometers(kilometers)).Should().Be(expected);
    }

    /// <summary>
    /// The client's commute constants: walk 5 km/h, transit 20 km/h + 5 min, drive 40 km/h
    /// + 2 min. A member must not see one estimate on a cached card and another after a
    /// refresh.
    /// </summary>
    [Fact]
    public void The_commute_constants_match_the_frontends()
    {
        DistancePresentation.WalkingSpeedKph.Should().Be(5d);
        DistancePresentation.TransitSpeedKph.Should().Be(20d);
        DistancePresentation.TransitBufferMinutes.Should().Be(5);
        DistancePresentation.DrivingSpeedKph.Should().Be(40d);
        DistancePresentation.DrivingBufferMinutes.Should().Be(2);
    }

    [Theory]
    [InlineData(5d, CommuteMode.Walk, 60)]
    [InlineData(10d, CommuteMode.Drive, 17)]
    [InlineData(10d, CommuteMode.Transit, 35)]
    public void Commute_estimates_match_the_frontends(
        double kilometers, CommuteMode mode, int expectedMinutes)
    {
        DistancePresentation.CommuteMinutes(Distance.FromKilometers(kilometers), mode)
            .Should().Be(expectedMinutes);
    }

    /// <summary>The client renders "1 min" rather than "0 mins" for a very short hop.</summary>
    [Fact]
    public void A_very_short_journey_is_reported_as_one_minute_not_zero()
    {
        DistancePresentation
            .CommuteMinutes(Distance.FromKilometers(0.001d), CommuteMode.Walk)
            .Should().Be(1);
    }

    // ── Units ────────────────────────────────────────────────────────────

    [Fact]
    public void Miles_convert_by_the_international_definition()
    {
        Distance.FromKilometers(1.609344d).Miles.Should().BeApproximately(1d, 0.000001d);
        Distance.FromMiles(1d).Kilometers.Should().BeApproximately(1.609344d, 0.000001d);
    }

    [Fact]
    public void A_negative_distance_is_rejected()
    {
        var act = () => Distance.FromKilometers(-1d);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    /// <summary>
    /// Ordering and equality have to agree. Distance implements both, and an asymmetry between
    /// them makes every sort in the module unstable.
    /// </summary>
    [Fact]
    public void Ordering_and_equality_agree()
    {
        var a = Distance.FromKilometers(5d);
        var b = Distance.FromKilometers(5d);
        var c = Distance.FromKilometers(6d);

        (a == b).Should().BeTrue();
        (a <= b).Should().BeTrue();
        (a >= b).Should().BeTrue();
        (a < c).Should().BeTrue();
        (c > a).Should().BeTrue();
    }

    /// <summary>
    /// Projects a point the given distance from the centre along a bearing, so the
    /// bounding-box test can place points exactly on the circle rather than near it.
    /// </summary>
    private static GeoCoordinate PointAtBearing(
        GeoCoordinate centre, Distance distance, double bearingDegrees)
    {
        const double earthRadius = HaversineDistanceCalculator.EarthRadiusKilometers;

        var angular = distance.Kilometers / earthRadius;
        var bearing = bearingDegrees * Math.PI / 180d;
        var latitude = centre.Latitude * Math.PI / 180d;
        var longitude = centre.Longitude * Math.PI / 180d;

        var destinationLatitude = Math.Asin(
            (Math.Sin(latitude) * Math.Cos(angular)) +
            (Math.Cos(latitude) * Math.Sin(angular) * Math.Cos(bearing)));

        var destinationLongitude = longitude + Math.Atan2(
            Math.Sin(bearing) * Math.Sin(angular) * Math.Cos(latitude),
            Math.Cos(angular) - (Math.Sin(latitude) * Math.Sin(destinationLatitude)));

        return GeoCoordinate.Create(
            destinationLatitude * 180d / Math.PI,
            destinationLongitude * 180d / Math.PI);
    }
}
