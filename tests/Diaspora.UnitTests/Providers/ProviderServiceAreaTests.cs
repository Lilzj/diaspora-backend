using Diaspora.Modules.Providers.Domain.Enums;
using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.Modules.Providers.Domain.Providers;
using Diaspora.Modules.Providers.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Providers;

/// <summary>
/// Where a provider serves, and who may find it, without the place catalogue (14 Oct 2026).
/// </summary>
public sealed class ProviderServiceAreaTests
{
    [Fact]
    public void A_town_service_area_names_its_town_and_country_and_no_catalogue_node()
    {
        var area = ProviderServiceArea.ForTown(ProviderServiceAreaId.New(), "ke", " Mombasa ").Value;

        area.Kind.Should().Be(ServiceAreaKind.Town);
        area.CountryCode.Should().Be("KE");
        area.CityName.Should().Be("Mombasa");
        area.Label.Should().Be("Mombasa", "the town is its own label when none is given");
        area.AreaId.Should().BeNull();
        area.HierarchyPath.Should().BeNull();
    }

    [Theory]
    [InlineData(null, "Mombasa")]
    [InlineData("KE", null)]
    [InlineData("KE", "  ")]
    public void A_town_service_area_needs_both_halves(string? country, string? town)
    {
        ProviderServiceArea.ForTown(ProviderServiceAreaId.New(), country, town)
            .IsFailure.Should().BeTrue("a town name alone is ambiguous across countries");
    }

    [Fact]
    public void Two_town_areas_for_the_same_town_in_any_case_are_duplicates()
    {
        var first = ProviderServiceArea.ForTown(ProviderServiceAreaId.New(), "KE", "Mombasa").Value;
        var second = ProviderServiceArea.ForTown(ProviderServiceAreaId.New(), "KE", "MOMBASA").Value;

        second.DedupeKey.Should().Be(first.DedupeKey);
    }

    [Fact]
    public void Area_only_visibility_stores_no_catalogue_area()
    {
        var visibility = ProviderVisibility.Create(
            ProviderVisibilityLevel.AreaOnly, countryCode: "ke", areaId: Guid.NewGuid());

        visibility.Level.Should().Be(ProviderVisibilityLevel.AreaOnly);
        visibility.AreaId.Should().BeNull();
        visibility.CountryCode.Should().Be("KE");
    }
}
