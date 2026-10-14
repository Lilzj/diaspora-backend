using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Modules.Housing.Domain.ValueObjects;
using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.Modules.Marketplace.Domain.ValueObjects;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using CL = Diaspora.Modules.CommunityLife.Domain;
using GuideDomain = Diaspora.Modules.Guides.Domain;
using JobDomain = Diaspora.Modules.Jobs.Domain;
using ProviderDomain = Diaspora.Modules.Providers.Domain;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// Every vertical is found by coordinates, country and the owner's typed town — never by the place
/// catalogue (14 Oct 2026, Phase 1 of retiring it).
/// <para>
/// <b>Mombasa is the point.</b> The catalogue does not hold it, so until this change a search
/// around a point in Mombasa snapped to whichever catalogued node was nearest (or none), and a
/// search for the town by name resolved through the catalogue and found nothing. Each vertical is
/// seeded with three rows: one pinned in Mombasa, one pinned in Nairobi (about 440 km away), and
/// one in Mombasa with no pin at all, the way every row written before pins were asked for is. Each
/// test then asks three questions and asserts on both halves of every answer:
/// </para>
/// <list type="bullet">
/// <item>a circle around Nyali, a few kilometres up the coast, finds the pinned Mombasa row and not
/// the Nairobi one;</item>
/// <item>the same circle with the town named also finds the unpinned Mombasa row;</item>
/// <item>the town typed in mixed case, with its country, finds both Mombasa rows and not Nairobi —
/// in either letter case, because Postgres compares text case-sensitively and SQLite does not, and
/// the repositories now lower both sides.</item>
/// </list>
/// </summary>
[Collection(AssortedCollection.Name)]
public sealed class CoordinateDiscoveryTests : PooledIdentityTestBase
{
    // Mombasa Old Town, Nyali (about 5 km north-east across the creek) and Nairobi CBD.
    private const double MombasaLatitude = -4.0435;
    private const double MombasaLongitude = 39.6682;
    private const double NairobiLatitude = -1.2921;
    private const double NairobiLongitude = 36.8219;
    private const string AroundNyali = "latitude=-4.0220&longitude=39.7100&radiusKm=15";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public CoordinateDiscoveryTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Providers_are_found_by_their_pin_and_by_their_town_in_any_case()
    {
        var run = await SeedProvidersAsync();

        await AssertFoundAsync(
            run,
            query => HttpClient.GetAsync($"/api/v1/providers?limit=50&query={run.Tag}&{query}"),
            "name",
            townQuery: "cityName=mOMBASA&countryCode=KE",
            circleWithTownQuery: $"{AroundNyali}&cityName=Mombasa",
            anonymous: false);
    }

    [Fact]
    public async Task Homes_are_found_by_their_pin_and_by_their_town_in_any_case()
    {
        var run = await SeedHomesAsync();

        await AssertFoundAsync(
            run,
            query => HttpClient.GetAsync($"/api/v1/housing/search?limit=50&query={run.Tag}&{query}"),
            "title",
            townQuery: "city=mombasa&countryCode=KE",
            circleWithTownQuery: $"{AroundNyali}&city=MOMBASA");
    }

    [Fact]
    public async Task Jobs_are_found_by_their_pin_and_by_their_town_in_any_case()
    {
        var run = await SeedJobsAsync();

        await AssertFoundAsync(
            run,
            query => HttpClient.GetAsync($"/api/v1/jobs?limit=50&q={run.Tag}&{query}"),
            "title",
            townQuery: "city=mOmBaSa&country=KE",
            circleWithTownQuery: $"{AroundNyali}&city=Mombasa");
    }

    [Fact]
    public async Task Marketplace_listings_are_found_by_their_pin_and_by_their_town_in_any_case()
    {
        var run = await SeedMarketplaceListingsAsync();

        Task<HttpResponseMessage> Search(object body) =>
            HttpClient.PostAsJsonAsync("/api/v1/marketplace/search", body, JsonOptions);

        var circle = await TitlesAsync(await Search(new
        {
            text = run.Tag, latitude = -4.0220, longitude = 39.7100, radiusKm = 15, limit = 50
        }), "title");

        circle.Should().Contain(run.Pinned).And.NotContain(run.Far).And.NotContain(run.Unpinned);

        var circleWithTown = await TitlesAsync(await Search(new
        {
            text = run.Tag, latitude = -4.0220, longitude = 39.7100, radiusKm = 15, city = "MOMBASA", limit = 50
        }), "title");

        circleWithTown.Should().Contain([run.Pinned, run.Unpinned]).And.NotContain(run.Far);

        var town = await TitlesAsync(await Search(new
        {
            text = run.Tag, city = "mombasa", countryCode = "KE", limit = 50
        }), "title");

        town.Should().Contain([run.Pinned, run.Unpinned]).And.NotContain(run.Far);
    }

    [Fact]
    public async Task Guides_are_found_by_their_pin_and_by_their_town_in_any_case()
    {
        var run = await SeedGuidesAsync();

        // The nearby route has no text filter; the titles carry the run's tag instead.
        await AssertFoundAsync(
            run,
            query => HttpClient.GetAsync($"/api/v1/guides/nearby?limit=50&{query}"),
            "title",
            townQuery: "cityName=MomBasa&countryCode=KE",
            circleWithTownQuery: $"{AroundNyali}&cityName=mombasa");
    }

    [Fact]
    public async Task Events_are_found_by_their_venue_and_by_their_town_in_any_case()
    {
        var run = await SeedEventsAsync();

        await AssertFoundAsync(
            run,
            query => HttpClient.GetAsync($"/api/v1/community-life/events?limit=50&q={run.Tag}&{query}"),
            "title",
            townQuery: "city=MOMBASA&countryCode=KE",
            circleWithTownQuery: $"{AroundNyali}&city=Mombasa");
    }

    [Fact]
    public async Task Organizations_are_found_by_their_pin_and_by_their_town_in_any_case()
    {
        var run = await SeedOrganizationsAsync();

        await AssertFoundAsync(
            run,
            query => HttpClient.GetAsync($"/api/v1/community-life/organizations?limit=50&q={run.Tag}&{query}"),
            "name",
            townQuery: "city=mombasa&countryCode=KE",
            circleWithTownQuery: $"{AroundNyali}&city=MOMBASA");
    }

    /// <summary>
    /// The feed defect named in the decision: a nearby feed with coordinates was narrowed to exactly
    /// the area id of the catalogue node the point snapped to, so a post pinned a street away, filed
    /// against another node or none, was not "nearby". It is now the rectangle around the circle.
    /// </summary>
    [Fact]
    public async Task The_nearby_feed_with_coordinates_returns_the_posts_inside_the_box()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var tag = Guid.NewGuid().ToString("N")[..8];

        var inside = await CreatePostAsync($"Water cut on Nyali Road {tag}", "Mombasa", MombasaLatitude, MombasaLongitude);
        var far = await CreatePostAsync($"Water cut in the CBD {tag}", "Nairobi", NairobiLatitude, NairobiLongitude);
        var unpinned = await CreatePostAsync($"Water cut somewhere in town {tag}", "Mombasa", null, null);

        var circle = await FeedIdsAsync($"/api/v1/community/feeds/nearby?limit=50&{AroundNyali}");

        circle.Should().Contain(inside).And.NotContain(far).And.NotContain(unpinned);

        var circleWithTown = await FeedIdsAsync(
            $"/api/v1/community/feeds/nearby?limit=50&{AroundNyali}&cityName=mombasa");

        circleWithTown.Should().Contain([inside, unpinned]).And.NotContain(far);

        // An area id an old app build still sends narrows nothing — not to a node, not to empty.
        var withStaleArea = await FeedIdsAsync(
            $"/api/v1/community/feeds/nearby?limit=50&{AroundNyali}&areaId={Guid.NewGuid()}");

        withStaleArea.Should().Contain(inside).And.NotContain(far);
    }

    /// <summary>
    /// What a create stores now: the owner's pin, country and typed town, and no catalogue area —
    /// for a town the catalogue does not hold as much as for one it does.
    /// </summary>
    [Fact]
    public async Task A_new_job_is_stored_with_its_pin_and_town_and_no_catalogue_area()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var employer = await DataAsync(await HttpClient.PostAsJsonAsync(
            "/api/v1/jobs/employers",
            new { displayName = "Coast Employer", organizationType = "Company", countryCode = "KE" },
            JsonOptions));

        var created = await DataAsync(await HttpClient.PostAsJsonAsync(
            "/api/v1/jobs/",
            new
            {
                employerId = employer.GetProperty("employerId").GetGuid(),
                title = "Harbour Clerk",
                description = "A harbour clerk role, described at enough length to be valid.",
                categoryKey = "healthcare",
                jobType = "FullTime",
                experienceLevel = "Mid",
                countryCode = "KE",
                cityName = "Mombasa",
                areaId = Guid.NewGuid(),
                latitude = MombasaLatitude,
                longitude = MombasaLongitude
            },
            JsonOptions));

        var jobId = JobDomain.Identifiers.JobPostingId.From(created.GetProperty("jobId").GetGuid());

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var stored = await dbContext.Set<JobDomain.Postings.JobPosting>().FindAsync(jobId);

        stored.Should().NotBeNull();
        stored!.Location.CityName.Should().Be("Mombasa");
        stored.Location.CountryCode.Should().Be("KE");
        stored.Location.AreaId.Should().BeNull("an area id from an old app build is not written");
        stored.Location.AreaPath.Should().BeNull("nothing files a row under the catalogue any more");
        stored.Latitude.Should().BeApproximately(MombasaLatitude, 1e-6);
    }

    // ── The three questions ───────────────────────────────────────────────────────────────────

    private async Task AssertFoundAsync(
        SeededRun run,
        Func<string, Task<HttpResponseMessage>> search,
        string titleProperty,
        string townQuery,
        string circleWithTownQuery,
        bool anonymous = true)
    {
        if (anonymous)
        {
            Api.ClearAuthentication();
        }
        else
        {
            var (_, auth) = await RegisterVerifiedMemberAsync();
            Api.Authenticate(auth.Tokens.AccessToken);
        }

        var circle = await TitlesAsync(await search(AroundNyali), titleProperty);

        circle.Should().Contain(run.Pinned, "it is pinned inside the circle")
            .And.NotContain(run.Far, "Nairobi is about 440 km away")
            .And.NotContain(run.Unpinned, "a row with no pin is outside every circle that names no town");

        var circleWithTown = await TitlesAsync(await search(circleWithTownQuery), titleProperty);

        circleWithTown.Should().Contain(run.Pinned)
            .And.Contain(run.Unpinned, "a row with no pin is kept when it names the town the search named")
            .And.NotContain(run.Far);

        var town = await TitlesAsync(await search(townQuery), titleProperty);

        town.Should().Contain(run.Pinned)
            .And.Contain(run.Unpinned, "a town search does not need coordinates")
            .And.NotContain(run.Far);
    }

    // ── Seeding: the rows the platform actually has, written straight to the store ─────────

    private sealed record SeededRun(string Tag, string Pinned, string Far, string Unpinned);

    private static SeededRun NewRun(string noun)
    {
        var tag = Guid.NewGuid().ToString("N")[..8];

        return new SeededRun(
            tag,
            $"{noun} in Mombasa {tag}",
            $"{noun} in Nairobi {tag}",
            $"{noun} somewhere in Mombasa {tag}");
    }

    private static IEnumerable<(string Title, string Town, double? Latitude, double? Longitude)> Rows(SeededRun run) =>
    [
        (run.Pinned, "Mombasa", MombasaLatitude, MombasaLongitude),
        (run.Far, "Nairobi", NairobiLatitude, NairobiLongitude),
        (run.Unpinned, "Mombasa", null, null)
    ];

    private async Task<SeededRun> SeedAsync(SeededRun run, Action<DiasporaDbContext, (string Title, string Town, double? Latitude, double? Longitude), int> add)
    {
        Factory.EnsureDatabaseCreated();

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var index = 0;

        foreach (var row in Rows(run))
        {
            add(dbContext, row, index++);
        }

        await dbContext.SaveChangesAsync();

        return run;
    }

    private Task<SeededRun> SeedProvidersAsync()
    {
        var run = NewRun("Tailor");
        var ownerId = UserId.New();
        var now = DateTimeOffset.UtcNow;

        return SeedAsync(run, (dbContext, row, index) =>
        {
            var provider = ProviderDomain.Providers.Provider.Register(
                ProviderDomain.Identifiers.ProviderId.New(),
                ProviderDomain.ValueObjects.ProviderName.Create(row.Title).Value,
                ProviderDomain.ValueObjects.ProviderSlug.Create($"coordinates-{run.Tag}-{index}").Value,
                ProviderDomain.Enums.ProviderType.Individual,
                ProviderDomain.Identifiers.ServiceCategoryId.New(),
                ownerId,
                ProviderDomain.Identifiers.ProviderProfileId.New(),
                ProviderDomain.Identifiers.ProviderMembershipId.New(),
                now,
                ProviderDomain.ValueObjects.ProviderLocationContext.Create(
                    countryCode: "KE",
                    cityName: row.Town,
                    latitude: row.Latitude,
                    longitude: row.Longitude)).Value;

            provider.AddContactMethod(
                ProviderDomain.Providers.ProviderContactMethod.Create(
                    ProviderDomain.Identifiers.ProviderContactMethodId.New(),
                    ProviderDomain.Enums.ContactMethodKind.Phone,
                    "+254712345678",
                    isPrimary: true).Value).IsSuccess.Should().BeTrue();

            provider.Publish(now).IsSuccess.Should().BeTrue();

            dbContext.Set<ProviderDomain.Providers.Provider>().Add(provider);
        });
    }

    private Task<SeededRun> SeedHomesAsync()
    {
        var run = NewRun("Flat");
        var landlordId = LandlordProfileId.New();
        var ownerId = UserId.New();
        var now = DateTimeOffset.UtcNow;

        return SeedAsync(run, (dbContext, row, index) =>
        {
            var listing = HousingListing.Draft(
                HousingListingId.New(),
                landlordId,
                ownerId,
                HousingTitle.Create(row.Title).Value,
                HousingSlug.Create($"coordinates-{run.Tag}-{index}"),
                HousingDescription.Create("A home seeded for the coordinate discovery tests.").Value,
                "apartment",
                HousingPrice.Create(30_000m, "KES", RentPeriod.PerMonth, null).Value,
                HousingCapacity.Create(2, 1m, maximumOccupants: 3).Value,
                HousingAvailability.Create(totalUnits: 1).Value,
                HousingLocationContext.Create("KE", cityName: row.Town).Value,
                row.Latitude is null
                    ? PropertyAddress.Undisclosed
                    : PropertyAddress.Create(
                        "Moi Avenue", null, null, row.Latitude, row.Longitude,
                        AddressVisibility.NeighborhoodOnly).Value,
                StayPolicy.Create(
                    acceptsShortStay: false, acceptsLongStay: true, isSharedAccommodation: false).Value,
                now).Value;

            listing.AttachMedia(Guid.NewGuid(), HousingMediaRole.Cover, null, now).IsSuccess.Should().BeTrue();
            listing.Publish(now).IsSuccess.Should().BeTrue();

            dbContext.Set<HousingListing>().Add(listing);
        });
    }

    private Task<SeededRun> SeedJobsAsync()
    {
        var run = NewRun("Clerk");
        var employerId = JobDomain.Identifiers.EmployerProfileId.New();
        var ownerId = UserId.New();
        var now = DateTimeOffset.UtcNow;

        return SeedAsync(run, (dbContext, row, index) =>
        {
            var posting = JobDomain.Postings.JobPosting.Create(
                JobDomain.Identifiers.JobPostingId.New(),
                employerId,
                ownerId,
                JobDomain.ValueObjects.JobTitle.Create(row.Title).Value,
                JobDomain.ValueObjects.JobSlug.Create($"coordinates-{run.Tag}-{index}").Value,
                JobDomain.ValueObjects.JobDescription
                    .Create("A posting seeded for the coordinate discovery tests.").Value,
                "technology",
                JobDomain.Enums.JobType.FullTime,
                JobDomain.Enums.ExperienceLevel.Mid,
                JobDomain.ValueObjects.JobLocationContext.Create("KE", null, row.Town).Value,
                JobDomain.ValueObjects.WorkArrangement.Create(
                    JobDomain.Enums.WorkplaceType.OnSite, false).Value,
                now).Value;

            posting.SetCoordinates(row.Latitude, row.Longitude).IsSuccess.Should().BeTrue();
            posting.Publish(now, openImmediately: true).IsSuccess.Should().BeTrue();

            dbContext.Set<JobDomain.Postings.JobPosting>().Add(posting);
        });
    }

    private Task<SeededRun> SeedMarketplaceListingsAsync()
    {
        var run = NewRun("Bicycle");
        var sellerId = SellerProfileId.New();
        var ownerId = UserId.New();
        var now = DateTimeOffset.UtcNow;

        return SeedAsync(run, (dbContext, row, index) =>
        {
            var listing = MarketplaceListing.Create(
                MarketplaceListingId.New(),
                sellerId,
                ownerId,
                ListingTitle.Create(row.Title).Value,
                ListingSlug.Create($"coordinates-{run.Tag}-{index}").Value,
                ListingDescription.Create("A listing seeded for the coordinate discovery tests.").Value,
                "electronics",
                ListingType.Negotiable,
                ProductCondition.Good,
                ListingPrice.Create(5_000m, "KES", isNegotiable: true, minimumOffer: null).Value,
                ListingLocationContext.Create("KE", null, row.Town).Value,
                now).Value;

            listing.SetCoordinates(row.Latitude, row.Longitude).IsSuccess.Should().BeTrue();
            listing.AttachMedia(Guid.NewGuid(), ListingMediaRole.Cover);
            listing.Publish(now).IsSuccess.Should().BeTrue();

            dbContext.Set<MarketplaceListing>().Add(listing);
        });
    }

    private Task<SeededRun> SeedGuidesAsync()
    {
        var run = NewRun("Registering with a clinic");
        var author = UserId.New();
        var now = DateTimeOffset.UtcNow;

        return SeedAsync(run, (dbContext, row, index) =>
        {
            var guide = GuideDomain.Guides.Guide.Create(
                GuideDomain.Identifiers.GuideId.New(),
                author,
                GuideDomain.ValueObjects.GuideTitle.Create(row.Title).Value,
                GuideDomain.ValueObjects.GuideSlug.Create($"coordinates-{run.Tag}-{index}").Value,
                "healthcare",
                GuideDomain.ValueObjects.GuideLanguage.Create("en").Value,
                GuideDomain.ValueObjects.GuideLocationContext.Create("KE", null, row.Town).Value,
                now).Value;

            guide.SetCoordinates(row.Latitude, row.Longitude).IsSuccess.Should().BeTrue();

            guide.ReplaceStructure(
                [
                    new GuideDomain.ValueObjects.GuideSectionDraft
                    {
                        SectionId = Guid.NewGuid(),
                        Title = "What to do",
                        Order = 0,
                        Blocks =
                        [
                            new GuideDomain.ValueObjects.GuideBlockDraft
                            {
                                BlockId = Guid.NewGuid(),
                                Kind = GuideDomain.Enums.GuideBlockKind.Markdown,
                                Order = 0,
                                Content = "Bring your passport and proof of address."
                            }
                        ]
                    }
                ],
                baseRevision: null,
                author);

            guide.Publish(now, author).IsSuccess.Should().BeTrue();

            dbContext.Set<GuideDomain.Guides.Guide>().Add(guide);
        });
    }

    private Task<SeededRun> SeedEventsAsync()
    {
        var run = NewRun("Coast meet-up");
        var organizerId = UserId.New();
        var now = DateTimeOffset.UtcNow;

        return SeedAsync(run, (dbContext, row, index) =>
        {
            var communityEvent = CL.CommunityEvents.CommunityEvent.Draft(
                CL.Identifiers.CommunityEventId.New(),
                organizationId: null,
                organizerId,
                CL.ValueObjects.CommunityLifeTitle.Create(row.Title).Value,
                CL.ValueObjects.CommunityLifeSlug.FromText($"coordinates-{run.Tag}-{index}"),
                CL.ValueObjects.CommunityLifeDescription
                    .Create("An event seeded for the coordinate discovery tests.", null).Value,
                "community-meeting",
                CL.ValueObjects.EventSchedule
                    .Create(now.AddDays(14), now.AddDays(14).AddHours(2), "Africa/Nairobi").Value,
                CL.ValueObjects.CommunityLifeLocationContext.Create("KE", null, row.Town).Value,
                CL.ValueObjects.EventVenue.Create(
                    name: "Community Hall",
                    latitude: row.Latitude,
                    longitude: row.Longitude).Value,
                CL.Enums.EventAttendanceMode.InPerson,
                now).Value;

            communityEvent.Publish(now).IsSuccess.Should().BeTrue();

            dbContext.Set<CL.CommunityEvents.CommunityEvent>().Add(communityEvent);
        });
    }

    private Task<SeededRun> SeedOrganizationsAsync()
    {
        var run = NewRun("Coast Association");
        var creator = UserId.New();
        var now = DateTimeOffset.UtcNow;

        return SeedAsync(run, (dbContext, row, index) =>
        {
            var organization = CL.Organizations.Organization.Draft(
                CL.Identifiers.OrganizationId.New(),
                creator,
                CL.ValueObjects.OrganizationName.Create(row.Title).Value,
                CL.ValueObjects.CommunityLifeSlug.FromText($"coordinates-{run.Tag}-{index}"),
                CL.ValueObjects.CommunityLifeDescription
                    .Create("A body seeded for the coordinate discovery tests.", null).Value,
                "cultural-association",
                CL.ValueObjects.CommunityLifeLocationContext.Create("KE", null, row.Town).Value,
                now).Value;

            organization.SetCoordinates(row.Latitude, row.Longitude).IsSuccess.Should().BeTrue();
            organization.Publish(now).IsSuccess.Should().BeTrue();

            dbContext.Set<CL.Organizations.Organization>().Add(organization);
        });
    }

    // ── Plumbing ─────────────────────────────────────────────────────────────────────────────

    private async Task<Guid> CreatePostAsync(string body, string town, double? latitude, double? longitude)
    {
        var created = await DataAsync(await HttpClient.PostAsJsonAsync(
            "/api/v1/community/posts",
            new
            {
                body,
                category = "Traffic",
                urgency = "Normal",
                countryCode = "KE",
                cityName = town,
                latitude,
                longitude
            },
            JsonOptions));

        return created.GetProperty("postId").GetGuid();
    }

    private async Task<List<Guid>> FeedIdsAsync(string url)
    {
        var feed = await DataAsync(await HttpClient.GetAsync(url));

        var ids = new List<Guid>();

        foreach (var item in feed.GetProperty("items").EnumerateArray())
        {
            ids.Add(item.GetProperty("contentId").GetGuid());

            if (item.TryGetProperty("post", out var post)
                && post.ValueKind == JsonValueKind.Object
                && post.TryGetProperty("postId", out var postId))
            {
                ids.Add(postId.GetGuid());
            }
        }

        return ids;
    }

    private static async Task<List<string>> TitlesAsync(HttpResponseMessage response, string property)
    {
        var data = await DataAsync(response);

        return [.. data.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty(property).GetString()!)];
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
