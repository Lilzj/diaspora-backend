using System.Net;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Location.Domain.Areas;
using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.Modules.Location.Integration;
using Diaspora.Modules.Location.Persistence.Seeding;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// Places have outlines where OpenStreetMap publishes one that matches exactly — 17 Sep 2026.
/// <para>
/// FRONTEND-MAPS-GAP-016 asked for boundary geometry and "a boundarySource and a licence
/// statement". The catalogue could store a ring and serve it, and held none; it recorded no source
/// for one either. These tests hold the import (exact matches only, never over somebody else's
/// outline), the credit on every read, the map route that draws them, and the one place outlines
/// change behaviour: filing a row under the place its pin is in.
/// </para>
/// </summary>
public sealed class AreaBoundaryTests : IdentityTestBase
{
    private const string OsmCredit = "© OpenStreetMap contributors";

    /// <summary>The north of Quatre Bornes and the south of Rose Hill, either side of the town line.</summary>
    private const string RoseHillQuatreBornesViewport = "-20.23,-20.27,57.50,57.46";

    /// <summary>
    /// Inside the Quatre Bornes outline, 1.2 km from Rose Hill's catalogue centre and 1.4 km from
    /// Quatre Bornes'. Nearest-centre filing put a flat here in the wrong town.
    /// </summary>
    private const double NorthQuatreBornesLatitude = -20.252d;
    private const double NorthQuatreBornesLongitude = 57.478d;

    public AreaBoundaryTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ── The import ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_catalogue_places_with_an_exact_openstreetmap_match_have_its_outline_and_its_licence()
    {
        var outlined = await OutlinedAreasAsync();

        outlined.Select(area => (area.CountryCode.Value, area.Level, area.Name.Value))
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

        outlined.Should().AllSatisfy(area =>
        {
            area.BoundaryProvenance.Should().NotBeNull();
            area.BoundaryProvenance!.Source.Should().Be("OpenStreetMap");
            area.BoundaryProvenance.Licence.Should().Be("ODbL-1.0");
            area.BoundaryProvenance.Attribution.Should().Be(OsmCredit);
            area.Extent.Should().Be(area.Boundary!.Extent, "an outline's extent is derived from it");
        });

        // "Port Louis" is a district and a city. Only the district's OSM relation was imported, and
        // the city must not have picked it up by name.
        (await AreaAsync("Port Louis", AdministrativeLevel.City)).Boundary.Should().BeNull();
    }

    [Fact]
    public async Task Seeding_again_leaves_an_outline_from_elsewhere_alone_and_refreshes_an_older_extract()
    {
        var curepipe = await AreaAsync("Curepipe", AdministrativeLevel.City);
        var quatreBornes = await AreaAsync("Quatre Bornes", AdministrativeLevel.City);
        var bundledQuatreBornes = quatreBornes.Boundary!;

        try
        {
            await UpdateAreaAsync(curepipe.Id, area => area.SetBoundary(
                SquareAround(area.Center!),
                BoundaryProvenance.TryCreate(
                    "Municipal survey", "CC-BY-4.0", "Curepipe Municipal Council", "survey/7",
                    new DateOnly(2026, 1, 5)).Value));

            await UpdateAreaAsync(quatreBornes.Id, area => area.SetBoundary(
                SquareAround(area.Center!),
                BoundaryProvenance.TryCreate(
                    "OpenStreetMap", "ODbL-1.0", OsmCredit, "relation/13476401",
                    new DateOnly(2025, 3, 1)).Value));

            await SeedCatalogueAgainAsync();

            var surveyed = await AreaAsync("Curepipe", AdministrativeLevel.City);
            surveyed.BoundaryProvenance!.Source.Should().Be("Municipal survey",
                "an import never replaces an outline somebody else supplied");
            surveyed.Boundary!.Vertices.Should().HaveCount(4);

            var refreshed = await AreaAsync("Quatre Bornes", AdministrativeLevel.City);
            refreshed.BoundaryProvenance!.RetrievedOn.Should().Be(new DateOnly(2026, 9, 17),
                "a newer extract of the same relation replaces the older one");
            refreshed.Boundary.Should().Be(bundledQuatreBornes);

            await SeedCatalogueAgainAsync();

            (await AreaAsync("Quatre Bornes", AdministrativeLevel.City)).Boundary
                .Should().Be(bundledQuatreBornes, "the same extract twice is not a change");
        }
        finally
        {
            // The class shares one database. Put Curepipe back the way the seeder leaves it.
            await UpdateAreaAsync(curepipe.Id, area => area.ClearBoundary());
            await SeedCatalogueAgainAsync();
        }

        (await AreaAsync("Curepipe", AdministrativeLevel.City)).BoundaryProvenance!.Source
            .Should().Be("OpenStreetMap");
    }

    // ── The reads ────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task An_area_read_says_whose_outline_it_is()
    {
        var quatreBornes = await AreaAsync("Quatre Bornes", AdministrativeLevel.City);

        await SignInAsync();

        var detail = await IdentityApiClient.ReadDataAsync<AreaDetailShape>(
            await HttpClient.GetAsync($"/api/v1/location/areas/{quatreBornes.Id.Value}"));

        detail.HasBoundary.Should().BeTrue();
        detail.Boundary!.Source.Should().Be("OpenStreetMap");
        detail.Boundary.Licence.Should().Be("ODbL-1.0");
        detail.Boundary.Attribution.Should().Be(OsmCredit);
        detail.Boundary.SourceReference.Should().Be("relation/13476401");
        detail.Boundary.RetrievedOn.Should().Be("2026-09-17");
        detail.Boundary.Vertices.Should().HaveCount(quatreBornes.Boundary!.Vertices.Count);
    }

    [Fact]
    public async Task A_map_gets_the_outlines_its_viewport_overlaps_broadest_first_with_the_credit_once()
    {
        await SignInAsync();

        var page = await IdentityApiClient.ReadDataAsync<BoundaryPageShape>(
            await HttpClient.GetAsync($"/api/v1/location/map/boundaries?bounds={RoseHillQuatreBornesViewport}"));

        // The Quatre Bornes line is in view although the town's centre is south of the viewport;
        // Port Louis and Curepipe are near and not in it.
        page.Boundaries.Select(boundary => (boundary.Name, boundary.Level))
            .Should().Equal(("Plaines Wilhems", "District"), ("Quatre Bornes", "City"));

        page.Boundaries.Should().AllSatisfy(boundary =>
        {
            boundary.CountryCode.Should().Be("MU");
            boundary.Boundary.Attribution.Should().Be(OsmCredit);
            boundary.Boundary.Vertices.Count.Should().BeGreaterThan(3);
        });

        page.Attributions.Should().Equal(OsmCredit);
        page.IsComplete.Should().BeTrue();
    }

    /// <summary>
    /// <b>A neighbourhood hub draws the town's line, under the town's name</b> — 17 Sep 2026.
    /// <para>
    /// No open dataset draws our neighbourhoods. London's are electoral wards, Manchester's are a ward
    /// and a district, and Mauritius's are a station and localities; the import takes exact matches
    /// only, so a neighbourhood has no outline and keeps its approximate circle. The user decided the
    /// hub should show the containing town's real outline beside it instead, named as the town's — so
    /// this is a separate field, never a substitute for the neighbourhood's own.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_neighbourhood_with_no_outline_carries_its_towns_under_the_towns_name()
    {
        var quatreBornes = await AreaAsync("Quatre Bornes", AdministrativeLevel.City);

        await SignInAsync();

        var hub = await IdentityApiClient.ReadDataAsync<HubShape>(
            await HttpClient.GetAsync("/api/v1/location/neighborhoods/quatre-bornes-center"));

        hub.Boundary.Should().BeNull("nothing publishes an outline of this neighbourhood");

        hub.ContainingOutline!.AreaId.Should().Be(quatreBornes.Id.Value);
        hub.ContainingOutline.Name.Should().Be("Quatre Bornes");
        hub.ContainingOutline.Level.Should().Be("City");
        hub.ContainingOutline.Boundary.Attribution.Should().Be(OsmCredit);
        hub.ContainingOutline.Boundary.Vertices.Should()
            .HaveCount(quatreBornes.Boundary!.Vertices.Count);

        // Rose Hill has no outline either, so the nearest one above it is the district's — the
        // nearest ancestor that has one, not the first or the broadest.
        var roseHill = await IdentityApiClient.ReadDataAsync<HubShape>(
            await HttpClient.GetAsync("/api/v1/location/neighborhoods/rose-hill-central"));

        roseHill.ContainingOutline!.Name.Should().Be("Plaines Wilhems");
        roseHill.ContainingOutline.Level.Should().Be("District");
    }

    [Fact]
    public async Task A_viewport_too_wide_for_outlines_is_answered_empty_and_marked_incomplete()
    {
        await SignInAsync();

        var page = await IdentityApiClient.ReadDataAsync<BoundaryPageShape>(
            await HttpClient.GetAsync("/api/v1/location/map/boundaries?bounds=60,40,10,-10"));

        page.Boundaries.Should().BeEmpty();
        page.Attributions.Should().BeEmpty();
        page.IsComplete.Should().BeFalse("empty because the view is too wide, not because nothing is there");
    }

    [Fact]
    public async Task Outlines_on_the_map_are_for_members()
    {
        Factory.EnsureDatabaseCreated();

        var response = await Factory.CreateClient().GetAsync(
            $"/api/v1/location/map/boundaries?bounds={RoseHillQuatreBornesViewport}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Filing ───────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_pin_inside_a_town_outline_is_filed_in_that_town_although_the_next_town_centre_is_nearer()
    {
        var placement = await LocateAsync(new AreaFilingRequest
        {
            CountryCode = "MU",
            Latitude = NorthQuatreBornesLatitude,
            Longitude = NorthQuatreBornesLongitude
        });

        placement.Should().NotBeNull();
        placement!.Name.Should().Be("Quatre Bornes Center");
        placement.Path.Should().StartWith((await AreaAsync("Quatre Bornes", AdministrativeLevel.City)).Path.Value + "/");
    }

    [Fact]
    public async Task A_town_the_row_names_still_wins_over_the_outline_its_pin_is_in()
    {
        var placement = await LocateAsync(new AreaFilingRequest
        {
            CountryCode = "MU",
            CityName = "Rose Hill",
            Latitude = NorthQuatreBornesLatitude,
            Longitude = NorthQuatreBornesLongitude
        });

        placement!.Name.Should().Be("Rose Hill Central");
    }

    /// <summary>
    /// Rose Hill's centre is 2.5 km from Quatre Bornes Center and outside the Quatre Bornes line. A
    /// row that says Quatre Bornes is filed in Quatre Bornes; it is not filed in one of Quatre
    /// Bornes' neighbourhoods on the strength of a pin that is not in the town at all.
    /// </summary>
    [Fact]
    public async Task A_pin_outside_the_named_towns_outline_does_not_refine_it_into_a_neighbourhood()
    {
        var roseHill = await AreaAsync("Rose Hill", AdministrativeLevel.City);

        var placement = await LocateAsync(new AreaFilingRequest
        {
            CountryCode = "MU",
            CityName = "Quatre Bornes",
            Latitude = roseHill.Center!.Latitude,
            Longitude = roseHill.Center.Longitude
        });

        placement!.Name.Should().Be("Quatre Bornes");
        placement.Level.Should().Be(AdministrativeLevel.City);
    }

    // ── Plumbing ─────────────────────────────────────────────────────────────────────────────────

    private async Task SignInAsync()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);
    }

    private IServiceScope CreateScope()
    {
        Factory.EnsureDatabaseCreated();

        return Factory.Services.CreateScope();
    }

    private async Task<AreaPlacement?> LocateAsync(AreaFilingRequest request)
    {
        using var scope = CreateScope();

        return await scope.ServiceProvider.GetRequiredService<IAreaLocator>().LocateAsync(request);
    }

    private async Task<List<AdministrativeArea>> OutlinedAreasAsync()
    {
        using var scope = CreateScope();

        var areas = await scope.ServiceProvider.GetRequiredService<DiasporaDbContext>()
            .Set<AdministrativeArea>().AsNoTracking()
            .ToListAsync();

        return areas.Where(area => area.Boundary is not null).ToList();
    }

    private async Task<AdministrativeArea> AreaAsync(string name, AdministrativeLevel level)
    {
        using var scope = CreateScope();

        var areas = await scope.ServiceProvider.GetRequiredService<DiasporaDbContext>()
            .Set<AdministrativeArea>().AsNoTracking()
            .Where(area => area.Level == level)
            .ToListAsync();

        return areas.Single(area => area.Name.Value == name);
    }

    private async Task UpdateAreaAsync(AreaId id, Action<AdministrativeArea> change)
    {
        using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var area = await dbContext.Set<AdministrativeArea>().SingleAsync(node => node.Id == id);
        change(area);

        await dbContext.SaveChangesAsync();
    }

    private async Task SeedCatalogueAgainAsync()
    {
        Factory.EnsureDatabaseCreated();

        await Factory.Services.GetRequiredService<LocationCatalogSeedRunner>().SeedAsync();
    }

    private static GeographicBoundary SquareAround(GeoCoordinate centre) =>
        GeographicBoundary.TryCreate(
        [
            GeoCoordinate.Create(centre.Latitude + 0.01d, centre.Longitude - 0.01d),
            GeoCoordinate.Create(centre.Latitude + 0.01d, centre.Longitude + 0.01d),
            GeoCoordinate.Create(centre.Latitude - 0.01d, centre.Longitude + 0.01d),
            GeoCoordinate.Create(centre.Latitude - 0.01d, centre.Longitude - 0.01d)
        ]).Value;

    private sealed record HubShape
    {
        public BoundaryShape? Boundary { get; init; }

        public ContainingOutlineShape? ContainingOutline { get; init; }
    }

    private sealed record ContainingOutlineShape
    {
        public Guid AreaId { get; init; }

        public string Name { get; init; } = "";

        public string Level { get; init; } = "";

        public BoundaryShape Boundary { get; init; } = new();
    }

    private sealed record AreaDetailShape
    {
        public bool HasBoundary { get; init; }

        public BoundaryShape? Boundary { get; init; }
    }

    private sealed record BoundaryShape
    {
        public List<VertexShape> Vertices { get; init; } = [];

        public string? Source { get; init; }

        public string? Licence { get; init; }

        public string? Attribution { get; init; }

        public string? SourceReference { get; init; }

        public string? RetrievedOn { get; init; }
    }

    private sealed record VertexShape(double Latitude, double Longitude);

    private sealed record BoundaryPageShape
    {
        public List<MapBoundaryShape> Boundaries { get; init; } = [];

        public List<string> Attributions { get; init; } = [];

        public bool IsComplete { get; init; }
    }

    private sealed record MapBoundaryShape
    {
        public string Name { get; init; } = "";

        public string Level { get; init; } = "";

        public string CountryCode { get; init; } = "";

        public BoundaryShape Boundary { get; init; } = new();
    }
}
