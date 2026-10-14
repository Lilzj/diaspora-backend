using Diaspora.Modules.Housing.Application.Services;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.Modules.Location.Integration;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Housing;

/// <summary>
/// DEFECT-010 — a country-only search must not delete every listing that has no neighbourhood.
/// <para>
/// <c>GeographicScopeResolver</c> answers a country-only request with <em>both</em> a
/// <c>CountryCode</c> and <c>Path = HierarchyPath.ForRoot(country)</c>. The adapter used to turn
/// that path into an <c>AreaPathPrefix</c>, and the repository matches a prefix as
/// <c>AreaPath != null &amp;&amp; AreaPath.StartsWith(prefix)</c> — so a listing filed without an
/// area, which is every listing the client can create, was silently excluded.
/// </para>
/// <para>
/// Observed at runtime before the fix: <c>GET /housing/search?countryCode=NG</c> answered
/// <c>totalCount: 0</c> while the same search with no country answered with the listings. These
/// tests pin the boundary in both directions, because the danger of the fix is over-correcting
/// into "never narrow by path at all".
/// </para>
/// </summary>
public class HousingGeographicScopeAdapterTests
{
    private static readonly HousingListingQueryFilter Empty = new();

    /// <summary>The defect itself.</summary>
    [Fact]
    public void A_country_only_scope_does_not_narrow_by_area_path()
    {
        var scope = new GeographicScope
        {
            CountryCode = CountryCode.TryCreate("NG").Value,
            Path = HierarchyPath.ForRoot("NG")
        };

        var filter = HousingGeographicScopeAdapter.Apply(Empty, scope);

        filter.AreaPathPrefix.Should().BeNull(
            "a country root is already applied exactly as CountryCode, and matching it as a path "
            + "prefix deletes every listing whose AreaPath is null");
    }

    /// <summary>The country is still applied — the fix must not widen the search to everywhere.</summary>
    [Fact]
    public void A_country_only_scope_still_narrows_by_country()
    {
        var scope = new GeographicScope
        {
            CountryCode = CountryCode.TryCreate("NG").Value,
            Path = HierarchyPath.ForRoot("NG")
        };

        var filter = HousingGeographicScopeAdapter.Apply(Empty, scope);

        filter.CountryCode.Should().Be("NG");
    }

    /// <summary>
    /// A catalogue path below the root narrows nothing either — 14 Oct 2026. The owner retired the
    /// place catalogue for discovery; a home is found by its coordinates, its country and its
    /// town's name, and a scope that still carries a path (from an old caller) must not filter by it.
    /// </summary>
    [Fact]
    public void A_path_below_the_root_no_longer_narrows_anything()
    {
        var path = HierarchyPath.ForRoot("GB").Append("GB-LND").Value.Append("peckham").Value;

        var scope = new GeographicScope
        {
            CountryCode = CountryCode.TryCreate("GB").Value,
            Path = path
        };

        var filter = HousingGeographicScopeAdapter.Apply(Empty, scope);

        filter.AreaPathPrefix.Should().BeNull();
        filter.AreaPathCityFallback.Should().BeNull();
        filter.CountryCode.Should().Be("GB");
    }

    /// <summary>
    /// With no path at all the city name is the narrowing, and dropping the root must not disturb
    /// that branch.
    /// </summary>
    [Fact]
    public void A_scope_with_a_city_and_no_path_still_narrows_by_city()
    {
        var scope = new GeographicScope
        {
            CountryCode = CountryCode.TryCreate("NG").Value,
            CityName = "Lagos"
        };

        var filter = HousingGeographicScopeAdapter.Apply(Empty, scope);

        filter.CityName.Should().Be("Lagos");
        filter.AreaPathPrefix.Should().BeNull();
    }

    /// <summary>
    /// A country root alongside a city name falls through to the city, rather than narrowing on
    /// neither. This is the combination a client sends most often.
    /// </summary>
    [Fact]
    public void A_country_root_falls_through_to_the_city_when_one_is_present()
    {
        var scope = new GeographicScope
        {
            CountryCode = CountryCode.TryCreate("NG").Value,
            Path = HierarchyPath.ForRoot("NG"),
            CityName = "Lagos"
        };

        var filter = HousingGeographicScopeAdapter.Apply(Empty, scope);

        filter.AreaPathPrefix.Should().BeNull();
        filter.CityName.Should().Be("Lagos");
        filter.CountryCode.Should().Be("NG");
    }

    /// <summary>An unbounded scope narrows nothing, which is "everywhere" rather than "nowhere".</summary>
    [Fact]
    public void An_unbounded_scope_narrows_nothing()
    {
        var filter = HousingGeographicScopeAdapter.Apply(Empty, GeographicScope.Unbounded);

        filter.CountryCode.Should().BeNull();
        filter.AreaPathPrefix.Should().BeNull();
        filter.CityName.Should().BeNull();
    }
}
