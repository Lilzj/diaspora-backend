using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Housing.Application.Services;
using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Modules.Housing.Domain.ValueObjects;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// A property is filed where it is — 16 Sep 2026.
/// <para>
/// <b>No listing on this platform had ever been filed under an area.</b> A neighbourhood was only
/// resolved from a <c>neighborhoodAreaId</c> the client sent, and no create screen sends one, so
/// every real listing carried a country, a city name and nothing the catalogue knows. Three things
/// followed: a town search by path matched nothing, every neighbourhood's resource count read
/// zero, and <c>GET /location/map/clusters</c> aggregated a catalogue of noughts — which is why
/// the map still clusters on the device.
/// </para>
/// <para>
/// <b>The existing viewport tests could not see it.</b> Every one of them seeds listings with the
/// Peckham path already set — its own comment says "a listing filed against an area the catalogue
/// does not have contributes to nothing" — so they proved the counters worked on data the platform
/// never produced. These tests start where a real listing starts: a country, a city and a pair of
/// coordinates.
/// </para>
/// </summary>
public sealed class HousingAreaFilingTests : IdentityTestBase
{
    private const string PeckhamViewport = "51.49,51.46,-0.04,-0.10";
    private const double PeckhamLatitude = 51.4742d;
    private const double PeckhamLongitude = -0.0691d;

    public HousingAreaFilingTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task A_property_with_coordinates_and_no_named_area_is_filed_where_it_stands()
    {
        Factory.EnsureDatabaseCreated();

        using var scope = Factory.Services.CreateScope();
        var resolver = scope.ServiceProvider.GetRequiredService<HousingNeighborhoodResolver>();

        var unfiled = HousingLocationContext.Create("GB", "Greater London", "London").Value;

        unfiled.HasNeighborhood.Should().BeFalse("this is every listing the platform holds today");

        var filed = await resolver.ResolveFromPointAsync(
            unfiled, PeckhamLatitude, PeckhamLongitude);

        filed.HasNeighborhood.Should().BeTrue();
        filed.NeighborhoodName.Should().Be("Peckham");
        filed.AreaPath.Should().NotBeNullOrWhiteSpace(
            "the path is what the counters aggregate by");
    }

    /// <summary>
    /// Nearest, not nearest-at-any-distance. A property in the sea is in no neighbourhood, and
    /// filing it under whichever node happened to be closest would put it on a map in a place
    /// nobody listed it in.
    /// </summary>
    [Fact]
    public async Task A_property_far_from_every_known_neighbourhood_stays_unfiled()
    {
        Factory.EnsureDatabaseCreated();

        using var scope = Factory.Services.CreateScope();
        var resolver = scope.ServiceProvider.GetRequiredService<HousingNeighborhoodResolver>();

        var unfiled = HousingLocationContext.Create("GB", "Greater London", "London").Value;

        // The middle of the North Sea.
        var filed = await resolver.ResolveFromPointAsync(unfiled, 56.0d, 3.0d);

        filed.HasNeighborhood.Should().BeFalse();
    }

    /// <summary>A property with no coordinates at all cannot be placed, and is not guessed at.</summary>
    [Fact]
    public async Task A_property_with_no_coordinates_stays_unfiled()
    {
        Factory.EnsureDatabaseCreated();

        using var scope = Factory.Services.CreateScope();
        var resolver = scope.ServiceProvider.GetRequiredService<HousingNeighborhoodResolver>();

        var unfiled = HousingLocationContext.Create("GB", "Greater London", "London").Value;

        (await resolver.ResolveFromPointAsync(unfiled, null, null))
            .HasNeighborhood.Should().BeFalse();
    }

    /// <summary>
    /// The payoff, and the assertion the viewport suite cannot make: a listing that began with
    /// nothing but coordinates reaches the map's cluster counts.
    /// </summary>
    [Fact]
    public async Task A_property_filed_from_its_coordinates_is_counted_by_the_cluster_route()
    {
        var run = await SeedFiledFromCoordinatesAsync();

        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var page = await IdentityApiClient.ReadDataAsync<ClusterPageShape>(
            await HttpClient.GetAsync(
                $"/api/v1/location/map/clusters?bounds={PeckhamViewport}&resourceTypes=housing"));

        var peckham = page.Clusters.Should().ContainSingle(cluster => cluster.Name == "Peckham").Subject;

        peckham.Counts.Should().ContainKey("housing");
        peckham.Counts["housing"].Should().BeGreaterThan(0, $"the listing seeded for {run} is in it");
    }

    /// <summary>
    /// A listing built the way the create handler builds one: a country, a city, coordinates, and
    /// the resolver deciding where that is. Nothing here supplies a path.
    /// </summary>
    private async Task<string> SeedFiledFromCoordinatesAsync()
    {
        Factory.EnsureDatabaseCreated();

        var run = Guid.NewGuid().ToString("N")[..8];
        var now = DateTimeOffset.UtcNow;

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();
        var resolver = scope.ServiceProvider.GetRequiredService<HousingNeighborhoodResolver>();

        var place = await resolver.ResolveFromPointAsync(
            HousingLocationContext.Create("GB", "Greater London", "London").Value,
            PeckhamLatitude,
            PeckhamLongitude);

        place.HasNeighborhood.Should().BeTrue("otherwise this test would prove nothing");

        var listing = HousingListing.Draft(
            HousingListingId.New(),
            LandlordProfileId.New(),
            UserId.New(),
            HousingTitle.Create($"Filed from its own address {run}").Value,
            HousingSlug.Create($"filed-from-address-{run}"),
            HousingDescription.Create("A property placed by its coordinates, as the app creates one.").Value,
            "apartment",
            HousingPrice.Create(900m, "GBP", RentPeriod.PerMonth, null).Value,
            HousingCapacity.Create(1, 1m, maximumOccupants: 2).Value,
            HousingAvailability.Create(null, totalUnits: 1).Value,
            place,
            PropertyAddress.Create(latitude: PeckhamLatitude, longitude: PeckhamLongitude).Value,
            StayPolicy.Create(
                acceptsShortStay: false, acceptsLongStay: true, isSharedAccommodation: false).Value,
            now).Value;

        listing.AttachMedia(Guid.NewGuid(), HousingMediaRole.Cover, null, now);
        listing.Publish(now).IsSuccess.Should().BeTrue();

        dbContext.Set<HousingListing>().Add(listing);
        await dbContext.SaveChangesAsync();

        return run;
    }

    private sealed record ClusterPageShape
    {
        public List<ClusterShape> Clusters { get; init; } = [];
    }

    private sealed record ClusterShape
    {
        public string Name { get; init; } = string.Empty;

        public Dictionary<string, int> Counts { get; init; } = [];

        public int Total { get; init; }
    }
}
