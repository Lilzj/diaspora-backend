using Diaspora.Modules.Location.Domain.Search;
using Diaspora.Modules.Location.Domain.Spatial;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.Modules.Location.Integration;
using Diaspora.Modules.Providers.Application.Services;
using Diaspora.Modules.Providers.Domain.Providers;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Providers;

/// <summary>
/// Searching for a service by distance — 15 Sep 2026, and without the catalogue since 14 Oct 2026.
/// <para>
/// The resolver used to snap a point to the nearest catalogued town and the path was applied on
/// top of the circle's rectangle, so "within 25 km" meant "inside the nearest town, and within
/// 25 km". A radius is answered by the rectangle alone, and a town search by the town's name —
/// never by a catalogue path, even when a scope carries one.
/// </para>
/// </summary>
public sealed class ProviderRadiusSearchTests
{
    private static readonly GeoCoordinate RoseHill = GeoCoordinate.Create(-20.2410, 57.4769);

    [Fact]
    public void A_point_search_is_not_narrowed_to_the_nearest_town()
    {
        var scope = PointScope(kilometres: 25);

        var filter = GeographicScopeAdapter.Apply(new ProviderQueryFilter(), scope, aroundPoint: true);

        filter.HierarchyPathPrefix.Should().BeNull();
        filter.AncestorHierarchyPaths.Should().BeNull();
        filter.AreaId.Should().BeNull();
        filter.CityName.Should().BeNull();
        filter.CountryCode.Should().Be("MU");
        filter.HasBoundingBox.Should().BeTrue();
        filter.MinLatitude.Should().Be(scope.Bounds!.South);
        filter.MaxLongitude.Should().Be(scope.Bounds.East);

        // The town the request named only keeps providers that have no pin of their own.
        filter.UnpinnedCityName.Should().Be("Rose Hill");
    }

    [Fact]
    public void A_town_search_narrows_by_the_town_name_and_never_by_a_catalogue_path()
    {
        var scope = PointScope(kilometres: 5);

        var filter = GeographicScopeAdapter.Apply(new ProviderQueryFilter(), scope, aroundPoint: false);

        filter.HierarchyPathPrefix.Should().BeNull();
        filter.AreaId.Should().BeNull();
        filter.CityName.Should().Be("Rose Hill");
        filter.CountryCode.Should().Be("MU");
        filter.HasBoundingBox.Should().BeFalse();
    }

    [Fact]
    public void A_viewport_is_its_rectangle_and_leaves_out_remote_providers()
    {
        var box = BoundingBox.Create(-20.0, -20.5, 57.7, 57.3);

        var filter = GeographicScopeAdapter.Apply(
            new ProviderQueryFilter(),
            new GeographicScope { Bounds = box, IsViewport = true, Center = box.Center },
            aroundPoint: false);

        filter.MinLatitude.Should().Be(-20.5);
        filter.MaxLatitude.Should().Be(-20.0);
        filter.MinLongitude.Should().Be(57.3);
        filter.MaxLongitude.Should().Be(57.7);
        filter.CityName.Should().BeNull();
        filter.IncludeRemoteProviders.Should().BeFalse();
    }

    private static GeographicScope PointScope(double kilometres)
    {
        var radius = SearchRadius.FromKilometers(kilometres).Value;
        var search = NearbySearch.Create(RoseHill, radius, new HaversineDistanceCalculator()).Value;

        return new GeographicScope
        {
            CountryCode = CountryCode.TryCreate("MU").Value,

            // A path a pre-retirement scope would have carried. It must narrow nothing.
            Path = HierarchyPath.TryCreate("/MU/plaines-wilhems/rose-hill").Value,
            CityName = "Rose Hill",
            Center = RoseHill,
            Radius = radius,
            Bounds = search.Bounds,
            Search = search
        };
    }
}
