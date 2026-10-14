using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Modules.Housing.Domain.ValueObjects;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using ProviderDomain = Diaspora.Modules.Providers.Domain;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// Phase 2B of retiring the place catalogue (14 Oct 2026), end to end.
/// <list type="bullet">
/// <item>Map clusters and the heat layer are square grid cells over each row's own coordinates —
/// in Kenya, which the catalogue never held, as much as anywhere.</item>
/// <item>Place outlines are retired: the route answers an empty page with a reason.</item>
/// <item>A neighbourhood-only post reaches readers whose active location is within the radius of
/// its pin, and nobody further.</item>
/// </list>
/// </summary>
[Collection(AssortedCollection.Name)]
public sealed class MapGridAndAudienceTests : PooledIdentityTestBase
{
    // A one-degree square of central Kenya, away from every other fixture, cut by a 0.5° grid into
    // four cells: cell A (south-west corner -2.5, 37.5) holds three pinned rows, cell B (-3, 38) one.
    private const string Viewport = "-2,-3,38.5,37.5";
    private const string HeatViewport = "north=-2&south=-3&east=38.5&west=37.5";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public MapGridAndAudienceTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Clusters_are_grid_cells_over_the_rows_own_coordinates_with_counts_per_type()
    {
        await SeedKenyaAsync();

        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var page = await DataAsync(await HttpClient.GetAsync(
            $"/api/v1/location/map/clusters?bounds={Viewport}&cellSizeKm=50"
            + "&resourceTypes=housing,providers&countryCode=KE"));

        page.GetProperty("aggregation").GetString().Should().Be("grid");
        page.GetProperty("cellSizeDegrees").GetDouble().Should().Be(0.5);
        page.GetProperty("truncated").GetBoolean().Should().BeFalse();

        var clusters = page.GetProperty("clusters").EnumerateArray().ToList();

        clusters.Should().HaveCount(2, "two cells hold pinned rows; the unpinned home is on no map");

        var a = clusters[0];
        a.GetProperty("total").GetInt32().Should().Be(3);
        a.GetProperty("counts").GetProperty("housing").GetInt32().Should().Be(2);
        a.GetProperty("counts").GetProperty("providers").GetInt32().Should().Be(1);
        a.GetProperty("level").GetString().Should().Be("Grid");
        a.GetProperty("cellKey").GetString().Should().NotBeNullOrWhiteSpace();
        a.GetProperty("slug").GetString().Should().Be(a.GetProperty("cellKey").GetString());
        a.GetProperty("name").GetString().Should().StartWith("Area around");
        a.GetProperty("areaId").GetGuid().Should().NotBe(Guid.Empty);

        var bounds = a.GetProperty("bounds");
        bounds.GetProperty("south").GetDouble().Should().Be(-2.5);
        bounds.GetProperty("north").GetDouble().Should().Be(-2);
        bounds.GetProperty("west").GetDouble().Should().Be(37.5);
        bounds.GetProperty("east").GetDouble().Should().Be(38);

        // Three rows: the marker is their mean, inside the cell.
        a.GetProperty("latitude").GetDouble().Should().BeInRange(-2.5, -2);
        a.GetProperty("longitude").GetDouble().Should().BeInRange(37.5, 38);

        var b = clusters[1];
        b.GetProperty("total").GetInt32().Should().Be(1);
        b.GetProperty("counts").GetProperty("housing").GetInt32().Should().Be(1);
        b.GetProperty("counts").GetProperty("providers").GetInt32().Should().Be(
            0, "Providers answered, and has nothing in this cell");

        // One row: the marker is the cell's centre, never the row's own pin.
        b.GetProperty("latitude").GetDouble().Should().Be(-2.75);
        b.GetProperty("longitude").GetDouble().Should().Be(38.25);
    }

    [Fact]
    public async Task The_heat_layer_is_built_from_the_same_grid_cells()
    {
        await SeedKenyaAsync();

        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        // City granularity over a one-degree viewport: about six cells across, so 0.25° cells.
        var layer = await DataAsync(await HttpClient.GetAsync(
            $"/api/v1/geo/heatmap?layer=housing-availability&{HeatViewport}&level=City&countryCode=KE"));

        layer.GetProperty("cellSizeDegrees").GetDouble().Should().Be(0.25);

        var points = layer.GetProperty("points").EnumerateArray().ToList();

        points.Select(point => point.GetProperty("rawValue").GetInt32())
            .Should().BeEquivalentTo([2, 1], "housing only: two homes in one cell, one in another");

        points.Should().AllSatisfy(point =>
        {
            point.GetProperty("areaName").GetString().Should().StartWith("Area around");
            point.GetProperty("cellKey").GetString().Should().NotBeNullOrWhiteSpace();
            point.GetProperty("intensity").GetDouble().Should().BeInRange(0, 1);
        });
    }

    [Fact]
    public async Task Place_outlines_are_retired_and_say_so()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var page = await DataAsync(await HttpClient.GetAsync(
            "/api/v1/location/map/boundaries?bounds=-20.1,-20.3,57.6,57.4"));

        page.GetProperty("boundaries").GetArrayLength().Should().Be(0);
        page.GetProperty("attributions").GetArrayLength().Should().Be(0);
        page.GetProperty("reason").GetString().Should().Be("outlines-retired");

        // Still a validated route: a malformed viewport is refused as one.
        var malformed = await HttpClient.GetAsync("/api/v1/location/map/boundaries?bounds=nonsense");
        malformed.IsSuccessStatusCode.Should().BeFalse();
    }

    [Fact]
    public async Task A_neighbourhood_only_post_reaches_readers_within_its_radius_and_nobody_further()
    {
        // The post's pin: Mombasa Old Town.
        const double PostLatitude = -4.0610;
        const double PostLongitude = 39.6770;

        var (_, author) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(author.Tokens.AccessToken);

        var tag = Guid.NewGuid().ToString("N")[..8];

        var created = await DataAsync(await HttpClient.PostAsJsonAsync(
            "/api/v1/community/posts",
            new
            {
                body = $"Street party on our road this Saturday {tag}",
                category = "Traffic",
                urgency = "Normal",
                visibility = "Neighborhood",
                audienceAreaId = Guid.NewGuid(),
                countryCode = "KE",
                cityName = "Mombasa",
                latitude = PostLatitude,
                longitude = PostLongitude
            },
            JsonOptions));

        var postId = created.GetProperty("postId").GetGuid();

        // About a kilometre away, across the island.
        var (_, near) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(near.Tokens.AccessToken);
        await SetActiveLocationAsync(-4.0530, 39.6700);

        (await HttpClient.GetAsync($"/api/v1/community/posts/{postId}"))
            .StatusCode.Should().Be(HttpStatusCode.OK, "a reader 1 km away is in the neighbourhood");

        (await FeedIdsAsync($"/api/v1/community/feeds/nearby?limit=50&latitude={PostLatitude}&longitude={PostLongitude}&radiusKm=10"))
            .Should().Contain(postId);

        // Nyali, about seven kilometres up the coast: the same town, not the same neighbourhood.
        var (_, far) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(far.Tokens.AccessToken);
        await SetActiveLocationAsync(-4.0100, 39.7200);

        (await HttpClient.GetAsync($"/api/v1/community/posts/{postId}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound, "beyond the radius the post does not exist");

        (await FeedIdsAsync($"/api/v1/community/feeds/nearby?limit=50&latitude={PostLatitude}&longitude={PostLongitude}&radiusKm=10"))
            .Should().NotContain(postId, "a feed centred on the post does not widen its audience");

        // A reader who has set no location at all is not near anything.
        var (_, nowhere) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(nowhere.Tokens.AccessToken);

        (await HttpClient.GetAsync($"/api/v1/community/posts/{postId}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        // The author always sees their own post.
        Api.Authenticate(author.Tokens.AccessToken);

        (await HttpClient.GetAsync($"/api/v1/community/posts/{postId}"))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var stored = await dbContext.Set<Diaspora.Modules.Community.Domain.Posts.CommunityPost>()
            .FindAsync(Diaspora.Modules.Community.Domain.Identifiers.CommunityPostId.From(postId));

        stored!.Audience.AreaId.Should().BeNull("an area id from an older app build is not stored");
    }

    // ── Seeding ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Two homes and a provider in cell A, one home in cell B, and a home with no pin. Written
    /// straight to the store, the way the coordinate discovery tests seed.
    /// </summary>
    private async Task SeedKenyaAsync()
    {
        Factory.EnsureDatabaseCreated();

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        // Idempotent across the two tests that share the store: the region is this class's own.
        if (dbContext.Set<HousingListing>().Any(listing => listing.Location.CityName == "Grid Cell A"))
        {
            return;
        }

        var tag = Guid.NewGuid().ToString("N")[..8];
        var landlordId = LandlordProfileId.New();
        var ownerId = UserId.New();
        var now = DateTimeOffset.UtcNow;

        (string Town, double? Latitude, double? Longitude)[] homes =
        [
            ("Grid Cell A", -2.3000, 37.7000),
            ("Grid Cell A", -2.3200, 37.7200),
            ("Grid Cell B", -2.7500, 38.2500),
            ("Grid Cell A", null, null)
        ];

        var index = 0;

        foreach (var (town, latitude, longitude) in homes)
        {
            var listing = HousingListing.Draft(
                HousingListingId.New(),
                landlordId,
                ownerId,
                HousingTitle.Create($"Grid home {index} {tag}").Value,
                HousingSlug.Create($"grid-{tag}-{index}"),
                HousingDescription.Create("A home seeded for the map grid tests.").Value,
                "apartment",
                HousingPrice.Create(30_000m, "KES", RentPeriod.PerMonth, null).Value,
                HousingCapacity.Create(2, 1m, maximumOccupants: 3).Value,
                HousingAvailability.Create(totalUnits: 1).Value,
                HousingLocationContext.Create("KE", cityName: town).Value,
                latitude is null
                    ? PropertyAddress.Undisclosed
                    : PropertyAddress.Create(
                        "Kenyatta Road", null, null, latitude, longitude, AddressVisibility.NeighborhoodOnly).Value,
                StayPolicy.Create(
                    acceptsShortStay: false, acceptsLongStay: true, isSharedAccommodation: false).Value,
                now).Value;

            listing.AttachMedia(Guid.NewGuid(), HousingMediaRole.Cover, null, now).IsSuccess.Should().BeTrue();
            listing.Publish(now).IsSuccess.Should().BeTrue();

            dbContext.Set<HousingListing>().Add(listing);
            index++;
        }

        var provider = ProviderDomain.Providers.Provider.Register(
            ProviderDomain.Identifiers.ProviderId.New(),
            ProviderDomain.ValueObjects.ProviderName.Create($"Grid tailor {tag}").Value,
            ProviderDomain.ValueObjects.ProviderSlug.Create($"grid-tailor-{tag}").Value,
            ProviderDomain.Enums.ProviderType.Individual,
            ProviderDomain.Identifiers.ServiceCategoryId.New(),
            ownerId,
            ProviderDomain.Identifiers.ProviderProfileId.New(),
            ProviderDomain.Identifiers.ProviderMembershipId.New(),
            now,
            ProviderDomain.ValueObjects.ProviderLocationContext.Create(
                countryCode: "KE",
                cityName: "Grid Cell A",
                latitude: -2.3100,
                longitude: 37.7100)).Value;

        provider.AddContactMethod(
            ProviderDomain.Providers.ProviderContactMethod.Create(
                ProviderDomain.Identifiers.ProviderContactMethodId.New(),
                ProviderDomain.Enums.ContactMethodKind.Phone,
                "+254712345678",
                isPrimary: true).Value).IsSuccess.Should().BeTrue();

        provider.Publish(now).IsSuccess.Should().BeTrue();

        dbContext.Set<ProviderDomain.Providers.Provider>().Add(provider);

        await dbContext.SaveChangesAsync();
    }

    private async Task SetActiveLocationAsync(double latitude, double longitude)
    {
        var response = await HttpClient.PutAsJsonAsync(
            "/api/v1/location/me/current-location",
            new { countryCode = "KE", cityName = "Mombasa", latitude, longitude },
            JsonOptions);

        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());
    }

    private async Task<List<Guid>> FeedIdsAsync(string url)
    {
        var feed = await DataAsync(await HttpClient.GetAsync(url));

        var ids = new List<Guid>();

        foreach (var item in feed.GetProperty("items").EnumerateArray())
        {
            ids.Add(item.GetProperty("contentId").GetGuid());
        }

        return ids;
    }

    private static async Task<JsonElement> DataAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue(
            "the server answered {0}: {1}", response.StatusCode, body);

        using var document = JsonDocument.Parse(body);

        return document.RootElement.GetProperty("data").Clone();
    }
}
