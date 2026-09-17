using Diaspora.Modules.Location.Domain.Areas;
using Diaspora.Modules.Location.Domain.Countries;
using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.Modules.Location.Persistence.Seeding;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Location;

/// <summary>
/// An outline says whose it is — 17 Sep 2026.
/// <para>
/// The catalogue stored rings with no record of where they came from, and the contract said so by
/// publishing no source. The first outlines it imports are OpenStreetMap's, under a licence that
/// requires a credit wherever they are shown, so the source now travels with every ring and a ring
/// cannot be stored without it.
/// </para>
/// </summary>
public sealed class AreaBoundaryProvenanceTests
{
    private static readonly DateOnly Retrieved = new(2026, 9, 17);

    [Theory]
    [InlineData(null, "ODbL-1.0", "© OpenStreetMap contributors")]
    [InlineData("OpenStreetMap", " ", "© OpenStreetMap contributors")]
    [InlineData("OpenStreetMap", "ODbL-1.0", "")]
    public void An_outline_nobody_can_credit_is_refused(string? source, string? licence, string? attribution)
    {
        BoundaryProvenance.TryCreate(source, licence, attribution, "relation/1", Retrieved)
            .IsFailure.Should().BeTrue();
    }

    [Fact]
    public void An_outline_with_no_retrieval_date_is_refused_because_nothing_could_ever_refresh_it()
    {
        BoundaryProvenance.TryCreate("OpenStreetMap", "ODbL-1.0", "© OpenStreetMap contributors", null, default)
            .IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Only_a_later_extract_of_the_same_shape_from_the_same_source_supersedes_an_outline()
    {
        var stored = Osm("relation/13476401", Retrieved);

        stored.IsSupersededBy(Osm("relation/13476401", Retrieved.AddDays(30))).Should().BeTrue();

        stored.IsSupersededBy(Osm("relation/13476401", Retrieved)).Should().BeFalse("the same extract is not newer");
        stored.IsSupersededBy(Osm("relation/13476401", Retrieved.AddDays(-1))).Should().BeFalse();
        stored.IsSupersededBy(Osm("relation/13476403", Retrieved.AddDays(30))).Should().BeFalse("a different shape");

        var surveyed = BoundaryProvenance.TryCreate(
            "Council survey", "OGL-UK-3.0", "Contains public sector information", "relation/13476401",
            Retrieved.AddDays(-100)).Value;

        surveyed.IsSupersededBy(Osm("relation/13476401", Retrieved)).Should().BeFalse(
            "an outline somebody else supplied is never replaced by an import");
    }

    [Fact]
    public void Setting_an_outline_records_its_source_and_its_extent_and_clearing_removes_both()
    {
        var country = Country.Register(CountryCode.Create("MU"), PlaceName.Create("Mauritius"));
        var town = AdministrativeArea.CreateUnderCountry(
            country, AdministrativeLevel.City, PlaceName.Create("Quatre Bornes")).Value;

        var ring = GeographicBoundary.TryCreate(
        [
            GeoCoordinate.Create(-20.24d, 57.44d),
            GeoCoordinate.Create(-20.24d, 57.52d),
            GeoCoordinate.Create(-20.31d, 57.52d),
            GeoCoordinate.Create(-20.31d, 57.44d)
        ]).Value;

        town.SetBoundary(ring, Osm("relation/13476401", Retrieved));

        town.Boundary.Should().Be(ring);
        town.BoundaryProvenance!.Attribution.Should().Be("© OpenStreetMap contributors");
        town.Extent.Should().Be(ring.Extent);

        town.ClearBoundary();

        town.Boundary.Should().BeNull();
        town.BoundaryProvenance.Should().BeNull();
    }

    /// <summary>
    /// The file the deployment ships, read in full. A malformed entry would otherwise surface as a
    /// seeding failure in a production log and a town with no outline.
    /// </summary>
    [Fact]
    public void The_bundled_openstreetmap_file_is_readable_and_every_outline_carries_its_licence()
    {
        var outlines = OsmBoundarySeeder.ReadBundled();

        outlines.Select(outline => (outline.CountryCode.Value, outline.Level, outline.Name.Value))
            .Should().BeEquivalentTo(new[]
            {
                ("GB", AdministrativeLevel.Region, "Greater London"),
                ("GB", AdministrativeLevel.Region, "Greater Manchester"),
                ("GB", AdministrativeLevel.City, "Manchester"),
                ("MU", AdministrativeLevel.District, "Plaines Wilhems"),
                ("MU", AdministrativeLevel.District, "Port Louis"),
                ("MU", AdministrativeLevel.City, "Quatre Bornes"),
                ("MU", AdministrativeLevel.City, "Curepipe")
            });

        outlines.Should().AllSatisfy(outline =>
        {
            outline.Level.Should().NotBe(AdministrativeLevel.Neighborhood,
                "no open dataset matches the catalogue's neighbourhoods exactly, and none is imported");
            outline.Provenance.Source.Should().Be("OpenStreetMap");
            outline.Provenance.Licence.Should().Be("ODbL-1.0");
            outline.Provenance.Attribution.Should().Be("© OpenStreetMap contributors");
            outline.Provenance.SourceReference.Should().StartWith("relation/");
            outline.Provenance.RetrievedOn.Should().Be(Retrieved);
            outline.Boundary.Vertices.Count.Should().BeInRange(
                GeographicBoundary.MinimumVertices, GeographicBoundary.MaximumVertices);
        });
    }

    private static BoundaryProvenance Osm(string reference, DateOnly retrievedOn) =>
        BoundaryProvenance.TryCreate(
            "OpenStreetMap", "ODbL-1.0", "© OpenStreetMap contributors", reference, retrievedOn).Value;
}
