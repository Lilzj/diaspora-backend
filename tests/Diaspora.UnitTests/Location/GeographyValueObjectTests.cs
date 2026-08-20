using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.Modules.Location.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Location;

/// <summary>
/// The geographic value objects, and the edge cases that are wrong everywhere until someone
/// writes a test for them.
/// </summary>
public sealed class GeographyValueObjectTests
{
    // ── Coordinates ──────────────────────────────────────────────────────

    [Theory]
    [InlineData(91d, 0d)]
    [InlineData(-91d, 0d)]
    [InlineData(0d, 181d)]
    [InlineData(0d, -181d)]
    [InlineData(double.NaN, 0d)]
    [InlineData(0d, double.PositiveInfinity)]
    public void Out_of_range_coordinates_are_rejected(double latitude, double longitude)
    {
        var result = GeoCoordinate.TryCreate(latitude, longitude);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("location.coordinates.invalid");
    }

    [Theory]
    [InlineData(90d, 180d)]
    [InlineData(-90d, -180d)]
    [InlineData(51.4742d, -0.0691d)]
    [InlineData(-20.2417d, 57.4806d)]
    public void The_extremes_and_the_launch_cities_are_accepted(double latitude, double longitude)
    {
        GeoCoordinate.TryCreate(latitude, longitude).IsSuccess.Should().BeTrue();
    }

    /// <summary>
    /// Approximate precision must be stable, not jittered. A member polled repeatedly has to
    /// yield the same rounded value every time, or their true position can be averaged out of
    /// the noise.
    /// </summary>
    [Fact]
    public void Approximate_precision_is_stable_across_repeated_reads()
    {
        var exact = GeoCoordinate.Create(51.474215d, -0.069133d);

        var first = exact.AtPrecision(CoordinatePrecision.Approximate);
        var second = exact.AtPrecision(CoordinatePrecision.Approximate);

        first.Should().Be(second);
        first.Latitude.Should().Be(51.47d);
        first.Longitude.Should().Be(-0.07d);
    }

    [Fact]
    public void Exact_precision_leaves_a_coordinate_untouched()
    {
        var exact = GeoCoordinate.Create(51.474215d, -0.069133d);

        exact.AtPrecision(CoordinatePrecision.Exact).Should().BeSameAs(exact);
    }

    // ── Bounding boxes ───────────────────────────────────────────────────

    [Fact]
    public void A_box_contains_a_point_inside_it()
    {
        var box = BoundingBox.Create(52d, 51d, 1d, -1d);

        box.Contains(GeoCoordinate.Create(51.5d, 0d)).Should().BeTrue();
        box.Contains(GeoCoordinate.Create(53d, 0d)).Should().BeFalse();
        box.Contains(GeoCoordinate.Create(51.5d, 2d)).Should().BeFalse();
    }

    /// <summary>
    /// The antimeridian case. Fiji spans 180°, and the naive west &lt;= x &lt;= east test
    /// returns nothing for every point in it — silently, which is the worst kind of wrong.
    /// </summary>
    [Fact]
    public void A_box_crossing_the_antimeridian_contains_points_on_both_sides()
    {
        var fiji = BoundingBox.Create(-16d, -19d, -178d, 177d);

        fiji.CrossesAntimeridian.Should().BeTrue();
        fiji.Contains(GeoCoordinate.Create(-18d, 179d)).Should().BeTrue();
        fiji.Contains(GeoCoordinate.Create(-18d, -179d)).Should().BeTrue();
        fiji.Contains(GeoCoordinate.Create(-18d, 0d)).Should().BeFalse();
    }

    [Fact]
    public void A_wrapped_box_splits_into_two_ordinary_boxes()
    {
        var fiji = BoundingBox.Create(-16d, -19d, -178d, 177d);

        var halves = fiji.Split();

        halves.Should().HaveCount(2);
        halves.Should().OnlyContain(half => !half.CrossesAntimeridian);
    }

    [Fact]
    public void An_inverted_latitude_pair_is_rejected()
    {
        BoundingBox.TryCreate(north: 10d, south: 20d, east: 1d, west: 0d)
            .IsFailure.Should().BeTrue();
    }

    // ── The viewport wire form (B4.1) ────────────────────────────────────

    /// <summary>
    /// The order is <c>north,south,east,west</c> and nothing else. Six discovery routes now take
    /// a viewport through this one parser; if it read the four numbers in a different order every
    /// map on the platform would show a box somewhere else and no request would fail.
    /// </summary>
    [Fact]
    public void A_viewport_parses_north_south_east_west_in_that_order()
    {
        var box = BoundingBox.TryParse("51.50,51.44,-0.05,-0.10");

        box.IsSuccess.Should().BeTrue();
        box.Value.North.Should().Be(51.50d);
        box.Value.South.Should().Be(51.44d);
        box.Value.East.Should().Be(-0.05d);
        box.Value.West.Should().Be(-0.10d);
    }

    /// <summary>
    /// <c>west &gt; east</c> is a box crossing ±180°, not an invalid one. A parser that refused it
    /// would make the map unusable in Fiji and Kiribati rather than merely empty.
    /// </summary>
    [Fact]
    public void A_parsed_viewport_may_cross_the_antimeridian()
    {
        var box = BoundingBox.TryParse("-16,-19,-178,177");

        box.IsSuccess.Should().BeTrue();
        box.Value.CrossesAntimeridian.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("51.5,51.4,-0.05")]
    [InlineData("51.5,51.4,-0.05,-0.1,0")]
    [InlineData("north,south,east,west")]
    [InlineData("51,5;51,4;-0,05;-0,1")]
    [InlineData("91,51.4,-0.05,-0.1")]
    [InlineData("51.4,51.5,-0.05,-0.1")]
    public void A_malformed_viewport_is_refused_rather_than_guessed_at(string? value) =>
        BoundingBox.TryParse(value).IsFailure.Should().BeTrue();

    /// <summary>
    /// Invariant culture, always. A client in a comma-decimal locale sending <c>51,5</c> would
    /// otherwise parse as two fields and land somewhere else entirely — the wire is not a locale.
    /// </summary>
    [Fact]
    public void A_viewport_is_parsed_in_the_invariant_culture()
    {
        var original = System.Globalization.CultureInfo.CurrentCulture;

        try
        {
            System.Globalization.CultureInfo.CurrentCulture =
                new System.Globalization.CultureInfo("de-DE");

            BoundingBox.TryParse("51.50,51.44,-0.05,-0.10").Value.North.Should().Be(51.50d);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void An_enclosing_box_covers_every_supplied_point()
    {
        var box = BoundingBox.Enclosing(
        [
            GeoCoordinate.Create(51.4742d, -0.0691d),
            GeoCoordinate.Create(51.5450d, -0.0553d),
            GeoCoordinate.Create(51.3762d, -0.0982d)
        ]);

        box.IsSuccess.Should().BeTrue();
        box.Value.North.Should().Be(51.5450d);
        box.Value.South.Should().Be(51.3762d);
        box.Value.West.Should().Be(-0.0982d);
    }

    // ── Codes ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("gb", "GB")]
    [InlineData("Gb", "GB")]
    [InlineData(" mu ", "MU")]
    public void A_country_code_is_normalised_to_upper_case(string input, string expected)
    {
        CountryCode.Create(input).Value.Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("G")]
    [InlineData("GBR")]
    [InlineData("G1")]
    public void A_malformed_country_code_is_rejected(string input)
    {
        CountryCode.TryCreate(input).IsFailure.Should().BeTrue();
    }

    /// <summary>
    /// The flag is derived from the code by shifting into the regional-indicator block, so
    /// there is no table to go stale.
    /// </summary>
    [Theory]
    [InlineData("GB", "🇬🇧")]
    [InlineData("MU", "🇲🇺")]
    [InlineData("NG", "🇳🇬")]
    public void A_flag_is_derived_from_the_country_code(string code, string flag)
    {
        CountryCode.Create(code).ToFlagEmoji().Should().Be(flag);
    }

    /// <summary>
    /// A language code lower-cases and a country code upper-cases, which is what makes
    /// <c>en-GB</c> read the way it does.
    /// </summary>
    [Fact]
    public void A_locale_composes_a_lower_case_language_with_an_upper_case_region()
    {
        var locale = LocaleCode.Create(LanguageCode.Create("EN"), CountryCode.Create("gb"));

        locale.Value.Should().Be("en-GB");
    }

    // ── Slugs ────────────────────────────────────────────────────────────

    /// <summary>
    /// The frontend builds its neighbourhood links as
    /// <c>name.toLowerCase().replace(/\s+/g, '-')</c>. The server has to resolve exactly that.
    /// </summary>
    [Theory]
    [InlineData("Peckham", "peckham")]
    [InlineData("Rose Hill Central", "rose-hill-central")]
    [InlineData("Quatre Bornes Center", "quatre-bornes-center")]
    [InlineData("Ward IV", "ward-iv")]
    public void A_slug_matches_what_the_frontend_builds(string name, string expected)
    {
        LocationSlug.Create(name).Value.Should().Be(expected);
    }

    /// <summary>
    /// Diacritics are folded rather than dropped, so Curépipe and Curepipe are one place
    /// rather than two.
    /// </summary>
    [Fact]
    public void A_slug_folds_diacritics_rather_than_dropping_the_letters()
    {
        LocationSlug.Create("Curépipe").Value.Should().Be("curepipe");
        LocationSlug.Create("Côte d'Ivoire").Value.Should().Be("cote-d-ivoire");
    }

    [Fact]
    public void A_qualified_slug_disambiguates_namesakes()
    {
        var illinois = LocationSlug.CreateQualified("Springfield", "Illinois");
        var missouri = LocationSlug.CreateQualified("Springfield", "Missouri");

        illinois.Should().NotBe(missouri);
    }

    // ── Hierarchy paths ──────────────────────────────────────────────────

    [Fact]
    public void A_path_reports_its_depth_and_its_parent()
    {
        var path = HierarchyPath.ForRoot("GB")
            .Append("greater-london").Value
            .Append("london").Value
            .Append("peckham").Value;

        path.Value.Should().Be("/GB/greater-london/london/peckham");
        path.Depth.Should().Be(4);
        path.Parent!.Value.Should().Be("/GB/greater-london/london");
    }

    /// <summary>
    /// The trailing separator on the descendant prefix is what stops <c>/GB/london</c> from
    /// claiming <c>/GB/london-ontario</c>. Without it the containment test is wrong in a way
    /// that only shows up once two similarly named places exist.
    /// </summary>
    [Fact]
    public void Containment_does_not_match_a_sibling_with_a_shared_prefix()
    {
        var london = HierarchyPath.ForRoot("GB").Append("london").Value;
        var ontario = HierarchyPath.ForRoot("GB").Append("london-ontario").Value;
        var peckham = london.Append("peckham").Value;

        london.Contains(peckham).Should().BeTrue();
        london.Contains(ontario).Should().BeFalse();
    }

    // ── Search radius ────────────────────────────────────────────────────

    /// <summary>
    /// The frontend's RadiusSlider encodes city-wide as -1 and country-wide as -2 on the same
    /// numeric field as real distances. Both are accepted and translated here, so no client
    /// change is forced.
    /// </summary>
    [Fact]
    public void The_client_radius_sentinels_translate_to_scopes()
    {
        SearchRadius.FromClientValue(-1d).Value.Scope.Should().Be(RadiusScope.City);
        SearchRadius.FromClientValue(-2d).Value.Scope.Should().Be(RadiusScope.Country);
        SearchRadius.FromClientValue(5d).Value.Scope.Should().Be(RadiusScope.Radius);
        SearchRadius.FromClientValue(5d).Value.Distance.Kilometers.Should().Be(5d);
    }

    /// <summary>A scope round-trips back to the encoding the slider renders.</summary>
    [Fact]
    public void A_scope_round_trips_to_the_client_encoding()
    {
        SearchRadius.CityWide.ToClientValue().Should().Be(-1d);
        SearchRadius.CountryWide.ToClientValue().Should().Be(-2d);
        SearchRadius.FromKilometers(3d).Value.ToClientValue().Should().Be(3d);
    }

    /// <summary>No radius sent is not the same as a radius of zero.</summary>
    [Fact]
    public void An_absent_radius_falls_back_to_the_client_default()
    {
        SearchRadius.FromClientValue(null).Value.Should().Be(SearchRadius.Default);
        SearchRadius.Default.Distance.Kilometers.Should().Be(5d);
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(-5d)]
    [InlineData(10_000d)]
    public void An_out_of_band_radius_is_rejected(double kilometers)
    {
        SearchRadius.FromKilometers(kilometers).IsFailure.Should().BeTrue();
    }

    // ── Boundaries ───────────────────────────────────────────────────────

    [Fact]
    public void A_boundary_needs_at_least_three_points()
    {
        var result = GeographicBoundary.TryCreate(
        [
            GeoCoordinate.Create(0d, 0d),
            GeoCoordinate.Create(1d, 1d)
        ]);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void A_boundary_contains_a_point_inside_the_ring_and_excludes_one_outside()
    {
        var square = GeographicBoundary.TryCreate(
        [
            GeoCoordinate.Create(0d, 0d),
            GeoCoordinate.Create(0d, 10d),
            GeoCoordinate.Create(10d, 10d),
            GeoCoordinate.Create(10d, 0d)
        ]).Value;

        square.Contains(GeoCoordinate.Create(5d, 5d)).Should().BeTrue();
        square.Contains(GeoCoordinate.Create(15d, 5d)).Should().BeFalse();
    }

    /// <summary>
    /// A concave outline is where a bounding-box-only implementation gives the wrong answer,
    /// which is exactly why the ring test exists.
    /// </summary>
    [Fact]
    public void A_concave_boundary_excludes_a_point_in_its_notch()
    {
        var lShape = GeographicBoundary.TryCreate(
        [
            GeoCoordinate.Create(0d, 0d),
            GeoCoordinate.Create(0d, 10d),
            GeoCoordinate.Create(4d, 10d),
            GeoCoordinate.Create(4d, 4d),
            GeoCoordinate.Create(10d, 4d),
            GeoCoordinate.Create(10d, 0d)
        ]).Value;

        lShape.Extent.Contains(GeoCoordinate.Create(8d, 8d)).Should().BeTrue();
        lShape.Contains(GeoCoordinate.Create(8d, 8d)).Should().BeFalse();
        lShape.Contains(GeoCoordinate.Create(2d, 2d)).Should().BeTrue();
    }

    /// <summary>The GeoJSON convention of repeating the first point is accepted and normalised.</summary>
    [Fact]
    public void A_closed_ring_is_normalised_to_an_open_one()
    {
        var closed = GeographicBoundary.TryCreate(
        [
            GeoCoordinate.Create(0d, 0d),
            GeoCoordinate.Create(0d, 10d),
            GeoCoordinate.Create(10d, 10d),
            GeoCoordinate.Create(0d, 0d)
        ]).Value;

        closed.Vertices.Should().HaveCount(3);
    }
}
