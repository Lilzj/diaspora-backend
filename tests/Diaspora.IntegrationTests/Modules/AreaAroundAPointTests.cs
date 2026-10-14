using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Policies;
using Diaspora.Modules.Community.Domain.Posts;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Modules.Housing.Domain.Neighborhoods;
using Diaspora.Modules.Housing.Domain.ValueObjects;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using CL = Diaspora.Modules.CommunityLife.Domain;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// The area around a point (Phase 2A of retiring the place catalogue, owner's decision 14 Oct 2026).
/// <para>
/// <b>Nairobi is the point because the catalogue does not hold it.</b> Every figure here is computed
/// from rows' own coordinates, so an area page in a country the catalogue never had is described
/// exactly like one in a country it did. Each test uses its own suburb, several kilometres from the
/// others and from the Nairobi CBD rows other suites seed, so the counts belong to the test that
/// asserts them; the "far" rows sit in Thika, about 40 km away.
/// </para>
/// </summary>
[Collection(AssortedCollection.Name)]
public sealed class AreaAroundAPointTests : PooledIdentityTestBase
{
    // Karen, Kilimani, Lavington and Runda: Nairobi suburbs 5–13 km apart.
    private const double KarenLatitude = -1.3197;
    private const double KarenLongitude = 36.7073;
    private const double KilimaniLatitude = -1.2900;
    private const double KilimaniLongitude = 36.7850;
    private const double LavingtonLatitude = -1.2766;
    private const double LavingtonLongitude = 36.7680;
    private const double RundaLatitude = -1.2180;
    private const double RundaLongitude = 36.8130;
    private const double ThikaLatitude = -1.0333;
    private const double ThikaLongitude = 37.0693;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public AreaAroundAPointTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task An_area_in_a_country_with_no_catalogue_counts_only_the_rows_inside_its_circle()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];

        // Two homes inside (one at the point, one about 600 m away), one in Thika.
        await SeedHomeAsync(tag, 0, KarenLatitude, KarenLongitude);
        await SeedHomeAsync(tag, 1, KarenLatitude + 0.005, KarenLongitude + 0.002);
        await SeedHomeAsync(tag, 2, ThikaLatitude, ThikaLongitude);

        // One upcoming event inside, one far.
        await SeedEventAsync(tag, 0, KarenLatitude + 0.003, KarenLongitude);
        await SeedEventAsync(tag, 1, ThikaLatitude, ThikaLongitude);

        // One public update inside, one far, and a corroborated police report inside.
        await SeedPostAsync(KarenLatitude, KarenLongitude - 0.004, PostCategory.Traffic, corroborated: false);
        await SeedPostAsync(ThikaLatitude, ThikaLongitude, PostCategory.Traffic, corroborated: false);
        await SeedPostAsync(KarenLatitude - 0.002, KarenLongitude, PostCategory.Police, corroborated: true);

        await SignInAsync();

        var area = await DataAsync(await HttpClient.GetAsync(
            $"/api/v1/geo/area?latitude={KarenLatitude}&longitude={KarenLongitude}&countryCode=KE&name=Karen"));

        area.GetProperty("name").GetString().Should().Be("Karen");
        area.GetProperty("displayName").GetString().Should().Be("Karen");
        area.GetProperty("radiusKm").GetDouble().Should().Be(2d, "the default radius");
        area.GetProperty("countryCode").GetString().Should().Be("KE");

        var counts = area.GetProperty("resourceCounts");
        counts.GetProperty("housing").GetInt32().Should().Be(2, "two homes are inside the circle and one is in Thika");
        counts.GetProperty("events").GetInt32().Should().Be(1);

        // The public traffic update and the police report are both public, published posts.
        counts.GetProperty("updates").GetInt32().Should().Be(2);
        area.GetProperty("unavailableResourceTypes").GetArrayLength().Should().Be(0);

        // Every vertical with coordinates answered, a zero included: none of these seeded rows is a job
        // or a marketplace listing, and an absent key would mean "could not ask", not "none".
        counts.TryGetProperty("jobs", out _).Should().BeTrue();
        counts.TryGetProperty("marketplace", out _).Should().BeTrue();

        // No score is invented for a circle.
        area.GetProperty("overall").ValueKind.Should().Be(JsonValueKind.Null);
        area.GetProperty("dimensions").GetArrayLength().Should().Be(0);
        area.GetProperty("unavailableDimensions").GetArrayLength().Should().BeGreaterThan(0);

        // Two homes is below the minimum: "not enough data", never a guess.
        var cost = area.GetProperty("costOfLiving");
        cost.GetProperty("isAvailable").GetBoolean().Should().BeFalse();
        cost.GetProperty("unavailableReason").GetString().Should().Be("InsufficientSample");
        cost.GetProperty("sampleSize").GetInt32().Should().Be(2);
        cost.GetProperty("categories").GetArrayLength().Should().Be(0);
        cost.GetProperty("overallIndex").ValueKind.Should().Be(JsonValueKind.Null);
        cost.GetProperty("freshness").GetProperty("methodology").GetString().Should().StartWith("Not enough data");

        // Outside Great Britain the police source says "not available here" — never a score, never a zero.
        var safety = area.GetProperty("safety");
        safety.GetProperty("isAvailable").GetBoolean().Should().BeFalse();
        safety.GetProperty("score").ValueKind.Should().Be(JsonValueKind.Null);

        var safetyCounts = safety.GetProperty("counts").EnumerateArray().ToList();
        var community = safetyCounts.Single(count => count.GetProperty("kind").GetString() == "community-reports");
        community.GetProperty("total").GetInt32().Should().Be(1, "one corroborated police report is inside the circle");

        var police = safetyCounts.Single(count => count.GetProperty("kind").GetString() == "police-recorded-crime");
        police.GetProperty("isAvailable").GetBoolean().Should().BeFalse();
        police.GetProperty("unavailableReason").GetString().Should().Be("NotPublished");
        police.GetProperty("total").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task An_area_with_no_name_is_described_around_the_member_and_needs_a_country()
    {
        await SignInAsync();

        var area = await DataAsync(await HttpClient.GetAsync(
            $"/api/v1/geo/area?latitude={RundaLatitude}&longitude={RundaLongitude}&countryCode=KE&radiusKm=1.5"));

        area.GetProperty("name").ValueKind.Should().Be(JsonValueKind.Null, "the platform never names a point itself");
        area.GetProperty("displayName").GetString().Should().Be("Within 1.5 km of you");

        var noCountry = await HttpClient.GetAsync(
            $"/api/v1/geo/area?latitude={RundaLatitude}&longitude={RundaLongitude}");

        noCountry.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var tooWide = await HttpClient.GetAsync(
            $"/api/v1/geo/area?latitude={RundaLatitude}&longitude={RundaLongitude}&countryCode=KE&radiusKm=50");

        tooWide.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Two_points_are_compared_side_by_side_in_the_order_asked()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];

        await SeedHomeAsync(tag, 0, KilimaniLatitude, KilimaniLongitude);

        await SignInAsync();

        var comparison = await DataAsync(await HttpClient.GetAsync(
            "/api/v1/geo/area/compare"
            + $"?latitudeA={KilimaniLatitude}&longitudeA={KilimaniLongitude}&countryCodeA=KE&nameA=Kilimani"
            + $"&latitudeB={ThikaLatitude}&longitudeB={ThikaLongitude}&countryCodeB=KE&nameB=Thika"
            + "&radiusKm=1"));

        comparison.GetProperty("radiusKm").GetDouble().Should().Be(1d);

        var areas = comparison.GetProperty("areas").EnumerateArray().ToList();
        areas.Select(area => area.GetProperty("name").GetString()).Should().Equal("Kilimani", "Thika");
        areas[0].GetProperty("resourceCounts").GetProperty("housing").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        areas.Should().AllSatisfy(area => area.GetProperty("radiusKm").GetDouble().Should().Be(1d));
    }

    [Fact]
    public async Task An_insight_submitted_at_a_point_is_found_from_a_nearby_point_and_not_a_far_one()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var body = $"The matatu stage on James Gichuru Road is busy until late. {Guid.NewGuid():N}";

        var created = await DataAsync(await HttpClient.PostAsJsonAsync(
            "/api/v1/housing/area/insights",
            new
            {
                latitude = LavingtonLatitude,
                longitude = LavingtonLongitude,
                placeName = "Lavington",
                placeId = "ChIJ-lavington-test",
                countryCode = "ke",
                category = "Transport",
                body
            },
            JsonOptions));

        var insightId = created.GetProperty("id").GetGuid();
        created.GetProperty("areaId").ValueKind.Should().Be(JsonValueKind.Null, "a point belongs to no catalogue area");
        created.GetProperty("placeName").GetString().Should().Be("Lavington");
        created.GetProperty("countryCode").GetString().Should().Be("KE");
        created.GetProperty("status").GetString().Should().Be("Submitted");

        // Unapproved: invisible to a visitor, visible to its author.
        Api.ClearAuthentication();
        (await InsightIdsAsync(LavingtonLatitude, LavingtonLongitude)).Should().NotContain(insightId);

        Api.Authenticate(auth.Tokens.AccessToken);
        (await InsightIdsAsync(LavingtonLatitude, LavingtonLongitude, mineOnly: true)).Should().Contain(insightId);

        await ApproveAsync(insightId);
        Api.ClearAuthentication();

        // About 300 m away: inside the default 2 km.
        var nearby = await InsightsAsync(LavingtonLatitude - 0.002, LavingtonLongitude + 0.002);
        nearby.Should().ContainSingle(insight => insight.GetProperty("id").GetGuid() == insightId)
            .Which.GetProperty("distanceKm").GetDouble().Should().BeInRange(0.1, 0.6);

        (await InsightIdsAsync(ThikaLatitude, ThikaLongitude)).Should().NotContain(insightId);
    }

    [Fact]
    public async Task Area_activity_counts_events_and_organizations_by_their_own_pins()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];

        // Runda: one upcoming event and one organization inside, the same again in Thika.
        await SeedEventAsync(tag, 10, RundaLatitude + 0.004, RundaLongitude);
        await SeedEventAsync(tag, 11, ThikaLatitude, ThikaLongitude);
        await SeedOrganizationAsync(tag, 0, RundaLatitude, RundaLongitude - 0.003);
        await SeedOrganizationAsync(tag, 1, ThikaLatitude, ThikaLongitude);

        var activity = await DataAsync(await HttpClient.GetAsync(
            $"/api/v1/community-life/area/activity?latitude={RundaLatitude}&longitude={RundaLongitude}&name=Runda"));

        activity.GetProperty("displayName").GetString().Should().Be("Runda");
        activity.GetProperty("upcomingEventCount").GetInt32().Should().Be(1);
        activity.GetProperty("organizationCount").GetInt32().Should().Be(1);
        activity.GetProperty("isLowerBound").GetBoolean().Should().BeFalse();
    }

    // ── Seeding ──────────────────────────────────────────────────────────────────────────────

    private async Task SignInAsync()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);
    }

    private async Task WithDbAsync(Func<DiasporaDbContext, Task> work)
    {
        Factory.EnsureDatabaseCreated();

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        await work(dbContext);
        await dbContext.SaveChangesAsync();
    }

    private Task SeedHomeAsync(string tag, int index, double latitude, double longitude) =>
        WithDbAsync(dbContext =>
        {
            var now = DateTimeOffset.UtcNow;

            var listing = HousingListing.Draft(
                HousingListingId.New(),
                LandlordProfileId.New(),
                UserId.New(),
                HousingTitle.Create($"Two-bedroom flat {tag} {index}").Value,
                HousingSlug.Create($"area-point-{tag}-{index}"),
                HousingDescription.Create("A home seeded for the area-around-a-point tests.").Value,
                "apartment",
                HousingPrice.Create(60_000m, "KES", RentPeriod.PerMonth, null).Value,
                HousingCapacity.Create(2, 1m, maximumOccupants: 3).Value,
                HousingAvailability.Create(totalUnits: 1).Value,
                HousingLocationContext.Create("KE", cityName: "Nairobi").Value,
                PropertyAddress.Create(
                    "Ngong Road", null, null, latitude, longitude, AddressVisibility.NeighborhoodOnly).Value,
                StayPolicy.Create(
                    acceptsShortStay: false, acceptsLongStay: true, isSharedAccommodation: false).Value,
                now).Value;

            listing.AttachMedia(Guid.NewGuid(), HousingMediaRole.Cover, null, now).IsSuccess.Should().BeTrue();
            listing.Publish(now).IsSuccess.Should().BeTrue();

            dbContext.Set<HousingListing>().Add(listing);

            return Task.CompletedTask;
        });

    private Task SeedEventAsync(string tag, int index, double latitude, double longitude) =>
        WithDbAsync(dbContext =>
        {
            var now = DateTimeOffset.UtcNow;

            var communityEvent = CL.CommunityEvents.CommunityEvent.Draft(
                CL.Identifiers.CommunityEventId.New(),
                organizationId: null,
                UserId.New(),
                CL.ValueObjects.CommunityLifeTitle.Create($"Neighbours meet-up {tag} {index}").Value,
                CL.ValueObjects.CommunityLifeSlug.FromText($"area-point-{tag}-{index}"),
                CL.ValueObjects.CommunityLifeDescription
                    .Create("An event seeded for the area-around-a-point tests.", null).Value,
                "community-meeting",
                CL.ValueObjects.EventSchedule
                    .Create(now.AddDays(10), now.AddDays(10).AddHours(2), "Africa/Nairobi").Value,
                CL.ValueObjects.CommunityLifeLocationContext.Create("KE", null, "Nairobi").Value,
                CL.ValueObjects.EventVenue.Create(
                    name: "Community Hall", latitude: latitude, longitude: longitude).Value,
                CL.Enums.EventAttendanceMode.InPerson,
                now).Value;

            communityEvent.Publish(now).IsSuccess.Should().BeTrue();

            dbContext.Set<CL.CommunityEvents.CommunityEvent>().Add(communityEvent);

            return Task.CompletedTask;
        });

    private Task SeedOrganizationAsync(string tag, int index, double latitude, double longitude) =>
        WithDbAsync(dbContext =>
        {
            var now = DateTimeOffset.UtcNow;

            var organization = CL.Organizations.Organization.Draft(
                CL.Identifiers.OrganizationId.New(),
                UserId.New(),
                CL.ValueObjects.OrganizationName.Create($"Residents Association {tag} {index}").Value,
                CL.ValueObjects.CommunityLifeSlug.FromText($"area-point-{tag}-{index}"),
                CL.ValueObjects.CommunityLifeDescription
                    .Create("A body seeded for the area-around-a-point tests.", null).Value,
                "cultural-association",
                CL.ValueObjects.CommunityLifeLocationContext.Create("KE", null, "Nairobi").Value,
                now).Value;

            organization.SetCoordinates(latitude, longitude).IsSuccess.Should().BeTrue();
            organization.Publish(now).IsSuccess.Should().BeTrue();

            dbContext.Set<CL.Organizations.Organization>().Add(organization);

            return Task.CompletedTask;
        });

    private Task SeedPostAsync(double latitude, double longitude, PostCategory category, bool corroborated) =>
        WithDbAsync(dbContext =>
        {
            var publishedAt = DateTimeOffset.UtcNow.AddDays(-2);

            var post = CommunityPost.Draft(
                UserId.New(),
                PublishingIdentity.Personal(),
                PostBody.Create($"A seeded update for the area-around-a-point tests, {Guid.NewGuid():N}.").Value,
                null,
                category,
                UrgencyLevel.Normal,
                ContentAudience.Public,
                PostLocationContext.Create(
                    countryCode: "KE", cityName: "Nairobi", latitude: latitude, longitude: longitude, pointIsExact: true),
                publishedAt).Value;

            post.Publish(publishedAt).IsSuccess.Should().BeTrue();

            if (corroborated)
            {
                var (total, verified) = ConfirmationThresholds.For(UrgencyLevel.Normal);

                for (var index = 0; index < total; index++)
                {
                    post.RecordCorroboration(index < verified, 1);
                }

                post.VerificationStatus.Should().Be(PostVerificationStatus.CommunityVerified);
            }

            dbContext.Set<CommunityPost>().Add(post);

            return Task.CompletedTask;
        });

    private async Task ApproveAsync(Guid insightId)
    {
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var insight = await dbContext.Set<NeighborhoodInsight>()
            .SingleAsync(row => row.Id == NeighborhoodInsightId.From(insightId));

        insight.Approve(UserId.New(), "Checked by the suite.", DateTimeOffset.UtcNow).IsSuccess.Should().BeTrue();

        await dbContext.SaveChangesAsync();
    }

    private async Task<List<JsonElement>> InsightsAsync(double latitude, double longitude, bool mineOnly = false)
    {
        var data = await DataAsync(await HttpClient.GetAsync(
            $"/api/v1/housing/area/insights?latitude={latitude}&longitude={longitude}&limit=100"
            + (mineOnly ? "&mineOnly=true" : string.Empty)));

        return [.. data.EnumerateArray()];
    }

    private async Task<List<Guid>> InsightIdsAsync(double latitude, double longitude, bool mineOnly = false) =>
        [.. (await InsightsAsync(latitude, longitude, mineOnly)).Select(insight => insight.GetProperty("id").GetGuid())];

    private static async Task<JsonElement> DataAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue(
            "the server answered {0}: {1}", response.StatusCode, body);

        using var document = JsonDocument.Parse(body);

        return document.RootElement.GetProperty("data").Clone();
    }
}
