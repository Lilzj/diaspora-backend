using Diaspora.Modules.Location.Domain.Areas;
using Diaspora.Modules.Location.Domain.Countries;
using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.Modules.Location.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Location;

/// <summary>
/// The hierarchy: its one invariant, its skippable rungs, and the relocation that is the price
/// of the materialised path.
/// </summary>
public sealed class GeographicHierarchyTests
{
    private static Country UnitedKingdom() =>
        Country.Register(CountryCode.Create("GB"), PlaceName.Create("United Kingdom"));

    private static Country Mauritius() =>
        Country.Register(CountryCode.Create("MU"), PlaceName.Create("Mauritius"));

    private static AdministrativeArea Under(
        Country country, AdministrativeLevel level, string name) =>
        AdministrativeArea.CreateUnderCountry(country, level, PlaceName.Create(name)).Value;

    private static AdministrativeArea Under(
        AdministrativeArea parent, AdministrativeLevel level, string name) =>
        AdministrativeArea.CreateUnder(parent, level, PlaceName.Create(name)).Value;

    // ── The invariant ────────────────────────────────────────────────────

    /// <summary>
    /// The single rule that keeps the tree acyclic and the path honest. A city filed under a
    /// neighbourhood corrupts every ancestor query that touches it, silently.
    /// </summary>
    [Fact]
    public void A_node_cannot_sit_inside_a_finer_one()
    {
        var country = UnitedKingdom();
        var london = Under(country, AdministrativeLevel.City, "London");
        var peckham = Under(london, AdministrativeLevel.Neighborhood, "Peckham");

        var result = AdministrativeArea.CreateUnder(
            peckham, AdministrativeLevel.City, PlaceName.Create("Nonsense"));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("location.area.level-mismatch");
    }

    [Fact]
    public void A_node_cannot_sit_inside_one_at_its_own_level()
    {
        var country = UnitedKingdom();
        var london = Under(country, AdministrativeLevel.City, "London");

        AdministrativeArea.CreateUnder(
                london, AdministrativeLevel.City, PlaceName.Create("Manchester"))
            .IsFailure.Should().BeTrue();
    }

    [Fact]
    public void A_country_cannot_be_registered_as_an_area()
    {
        var country = UnitedKingdom();

        AdministrativeArea.CreateUnderCountry(
                country, AdministrativeLevel.Country, PlaceName.Create("Nonsense"))
            .IsFailure.Should().BeTrue();
    }

    // ── Skippable rungs ──────────────────────────────────────────────────

    /// <summary>
    /// Mauritius organises by district with no state above it, and England has counties where
    /// France has departments. A hierarchy that demanded adjacent levels would be wrong
    /// everywhere.
    /// </summary>
    [Fact]
    public void Levels_may_be_skipped()
    {
        var mauritius = Mauritius();

        var district = Under(mauritius, AdministrativeLevel.District, "Plaines Wilhems");
        var city = Under(district, AdministrativeLevel.City, "Rose Hill");

        city.Path.Value.Should().Be("/MU/plaines-wilhems/rose-hill");
        city.Depth.Should().Be(3);
    }

    [Fact]
    public void A_city_may_hang_straight_off_its_country()
    {
        var country = UnitedKingdom();
        var city = Under(country, AdministrativeLevel.City, "London");

        city.ParentId.Should().BeNull();
        city.Path.Value.Should().Be("/GB/london");
    }

    // ── Containment ──────────────────────────────────────────────────────

    [Fact]
    public void A_descendant_reports_itself_as_within_its_ancestor()
    {
        var country = UnitedKingdom();
        var region = Under(country, AdministrativeLevel.Region, "Greater London");
        var london = Under(region, AdministrativeLevel.City, "London");
        var peckham = Under(london, AdministrativeLevel.Neighborhood, "Peckham");

        peckham.IsWithin(london).Should().BeTrue();
        peckham.IsWithin(region).Should().BeTrue();
        london.IsWithin(peckham).Should().BeFalse();
    }

    // ── The domain service ───────────────────────────────────────────────

    /// <summary>
    /// Ordered by path rather than by level, so a set with a skipped rung still comes back in
    /// the right order without the caller knowing which rungs exist.
    /// </summary>
    [Fact]
    public void A_breadcrumb_orders_root_first_across_skipped_levels()
    {
        var country = Mauritius();
        var district = Under(country, AdministrativeLevel.District, "Port Louis");
        var city = Under(district, AdministrativeLevel.City, "Port Louis");
        var ward = Under(city, AdministrativeLevel.Neighborhood, "Ward IV");

        var breadcrumb = GeographicHierarchy.ToBreadcrumb([ward, district, city]);

        breadcrumb.Select(area => area.Name.Value)
            .Should().ContainInOrder("Port Louis", "Port Louis", "Ward IV");
    }

    [Fact]
    public void Direct_children_are_the_nodes_whose_parent_is_this_one()
    {
        var country = UnitedKingdom();
        var london = Under(country, AdministrativeLevel.City, "London");
        var peckham = Under(london, AdministrativeLevel.Neighborhood, "Peckham");
        var brixton = Under(london, AdministrativeLevel.Neighborhood, "Brixton");
        var manchester = Under(country, AdministrativeLevel.City, "Manchester");

        var children = GeographicHierarchy.DirectChildren(
            london, [peckham, brixton, manchester]);

        children.Should().HaveCount(2);
        children.Select(child => child.Name.Value).Should().Contain(["Peckham", "Brixton"]);
    }

    /// <summary>
    /// Answers "how close administratively?" — which ranks a result in the same neighbourhood
    /// above one merely in the same city, something a raw kilometre figure does not capture.
    /// </summary>
    [Fact]
    public void A_common_ancestor_is_the_deepest_shared_path()
    {
        var country = UnitedKingdom();
        var london = Under(country, AdministrativeLevel.City, "London");
        var peckham = Under(london, AdministrativeLevel.Neighborhood, "Peckham");
        var brixton = Under(london, AdministrativeLevel.Neighborhood, "Brixton");

        GeographicHierarchy.CommonAncestor(peckham, brixton)!.Value
            .Should().Be("/GB/london");
    }

    [Fact]
    public void Two_countries_share_no_common_ancestor()
    {
        var london = Under(UnitedKingdom(), AdministrativeLevel.City, "London");
        var roseHill = Under(Mauritius(), AdministrativeLevel.City, "Rose Hill");

        GeographicHierarchy.CommonAncestor(london, roseHill).Should().BeNull();
    }

    /// <summary>
    /// "Which city is this neighbourhood in?" answered as a filter rather than a parent walk,
    /// because the path already names every ancestor.
    /// </summary>
    [Fact]
    public void An_ancestor_at_a_level_is_found_without_walking_the_tree()
    {
        var country = UnitedKingdom();
        var region = Under(country, AdministrativeLevel.Region, "Greater London");
        var london = Under(region, AdministrativeLevel.City, "London");
        var peckham = Under(london, AdministrativeLevel.Neighborhood, "Peckham");

        GeographicHierarchy
            .AncestorAtLevel(peckham, AdministrativeLevel.City, [region, london, peckham])
            .Should().Be(london);
    }

    // ── Relocation ───────────────────────────────────────────────────────

    /// <summary>
    /// The cost of the materialised path, paid on the operation that happens once a decade.
    /// The aggregate returns the rewrite rather than performing it, because its descendants
    /// are separate aggregates.
    /// </summary>
    [Fact]
    public void Moving_a_node_returns_the_rewrite_its_descendants_need()
    {
        var country = UnitedKingdom();
        var oldRegion = Under(country, AdministrativeLevel.Region, "Greater London");
        var newRegion = Under(country, AdministrativeLevel.Region, "South East");
        var london = Under(oldRegion, AdministrativeLevel.City, "London");

        var relocation = london.MoveUnder(newRegion);

        relocation.IsSuccess.Should().BeTrue();
        relocation.Value.PreviousPath.Value.Should().Be("/GB/greater-london/london");
        relocation.Value.NewPath.Value.Should().Be("/GB/south-east/london");
        london.Path.Value.Should().Be("/GB/south-east/london");
    }

    [Fact]
    public void A_descendant_rebases_onto_the_new_path()
    {
        var country = UnitedKingdom();
        var oldRegion = Under(country, AdministrativeLevel.Region, "Greater London");
        var newRegion = Under(country, AdministrativeLevel.Region, "South East");
        var london = Under(oldRegion, AdministrativeLevel.City, "London");
        var peckham = Under(london, AdministrativeLevel.Neighborhood, "Peckham");

        var relocation = london.MoveUnder(newRegion).Value;

        peckham.ApplyAncestorRelocation(relocation).Should().BeTrue();
        peckham.Path.Value.Should().Be("/GB/south-east/london/peckham");
    }

    /// <summary>A node that is not beneath the moved one must be left alone.</summary>
    [Fact]
    public void An_unrelated_node_is_not_rebased()
    {
        var country = UnitedKingdom();
        var oldRegion = Under(country, AdministrativeLevel.Region, "Greater London");
        var newRegion = Under(country, AdministrativeLevel.Region, "South East");
        var london = Under(oldRegion, AdministrativeLevel.City, "London");
        var manchester = Under(country, AdministrativeLevel.City, "Manchester");

        var relocation = london.MoveUnder(newRegion).Value;

        manchester.ApplyAncestorRelocation(relocation).Should().BeFalse();
        manchester.Path.Value.Should().Be("/GB/manchester");
    }

    [Fact]
    public void A_node_cannot_be_moved_beneath_its_own_descendant()
    {
        var country = UnitedKingdom();
        var london = Under(country, AdministrativeLevel.City, "London");
        var peckham = Under(london, AdministrativeLevel.Neighborhood, "Peckham");

        london.MoveUnder(peckham).IsFailure.Should().BeTrue();
    }

    // ── Typed views ──────────────────────────────────────────────────────

    /// <summary>
    /// The views are what make the six named domain concepts survive one aggregate. A view
    /// over the wrong level must not construct.
    /// </summary>
    [Fact]
    public void A_typed_view_refuses_a_node_at_the_wrong_level()
    {
        var london = Under(UnitedKingdom(), AdministrativeLevel.City, "London");

        Neighborhood.For(london).Should().BeNull();
        City.For(london).Should().NotBeNull();

        var act = () => new Neighborhood(london);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void A_neighbourhood_view_exposes_the_hub_slug_the_frontend_routes_on()
    {
        var london = Under(UnitedKingdom(), AdministrativeLevel.City, "London");
        var peckham = Under(london, AdministrativeLevel.Neighborhood, "Peckham");

        new Neighborhood(peckham).HubSlug.Value.Should().Be("peckham");
    }

    /// <summary>
    /// The indexes are null until a data source exists. A view has to say so rather than
    /// report zeroes, which read as a very bad neighbourhood.
    /// </summary>
    [Fact]
    public void Quality_indexes_are_absent_until_they_are_sourced()
    {
        var london = Under(UnitedKingdom(), AdministrativeLevel.City, "London");
        var peckham = Under(london, AdministrativeLevel.Neighborhood, "Peckham");

        var view = new Neighborhood(peckham);

        view.HasQualityIndexes.Should().BeFalse();
        view.SafetyScore.Should().BeNull();

        peckham.SetQualityIndexes(84, 88, CostOfLivingBand.High);

        new Neighborhood(peckham).HasQualityIndexes.Should().BeTrue();
    }

    /// <summary>An out-of-range score from a third-party import is clamped, not rejected.</summary>
    [Fact]
    public void An_out_of_range_index_is_clamped_rather_than_losing_the_whole_record()
    {
        var london = Under(UnitedKingdom(), AdministrativeLevel.City, "London");
        var peckham = Under(london, AdministrativeLevel.Neighborhood, "Peckham");

        peckham.SetQualityIndexes(101, -5, CostOfLivingBand.Medium);

        peckham.SafetyScore.Should().Be(100);
        peckham.WalkabilityScore.Should().Be(0);
        peckham.CostOfLiving.Should().Be(CostOfLivingBand.Medium);
    }
}
