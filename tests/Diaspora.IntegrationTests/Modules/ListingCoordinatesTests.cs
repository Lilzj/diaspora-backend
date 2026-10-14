using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// Every listing carries the owner's pin (14 Oct 2026). The place catalogue is being retired in
/// favour of coordinates, a country and the owner's typed town; jobs, marketplace listings, guides
/// and organizations stored no coordinates at all. This is the additive step: each accepts an
/// optional latitude and longitude on create and update and hands them back. Nothing searches by
/// them yet.
/// </summary>
[Collection(AssortedCollection.Name)]
public sealed class ListingCoordinatesTests : PooledIdentityTestBase
{
    // Grand Baie, Mauritius, then Port Louis.
    private const double FirstLatitude = -20.0117863;
    private const double FirstLongitude = 57.5935099;
    private const double SecondLatitude = -20.1608912;
    private const double SecondLongitude = 57.5012222;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public ListingCoordinatesTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task A_job_keeps_the_pin_it_was_created_with_and_moves_when_edited()
    {
        await SignInAsync();

        var employer = await DataAsync(await HttpClient.PostAsJsonAsync(
            "/api/v1/jobs/employers",
            new { displayName = "Pinned Employer", organizationType = "Company", countryCode = "MU" },
            JsonOptions));
        var employerId = employer.GetProperty("employerId").GetGuid();

        var created = await DataAsync(await HttpClient.PostAsJsonAsync(
            "/api/v1/jobs/",
            new
            {
                employerId,
                title = "Line Cook",
                description = "A line cook role, described at enough length to be valid.",
                categoryKey = "healthcare",
                jobType = "FullTime",
                experienceLevel = "Mid",
                countryCode = "MU",
                cityName = "Grand Baie",
                latitude = FirstLatitude,
                longitude = FirstLongitude
            },
            JsonOptions));

        ShouldBeAt(created.GetProperty("location"), FirstLatitude, FirstLongitude);

        var jobId = created.GetProperty("jobId").GetGuid();

        var updated = await DataAsync(await HttpClient.PutAsJsonAsync(
            $"/api/v1/jobs/{jobId}",
            new
            {
                title = "Line Cook",
                description = "A line cook role, described at enough length to be valid.",
                jobType = "FullTime",
                experienceLevel = "Mid",
                countryCode = "MU",
                cityName = "Port Louis",
                latitude = SecondLatitude,
                longitude = SecondLongitude
            },
            JsonOptions));

        ShouldBeAt(updated.GetProperty("location"), SecondLatitude, SecondLongitude);

        // An editor that has never heard of coordinates must not wipe the pin.
        var unaware = await DataAsync(await HttpClient.PutAsJsonAsync(
            $"/api/v1/jobs/{jobId}",
            new
            {
                title = "Line Cook",
                description = "A line cook role, described at enough length to be valid.",
                jobType = "FullTime",
                experienceLevel = "Mid",
                countryCode = "MU",
                cityName = "Port Louis"
            },
            JsonOptions));

        ShouldBeAt(unaware.GetProperty("location"), SecondLatitude, SecondLongitude);
    }

    [Fact]
    public async Task A_marketplace_listing_keeps_the_pin_it_was_created_with_and_moves_when_edited()
    {
        await SignInAsync();

        var seller = await HttpClient.PostAsJsonAsync(
            "/api/v1/marketplace/sellers",
            new { displayName = "Pinned Seller", countryCode = "MU", kind = "Individual" },
            JsonOptions);

        seller.StatusCode.Should().Be(HttpStatusCode.Created, await seller.Content.ReadAsStringAsync());

        var created = await DataAsync(await HttpClient.PostAsJsonAsync(
            "/api/v1/marketplace/listings",
            new
            {
                title = $"Child's bicycle {Guid.NewGuid():N}",
                description = "Outgrown, rides well. Collection from Grand Baie.",
                categoryKey = "bicycles",
                countryCode = "MU",
                cityName = "Grand Baie",
                condition = "Good",
                offersPickup = true,
                latitude = FirstLatitude,
                longitude = FirstLongitude
            },
            JsonOptions));

        ShouldBeAt(created.GetProperty("location"), FirstLatitude, FirstLongitude);

        var listingId = created.GetProperty("listingId").GetGuid();

        var updated = await DataAsync(await HttpClient.PutAsJsonAsync(
            $"/api/v1/marketplace/listings/{listingId}",
            new
            {
                title = "Child's bicycle",
                description = "Outgrown, rides well. Collection from Port Louis.",
                condition = "Good",
                countryCode = "MU",
                cityName = "Port Louis",
                offersPickup = true,
                latitude = SecondLatitude,
                longitude = SecondLongitude
            },
            JsonOptions));

        ShouldBeAt(updated.GetProperty("location"), SecondLatitude, SecondLongitude);
    }

    [Fact]
    public async Task A_guide_keeps_the_pin_it_was_created_with_and_moves_when_edited()
    {
        await SignInAsync();

        var created = await DataAsync(await HttpClient.PostAsJsonAsync(
            "/api/v1/guides",
            new
            {
                title = "Registering with a doctor in Grand Baie",
                categoryKey = "immigration",
                language = "en",
                countryCode = "MU",
                cityName = "Grand Baie",
                latitude = FirstLatitude,
                longitude = FirstLongitude
            },
            JsonOptions));

        ShouldBeAt(created, FirstLatitude, FirstLongitude);

        var guideId = created.GetProperty("guideId").GetGuid();

        var updated = await DataAsync(await HttpClient.PutAsJsonAsync(
            $"/api/v1/guides/{guideId}",
            new
            {
                title = "Registering with a doctor in Port Louis",
                countryCode = "MU",
                cityName = "Port Louis",
                latitude = SecondLatitude,
                longitude = SecondLongitude
            },
            JsonOptions));

        ShouldBeAt(updated, SecondLatitude, SecondLongitude);
    }

    [Fact]
    public async Task An_organization_keeps_the_pin_it_was_created_with_and_moves_when_edited()
    {
        await SignInAsync();

        var created = await DataAsync(await HttpClient.PostAsJsonAsync(
            "/api/v1/community-life/organizations",
            new
            {
                name = $"Pinned Association {Guid.NewGuid():N}",
                summary = "A body created by the integration suite.",
                organizationTypeKey = "cultural-association",
                countryCode = "MU",
                cityName = "Grand Baie",
                latitude = FirstLatitude,
                longitude = FirstLongitude
            },
            JsonOptions));

        ShouldBeAt(created.GetProperty("location"), FirstLatitude, FirstLongitude);

        var organizationId = created.GetProperty("organizationId").GetGuid();

        var updated = await DataAsync(await HttpClient.PatchAsJsonAsync(
            $"/api/v1/community-life/organizations/{organizationId}",
            new { latitude = SecondLatitude, longitude = SecondLongitude },
            JsonOptions));

        ShouldBeAt(updated.GetProperty("location"), SecondLatitude, SecondLongitude);
    }

    [Fact]
    public async Task Half_a_pin_or_one_off_the_planet_is_refused()
    {
        await SignInAsync();

        var half = await HttpClient.PostAsJsonAsync(
            "/api/v1/guides",
            new
            {
                title = "A guide with half a pin",
                categoryKey = "immigration",
                language = "en",
                countryCode = "MU",
                latitude = FirstLatitude
            },
            JsonOptions);

        half.StatusCode.Should().Be(HttpStatusCode.BadRequest, await half.Content.ReadAsStringAsync());

        var offPlanet = await HttpClient.PostAsJsonAsync(
            "/api/v1/community-life/organizations",
            new
            {
                name = $"Off Planet Association {Guid.NewGuid():N}",
                summary = "A body created by the integration suite.",
                organizationTypeKey = "cultural-association",
                countryCode = "MU",
                latitude = 91.0,
                longitude = FirstLongitude
            },
            JsonOptions);

        offPlanet.StatusCode.Should().Be(
            HttpStatusCode.BadRequest, await offPlanet.Content.ReadAsStringAsync());
    }

    private async Task SignInAsync()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);
    }

    private static void ShouldBeAt(JsonElement holder, double latitude, double longitude)
    {
        holder.GetProperty("latitude").GetDouble().Should().BeApproximately(latitude, 1e-6);
        holder.GetProperty("longitude").GetDouble().Should().BeApproximately(longitude, 1e-6);
    }

    private static async Task<JsonElement> DataAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue(body);

        using var document = JsonDocument.Parse(body);

        return document.RootElement.GetProperty("data").Clone();
    }
}
