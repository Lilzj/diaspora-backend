using Diaspora.Modules.Housing.Application.Services;
using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Modules.Housing.Domain.ValueObjects;
using Diaspora.Modules.Location.Domain.Search;
using Diaspora.Modules.Location.Domain.Spatial;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.Modules.Location.Integration;
using Diaspora.UnitTests.Housing.TestData;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Diaspora.UnitTests.Housing;

/// <summary>
/// Searching for a home by distance, by town and by country — 15 Sep 2026.
/// <para>
/// <b>Three failures, all found on two phones against the live platform.</b> "Within 5 km" was
/// answered as "filed under the nearest town"; a town search matched only listings filed under an
/// area, which no create screen does; and a country-wide search that still carried a point was
/// narrowed to the town nearest that point. Every home on the platform has a position and a town
/// name and no area, so all three searches came back empty or wrong.
/// </para>
/// </summary>
public sealed class HousingRadiusSearchTests
{
    private static readonly HousingListingQueryFilter Empty = new();

    private static readonly HaversineDistanceCalculator Calculator = new();

    // Rose Hill and Curepipe, Mauritius: about 9 km apart.
    private static readonly GeoCoordinate RoseHill = GeoCoordinate.Create(-20.2410, 57.4769);
    private static readonly GeoCoordinate Curepipe = GeoCoordinate.Create(-20.3163, 57.5259);

    [Fact]
    public void A_point_search_is_answered_by_the_rectangle_around_its_circle_not_the_nearest_town()
    {
        var scope = PointScope(RoseHill, kilometres: 5, nearestTown: "/MU/plaines-wilhems/rose-hill");

        var filter = HousingGeographicScopeAdapter.Apply(
            Empty with { CityName = "Rose Hill" }, scope, aroundPoint: true);

        filter.AreaPathPrefix.Should().BeNull("the nearest town is not part of a distance question");
        filter.CityName.Should().BeNull();
        filter.CountryCode.Should().Be("MU");
        filter.HasBounds.Should().BeTrue();

        // Snapped outward to the published precision, so the rectangle can only widen.
        filter.BoundsSouth.Should().BeLessThanOrEqualTo(scope.Bounds!.South);
        filter.BoundsNorth.Should().BeGreaterThanOrEqualTo(scope.Bounds.North);
        filter.BoundsWest.Should().BeLessThanOrEqualTo(scope.Bounds.West);
        filter.BoundsEast.Should().BeGreaterThanOrEqualTo(scope.Bounds.East);
    }

    [Fact]
    public void The_centre_the_resolver_gives_a_town_search_does_not_turn_it_into_a_radius()
    {
        var scope = PointScope(RoseHill, kilometres: 5, nearestTown: "/MU/plaines-wilhems/rose-hill");

        var filter = HousingGeographicScopeAdapter.Apply(Empty, scope, aroundPoint: false);

        filter.HasBounds.Should().BeFalse();
        filter.AreaPathPrefix.Should().Be("/MU/plaines-wilhems/rose-hill");
    }

    [Fact]
    public void A_town_search_keeps_the_listings_filed_without_an_area()
    {
        var scope = new GeographicScope
        {
            CountryCode = CountryCode.TryCreate("MU").Value,
            Path = HierarchyPath.TryCreate("/MU/plaines-wilhems/rose-hill").Value,
            CityName = "Rose Hill"
        };

        var filter = HousingGeographicScopeAdapter.Apply(Empty, scope);

        filter.AreaPathPrefix.Should().Be("/MU/plaines-wilhems/rose-hill");
        filter.AreaPathCityFallback.Should().Be(
            "Rose Hill", "no create screen files a listing under an area, so a prefix alone finds none");
    }

    [Fact]
    public void A_neighbourhood_search_does_not_widen_to_every_unfiled_listing_in_its_town()
    {
        var scope = new GeographicScope
        {
            CountryCode = CountryCode.TryCreate("GB").Value,
            Path = HierarchyPath.TryCreate("/GB/greater-london/london/peckham").Value,
            CityName = "London"
        };

        var filter = HousingGeographicScopeAdapter.Apply(Empty, scope);

        filter.AreaPathPrefix.Should().Be("/GB/greater-london/london/peckham");
        filter.AreaPathCityFallback.Should().BeNull();
    }

    [Fact]
    public void A_country_wide_search_is_not_narrowed_to_the_town_nearest_its_point()
    {
        var scope = new GeographicScope
        {
            CountryCode = CountryCode.TryCreate("MU").Value,
            Path = HierarchyPath.ForRoot("MU"),
            CityName = "Curepipe",
            Center = Curepipe,
            Radius = SearchRadius.CountryWide
        };

        var filter = HousingGeographicScopeAdapter.Apply(Empty, scope, aroundPoint: true);

        filter.CountryCode.Should().Be("MU");
        filter.CityName.Should().BeNull();
        filter.AreaPathPrefix.Should().BeNull();
        filter.HasBounds.Should().BeFalse();
    }

    [Fact]
    public void Distances_are_measured_from_the_position_a_stranger_may_see_and_the_corners_are_trimmed()
    {
        var directory = Substitute.For<ILocationDirectory>();
        directory.DistanceBetween(Arg.Any<GeoCoordinate>(), Arg.Any<GeoCoordinate>())
            .Returns(call => Calculator.Between(
                call.ArgAt<GeoCoordinate>(0), call.ArgAt<GeoCoordinate>(1)));

        var adapter = new HousingGeographicScopeAdapter(
            Substitute.For<IGeographicScopeResolver>(), directory);

        var near = ListingAt(-20.2415, 57.4772);
        var far = ListingAt(Curepipe.Latitude, Curepipe.Longitude);
        var nowhere = ListingAt(null, null);

        var scope = PointScope(RoseHill, kilometres: 5, nearestTown: "/MU/plaines-wilhems/rose-hill");
        IReadOnlyList<HousingListing> page = [near, far, nowhere];

        var distances = adapter.MeasureDistances(scope, page, aroundPoint: true);

        distances.Should().ContainKey(near.Id.Value);
        distances[near.Id.Value].Should().BeLessThan(1.5d);
        distances[far.Id.Value].Should().BeGreaterThan(5d);
        distances.Should().NotContainKey(nowhere.Id.Value, "a home with no position has no distance");

        // The published position is rounded to two places; a distance from the exact one would
        // return that position to anybody who asked from three places.
        directory.Received().DistanceBetween(
            Arg.Is(RoseHill),
            Arg.Is<GeoCoordinate>(point => point.Latitude == -20.24 && point.Longitude == 57.48));

        HousingGeographicScopeAdapter.WithinRadius(scope, page, distances, aroundPoint: true)
            .Should().Equal(near);
    }

    [Fact]
    public void Nothing_is_measured_or_trimmed_for_a_search_without_a_point()
    {
        var adapter = new HousingGeographicScopeAdapter(
            Substitute.For<IGeographicScopeResolver>(), Substitute.For<ILocationDirectory>());

        var scope = PointScope(RoseHill, kilometres: 5, nearestTown: "/MU/plaines-wilhems/rose-hill");
        IReadOnlyList<HousingListing> page = [ListingAt(Curepipe.Latitude, Curepipe.Longitude)];

        var distances = adapter.MeasureDistances(scope, page, aroundPoint: false);

        distances.Should().BeEmpty();
        HousingGeographicScopeAdapter.WithinRadius(scope, page, distances, aroundPoint: false)
            .Should().Equal(page);
    }

    /// <summary>The scope the resolver builds from coordinates and a radius.</summary>
    private static GeographicScope PointScope(GeoCoordinate centre, double kilometres, string nearestTown)
    {
        var radius = SearchRadius.FromKilometers(kilometres).Value;
        var search = NearbySearch.Create(centre, radius, Calculator).Value;

        return new GeographicScope
        {
            CountryCode = CountryCode.TryCreate("MU").Value,
            Path = HierarchyPath.TryCreate(nearestTown).Value,
            CityName = "Rose Hill",
            Center = centre,
            Radius = radius,
            Bounds = search.Bounds,
            Search = search
        };
    }

    private static HousingListing ListingAt(double? latitude, double? longitude) =>
        HousingListing.Draft(
            HousingFixtures.NewListingId(),
            HousingFixtures.NewLandlordId(),
            HousingFixtures.NewUserId(),
            HousingTitle.Create("Two-bed flat").Value,
            HousingSlug.Create($"two-bed-flat-{Guid.NewGuid():N}"),
            HousingDescription.Create("A flat.").Value,
            "apartment",
            HousingFixtures.Price(currency: "MUR"),
            HousingCapacity.Create(2, 1m, maximumOccupants: 4).Value,
            HousingAvailability.Create(null, totalUnits: 1).Value,
            HousingLocationContext.Create("MU", null, "Rose Hill", null, null, null).Value,
            PropertyAddress.Create(
                "14 Royal Road", null, null, latitude, longitude, AddressVisibility.NeighborhoodOnly).Value,
            StayPolicy.Create(acceptsShortStay: false, acceptsLongStay: true, isSharedAccommodation: false).Value,
            HousingFixtures.Now).Value;
}
