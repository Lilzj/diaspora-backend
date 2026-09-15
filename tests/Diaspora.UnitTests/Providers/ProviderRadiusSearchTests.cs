using Diaspora.Modules.Location.Domain.Search;
using Diaspora.Modules.Location.Domain.Spatial;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.Modules.Location.Integration;
using Diaspora.Modules.Providers.Application.Services;
using Diaspora.Modules.Providers.Domain.Providers;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Diaspora.UnitTests.Providers;

/// <summary>
/// Searching for a service by distance — 15 Sep 2026.
/// <para>
/// The resolver snaps a point to the nearest catalogued town and hands back that town's path, and
/// the path used to be applied on top of the circle's rectangle, so "within 25 km" meant "inside
/// the nearest town, and within 25 km". A radius is now answered by the rectangle alone.
/// </para>
/// </summary>
public sealed class ProviderRadiusSearchTests
{
    private static readonly GeoCoordinate RoseHill = GeoCoordinate.Create(-20.2410, 57.4769);

    [Fact]
    public async Task A_point_search_is_not_narrowed_to_the_nearest_town()
    {
        var adapter = new GeographicScopeAdapter(
            Substitute.For<IGeographicScopeResolver>(), Substitute.For<ILocationDirectory>());

        var scope = PointScope(kilometres: 25);

        var filter = await adapter.ApplyAsync(new ProviderQueryFilter(), scope, aroundPoint: true);

        filter.HierarchyPathPrefix.Should().BeNull();
        filter.AncestorHierarchyPaths.Should().BeNull();
        filter.AreaId.Should().BeNull();
        filter.CityName.Should().BeNull();
        filter.CountryCode.Should().Be("MU");
        filter.HasBoundingBox.Should().BeTrue();
        filter.MinLatitude.Should().Be(scope.Bounds!.South);
        filter.MaxLongitude.Should().Be(scope.Bounds.East);
    }

    [Fact]
    public async Task A_town_search_still_narrows_by_the_town()
    {
        var adapter = new GeographicScopeAdapter(
            Substitute.For<IGeographicScopeResolver>(), Substitute.For<ILocationDirectory>());

        var scope = PointScope(kilometres: 5);

        var filter = await adapter.ApplyAsync(new ProviderQueryFilter(), scope, aroundPoint: false);

        filter.HierarchyPathPrefix.Should().Be("/MU/plaines-wilhems/rose-hill");
    }

    private static GeographicScope PointScope(double kilometres)
    {
        var radius = SearchRadius.FromKilometers(kilometres).Value;
        var search = NearbySearch.Create(RoseHill, radius, new HaversineDistanceCalculator()).Value;

        return new GeographicScope
        {
            CountryCode = CountryCode.TryCreate("MU").Value,
            Path = HierarchyPath.TryCreate("/MU/plaines-wilhems/rose-hill").Value,
            CityName = "Rose Hill",
            Center = RoseHill,
            Radius = radius,
            Bounds = search.Bounds,
            Search = search
        };
    }
}
