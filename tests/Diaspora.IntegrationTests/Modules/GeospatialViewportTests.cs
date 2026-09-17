using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Modules.Housing.Domain.ValueObjects;
using Diaspora.Modules.Location.Domain.Areas;
using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.Modules.Location.Extensibility;
using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.Modules.Marketplace.Domain.ValueObjects;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// B4.1 — the map viewport, driven over real HTTP against real rows.
/// <para>
/// <b>Every assertion here needs data to mean anything.</b> A bounding-box filter over an empty
/// table passes whatever it does, and the failure this suite exists to catch — a predicate that
/// returns nothing, or everything — looks exactly like an empty database. So each test seeds rows
/// that should match <em>and</em> rows that should not, and asserts on both halves.
/// </para>
/// <para>
/// The seeded catalogue is B4's own launch geography: Peckham at 51.4742, -0.0691 and Woolwich at
/// 51.4933, 0.0632, both under London. Sixteen kilometres apart, which is far enough that no
/// rounding this suite performs can confuse them.
/// </para>
/// </summary>
public sealed class GeospatialViewportTests : IdentityTestBase
{
    /// <summary>A rectangle around Peckham that excludes Woolwich. North,South,East,West.</summary>
    private const string PeckhamViewport = "51.49,51.46,-0.04,-0.10";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public GeospatialViewportTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ── The rectangle, against rows ─────────────────────────────────────────────────────────

    /// <summary>
    /// The gap the frontend rated critical: every certified search route took a centre and a
    /// radius, and a map is a rectangle. Three properties, one in the box.
    /// </summary>
    [Fact]
    public async Task A_housing_viewport_returns_only_the_properties_inside_the_rectangle()
    {
        var run = await SeedListingsAsync(
            ("inside", 51.4742d, -0.0691d),
            ("outside", 51.4933d, 0.0632d),
            ("nowhere", null, null));

        var page = await SearchHousingAsync($"bounds={PeckhamViewport}&query={run}");

        page.Items.Select(item => item.Title).Should().BeEquivalentTo([$"inside {run}"]);
    }

    /// <summary>
    /// A property with no recorded coordinate is not in any rectangle. Keeping it "just in case"
    /// would put a pin on a map at a place nobody ever stated.
    /// </summary>
    [Fact]
    public async Task A_property_with_no_coordinate_is_absent_from_a_viewport_and_present_without_one()
    {
        var run = await SeedListingsAsync(("nowhere", null, null));

        (await SearchHousingAsync($"bounds={PeckhamViewport}&query={run}"))
            .Items.Should().BeEmpty();

        (await SearchHousingAsync($"query={run}"))
            .Items.Should().HaveCount(1, "an ordinary search still finds it");
    }

    /// <summary>
    /// The ±180° case. <c>west &gt; east</c> means the box wraps, and the naive predicate
    /// <c>lng &gt;= 177 AND lng &lt;= -178</c> is satisfied by no row on the planet — an empty map
    /// with no error, in the two countries that need this most.
    /// </summary>
    [Fact]
    public async Task A_viewport_crossing_the_antimeridian_finds_properties_on_both_sides()
    {
        var run = await SeedListingsAsync(
            ("east-of-the-line", -17.8d, 178.4d),
            ("west-of-the-line", -17.8d, -179.9d),
            ("far-away", -17.8d, 0d));

        var page = await SearchHousingAsync($"bounds=-16,-19,-178,177&query={run}");

        page.Items.Select(item => item.Title).Should().BeEquivalentTo(
            [$"east-of-the-line {run}", $"west-of-the-line {run}"]);
    }

    /// <summary>
    /// <b>The privacy property, and it is not incidental.</b> A housing coordinate is published
    /// rounded to two decimal places because a property is somebody's home. A viewport filter that
    /// read the exact stored coordinate would let a caller shrink the box around one listing until
    /// only that listing answered — the same position, obtained by bisection instead of by reading
    /// it. The rectangle is snapped outward to the disclosure grid, so a box far finer than that
    /// grid cannot separate two homes inside one cell.
    /// </summary>
    [Fact]
    public async Task A_viewport_finer_than_the_published_address_precision_cannot_isolate_one_home()
    {
        // 60 metres apart. Both round to 51.47, -0.07 — one published cell.
        var run = await SeedListingsAsync(
            ("north-side", 51.4746d, -0.0688d),
            ("south-side", 51.4741d, -0.0692d));

        // A box tight around the first, roughly 20 metres on a side.
        var page = await SearchHousingAsync($"bounds=51.4747,51.4745,-0.0687,-0.0689&query={run}");

        page.Items.Should().HaveCount(
            2,
            "a rectangle narrower than the disclosed precision must not distinguish two homes "
            + "inside one published cell");
    }

    /// <summary>
    /// <b>The regression for D-2.</b> B7's bounding box has been a real prefilter since the module
    /// shipped, and it could not express a box that wraps: <c>MinLongitude = West</c>,
    /// <c>MaxLongitude = East</c>, and <c>lng &gt;= 179 AND lng &lt;= -179</c> is satisfied by no row
    /// on the planet. A member in Suva panning across the date line got an empty map, no error, and
    /// nothing in the logs.
    /// </summary>
    [Fact]
    public async Task A_provider_viewport_crossing_the_antimeridian_finds_both_sides()
    {
        var run = await SeedProvidersAsync(
            ("east-of-the-line", -17.8d, 178.4d),
            ("west-of-the-line", -17.8d, -179.9d),
            ("far-away", -17.8d, 0d));

        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var page = await ReadDataAsync<ProviderPageDto>(
            await HttpClient.GetAsync(
                $"/api/v1/providers?limit=50&query={run}&bounds=-16,-19,-178,177"));

        page.Items.Select(item => item.Name).Should().BeEquivalentTo(
            [$"east-of-the-line {run}", $"west-of-the-line {run}"]);
    }

    // ── The subtree, for verticals that store no coordinate ─────────────────────────────────

    /// <summary>
    /// <b>Containment runs in two directions and only one of them is a prefix.</b> Marketplace
    /// files a listing against an area rather than a point, so a viewport becomes the smallest
    /// catalogue subtree containing it. A listing filed against London itself must survive a
    /// member zooming into one street of Peckham — otherwise the most common filing in the module
    /// disappears at exactly the zoom where somebody is looking hardest.
    /// </summary>
    [Fact]
    public async Task A_marketplace_viewport_keeps_a_city_filed_listing_and_drops_another_city()
    {
        var (london, manchester) = await CityPathsAsync();
        var peckham = await NeighborhoodPathAsync("Peckham");

        var run = await SeedMarketplaceListingsAsync(
            ("in-the-neighbourhood", peckham),
            ("filed-against-the-city", london),
            ("another-city", manchester));

        var response = await HttpClient.PostAsJsonAsync(
            "/api/v1/marketplace/search",
            new { text = run, bounds = PeckhamViewport, limit = 50 });

        var page = await ReadDataAsync<ListingPageDto>(response);

        page.Items.Select(item => item.Title).Should().BeEquivalentTo(
            [$"in-the-neighbourhood {run}", $"filed-against-the-city {run}"]);
    }

    /// <summary>
    /// An event is at a venue and the venue has a coordinate, so the rectangle filters it exactly
    /// — the same treatment a housing listing gets and the opposite of the subtree approximation
    /// an organization takes. An online-only event is in no rectangle at all.
    /// </summary>
    [Fact]
    public async Task An_event_viewport_filters_by_the_venue_coordinate()
    {
        var run = await SeedEventsAsync(
            ("at-the-venue", 51.4742d, -0.0691d),
            ("across-town", 51.4933d, 0.0632d),
            ("online-only", null, null));

        var page = await ReadDataAsync<EventPageDto>(
            await HttpClient.GetAsync(
                $"/api/v1/community-life/events?limit=50&q={run}&bounds={PeckhamViewport}"));

        page.Items.Select(item => item.Title).Should().BeEquivalentTo([$"at-the-venue {run}"]);
    }

    /// <summary>
    /// <b>Jobs could not answer a geographic question at all before this phase.</b>
    /// <c>JobGeographicScopeAdapter</c> was registered in DI and called from nowhere, and
    /// <c>SearchJobsRequest</c> carried no coordinate, no radius and no viewport — the browse
    /// filtered on whatever country and city string the client happened to send. This is the first
    /// test that resolves a job search through B4.
    /// </summary>
    [Fact]
    public async Task A_jobs_viewport_narrows_to_the_catalogue_subtree()
    {
        var (london, manchester) = await CityPathsAsync();
        var peckham = await NeighborhoodPathAsync("Peckham");

        var run = await SeedJobsAsync(
            ("in-the-neighbourhood", peckham),
            ("filed-against-the-city", london),
            ("another-city", manchester));

        var page = await ReadDataAsync<JobPageDto>(
            await HttpClient.GetAsync(
                $"/api/v1/jobs?limit=50&q={run}&bounds={PeckhamViewport}"));

        page.Items.Select(item => item.Title).Should().BeEquivalentTo(
            [$"in-the-neighbourhood {run}", $"filed-against-the-city {run}"]);
    }

    // ── Clusters ────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Server-side aggregation, on catalogue nodes rather than grid tiles — so a cluster carries a
    /// name and a slug a member can tap through to, and Jobs and Marketplace (which store no
    /// coordinate at all) are included rather than absent.
    /// </summary>
    [Fact]
    public async Task Map_clusters_aggregate_a_viewport_into_places_with_counts()
    {
        // Filed against the real Peckham path: the counters aggregate by hierarchy prefix, and a
        // listing filed against an area the catalogue does not have contributes to nothing.
        await SeedListingsAsync(
            await NeighborhoodPathAsync("Peckham"), ("clustered", 51.4742d, -0.0691d));

        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.GetAsync(
            $"/api/v1/location/map/clusters?bounds={PeckhamViewport}&resourceTypes=housing");

        var page = await ReadDataAsync<ClusterPageDto>(response);

        page.Level.Should().Be("Neighborhood");
        page.ResourceTypes.Should().BeEquivalentTo(["housing"]);
        page.MinimumCellCount.Should().Be(1);

        var peckham = page.Clusters.Single(cluster => cluster.Name == "Peckham");

        peckham.Total.Should().BeGreaterThan(0);
        peckham.Counts.Should().ContainKey("housing");
        peckham.Latitude.Should().BeApproximately(51.4742d, 0.001d);
    }

    /// <summary>A viewport wider than the catalogue's continental cap is refused, not scanned.</summary>
    [Fact]
    public async Task A_viewport_spanning_more_than_a_continent_is_refused()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.GetAsync(
            "/api/v1/location/map/clusters?bounds=60,-60,60,-60");

        response.IsSuccessStatusCode.Should().BeFalse();
    }

    // ── The aggregate fan-out ───────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>The regression this suite was written for.</b> The hub asked every registered counter
    /// concurrently over the platform's one scoped <c>DbContext</c>, which refuses a second
    /// operation while the first is running — so every counter but one threw, the aggregator's
    /// catch turned each throw into an omitted key, and the contract defines an omitted key as
    /// "this vertical could not be asked". A member saw one number out of six and nothing was
    /// logged as an error.
    /// <para>
    /// It needed two verticals with rows in the same place to appear at all, which no suite had.
    /// This test is that arrangement.
    /// </para>
    /// </summary>
    [Fact]
    public async Task The_neighbourhood_hub_answers_with_every_counter_the_deployment_has()
    {
        var peckham = await NeighborhoodPathAsync("Peckham");

        await SeedListingsAsync(peckham, ("hub-housing", 51.4742d, -0.0691d));
        await SeedMarketplaceListingsAsync(("hub-marketplace", peckham));

        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.GetAsync("/api/v1/location/neighborhoods/peckham");

        var hub = await ReadDataAsync<HubDto>(response);

        var expected = await CounterResourceTypesAsync();

        hub.ResourceCounts.Keys.Should().BeEquivalentTo(
            expected,
            "an absent key means 'could not be asked', and every counter can be asked");

        hub.ResourceCounts["housing"].Should().BeGreaterThan(0);
        hub.ResourceCounts["marketplace"].Should().BeGreaterThan(0);
    }

    // ── Reverse geocoding ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// The register's complaint was that the route "answers with the same confidence from 40 km
    /// away as from 400 m". It now says which it is, and how far.
    /// </summary>
    [Fact]
    public async Task Reverse_geocoding_states_the_distance_and_the_confidence()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var onTheSpot = await ReadDataAsync<ReferenceDto>(
            await HttpClient.GetAsync(
                "/api/v1/location/reverse-geocode?latitude=51.4742&longitude=-0.0691"));

        onTheSpot.Neighborhood.Should().Be("Peckham");
        onTheSpot.Confidence.Should().Be("Exact");
        onTheSpot.DistanceKm.Should().BeLessThan(1d);

        // Thirty kilometres north of anything the catalogue knows in London.
        var faraway = await ReadDataAsync<ReferenceDto>(
            await HttpClient.GetAsync(
                "/api/v1/location/reverse-geocode?latitude=51.85&longitude=-0.0691"));

        faraway.Confidence.Should().Be("Low");
        faraway.DistanceKm.Should().BeGreaterThan(25d);
    }

    // ── Boundaries ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The geometry has been persisted since B4 and the contract said only whether one existed.
    /// A client cannot draw <c>hasBoundary: true</c>.
    /// </summary>
    [Fact]
    public async Task An_area_publishes_the_outline_the_catalogue_holds()
    {
        var areaId = await SetPeckhamBoundaryAsync();

        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var detail = await ReadDataAsync<AreaDetailDto>(
            await HttpClient.GetAsync($"/api/v1/location/areas/{areaId}"));

        detail.HasBoundary.Should().BeTrue();
        detail.Boundary.Should().NotBeNull();
        detail.Boundary!.Vertices.Should().HaveCount(4);
        detail.Boundary.SimplifiedTo.Should().Be(4);
        detail.Boundary.Extent.North.Should().BeGreaterThan(detail.Boundary.Extent.South);
    }

    // ── Seeding ─────────────────────────────────────────────────────────────────────────────

    private Task<string> SeedListingsAsync(
        params (string Label, double? Latitude, double? Longitude)[] properties) =>
        SeedListingsAsync(areaPath: null, properties);

    private async Task<string> SeedListingsAsync(
        string? areaPath,
        params (string Label, double? Latitude, double? Longitude)[] properties)
    {
        Factory.EnsureDatabaseCreated();

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var run = Guid.NewGuid().ToString("N")[..8];
        var landlordId = LandlordProfileId.New();
        var ownerId = UserId.New();
        var now = DateTimeOffset.UtcNow;

        foreach (var (label, latitude, longitude) in properties)
        {
            var listing = HousingListing.Draft(
                HousingListingId.New(),
                landlordId,
                ownerId,
                HousingTitle.Create($"{label} {run}").Value,
                HousingSlug.Create($"viewport-{run}-{label}"),
                HousingDescription.Create(
                    $"A seeded property for the viewport suite: {label}.").Value,
                "apartment",
                HousingPrice.Create(900m, "GBP", RentPeriod.PerMonth, null).Value,
                HousingCapacity.Create(1, 1m, maximumOccupants: 2).Value,
                HousingAvailability.Create(null, totalUnits: 1).Value,
                HousingLocationContext.Create(
                    "GB", "Greater London", "London", "Peckham",
                    Guid.NewGuid(), areaPath).Value,
                latitude is null
                    ? PropertyAddress.Undisclosed
                    : PropertyAddress.Create(
                        latitude: latitude, longitude: longitude).Value,
                StayPolicy.Create(
                    acceptsShortStay: false,
                    acceptsLongStay: true,
                    isSharedAccommodation: false).Value,
                now).Value;

            listing.AttachMedia(Guid.NewGuid(), HousingMediaRole.Cover, null, now);

            // Asserted rather than ignored: a seed that silently failed to publish would make
            // every bounds assertion below pass for the wrong reason — an empty result set.
            listing.Publish(now).IsSuccess.Should().BeTrue();

            dbContext.Set<HousingListing>().Add(listing);
        }

        await dbContext.SaveChangesAsync();

        return run;
    }

    private async Task<string> SeedMarketplaceListingsAsync(
        params (string Label, string AreaPath)[] listings)
    {
        Factory.EnsureDatabaseCreated();

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var run = Guid.NewGuid().ToString("N")[..8];
        var sellerId = SellerProfileId.New();
        var ownerId = UserId.New();
        var now = DateTimeOffset.UtcNow;

        foreach (var (label, areaPath) in listings)
        {
            var listing = MarketplaceListing.Create(
                MarketplaceListingId.New(),
                sellerId,
                ownerId,
                ListingTitle.Create($"{label} {run}").Value,
                ListingSlug.Create($"viewport-{run}-{label}").Value,
                ListingDescription
                    .Create($"A seeded listing for the viewport suite: {label}.").Value,
                "electronics",
                ListingType.Negotiable,
                ProductCondition.Good,
                ListingPrice.Create(50m, "GBP", isNegotiable: true, minimumOffer: null).Value,
                ListingLocationContext.Create(
                    "GB", "Greater London", "London", "Peckham",
                    Guid.NewGuid(), areaPath).Value,
                now).Value;

            listing.AttachMedia(Guid.NewGuid(), ListingMediaRole.Cover);
            listing.Publish(now).IsSuccess.Should().BeTrue();

            dbContext.Set<MarketplaceListing>().Add(listing);
        }

        await dbContext.SaveChangesAsync();

        return run;
    }

    private async Task<string> SeedEventsAsync(
        params (string Label, double? Latitude, double? Longitude)[] events)
    {
        Factory.EnsureDatabaseCreated();

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var run = Guid.NewGuid().ToString("N")[..8];
        var organizerId = UserId.New();
        var now = DateTimeOffset.UtcNow;

        foreach (var (label, latitude, longitude) in events)
        {
            var communityEvent = global::Diaspora.Modules.CommunityLife.Domain.CommunityEvents.CommunityEvent.Draft(
                global::Diaspora.Modules.CommunityLife.Domain.Identifiers.CommunityEventId.New(),
                organizationId: null,
                organizerId,
                global::Diaspora.Modules.CommunityLife.Domain.ValueObjects.CommunityLifeTitle
                    .Create($"{label} {run}").Value,
                global::Diaspora.Modules.CommunityLife.Domain.ValueObjects.CommunityLifeSlug
                    .FromText($"viewport-{run}-{label}"),
                global::Diaspora.Modules.CommunityLife.Domain.ValueObjects.CommunityLifeDescription
                    .Create($"A seeded event for the viewport suite: {label}.", null).Value,
                "community-meeting",
                global::Diaspora.Modules.CommunityLife.Domain.ValueObjects.EventSchedule
                    .Create(now.AddDays(14), now.AddDays(14).AddHours(2), "Europe/London").Value,
                global::Diaspora.Modules.CommunityLife.Domain.ValueObjects.CommunityLifeLocationContext
                    .Create("GB", "Greater London", "London").Value,
                latitude is null
                    ? global::Diaspora.Modules.CommunityLife.Domain.ValueObjects.EventVenue.Create(
                        onlineUrl: "https://meet.diaspora.test/seeded").Value
                    : global::Diaspora.Modules.CommunityLife.Domain.ValueObjects.EventVenue.Create(
                        name: "Community Hall",
                        latitude: latitude,
                        longitude: longitude).Value,
                latitude is null
                    ? global::Diaspora.Modules.CommunityLife.Domain.Enums.EventAttendanceMode.Online
                    : global::Diaspora.Modules.CommunityLife.Domain.Enums.EventAttendanceMode.InPerson,
                now).Value;

            communityEvent.Publish(now).IsSuccess.Should().BeTrue();

            dbContext.Set<global::Diaspora.Modules.CommunityLife.Domain.CommunityEvents.CommunityEvent>()
                .Add(communityEvent);
        }

        await dbContext.SaveChangesAsync();

        return run;
    }

    private async Task<string> SeedProvidersAsync(
        params (string Label, double Latitude, double Longitude)[] providers)
    {
        Factory.EnsureDatabaseCreated();

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var run = Guid.NewGuid().ToString("N")[..8];
        var ownerId = UserId.New();
        var now = DateTimeOffset.UtcNow;

        foreach (var (label, latitude, longitude) in providers)
        {
            var name = global::Diaspora.Modules.Providers.Domain.ValueObjects.ProviderName
                .Create($"{label} {run}").Value;

            var provider = global::Diaspora.Modules.Providers.Domain.Providers.Provider.Register(
                global::Diaspora.Modules.Providers.Domain.Identifiers.ProviderId.New(),
                name,
                global::Diaspora.Modules.Providers.Domain.ValueObjects.ProviderSlug
                    .Create($"viewport-{run}-{label}").Value,
                global::Diaspora.Modules.Providers.Domain.Enums.ProviderType.Individual,
                global::Diaspora.Modules.Providers.Domain.Identifiers.ServiceCategoryId.New(),
                ownerId,
                global::Diaspora.Modules.Providers.Domain.Identifiers.ProviderProfileId.New(),
                global::Diaspora.Modules.Providers.Domain.Identifiers.ProviderMembershipId.New(),
                now,
                global::Diaspora.Modules.Providers.Domain.ValueObjects.ProviderLocationContext
                    .Create(
                        countryCode: "FJ",
                        cityName: "Suva",
                        latitude: latitude,
                        longitude: longitude)).Value;

            // A provider with no way to be contacted is not publishable — the module's own bar,
            // deliberately low and deliberately real.
            provider.AddContactMethod(
                global::Diaspora.Modules.Providers.Domain.Providers.ProviderContactMethod.Create(
                    global::Diaspora.Modules.Providers.Domain.Identifiers
                        .ProviderContactMethodId.New(),
                    global::Diaspora.Modules.Providers.Domain.Enums.ContactMethodKind.Phone,
                    "+6791234567",
                    isPrimary: true).Value).IsSuccess.Should().BeTrue();

            provider.Publish(now).IsSuccess.Should().BeTrue();

            dbContext.Set<global::Diaspora.Modules.Providers.Domain.Providers.Provider>()
                .Add(provider);
        }

        await dbContext.SaveChangesAsync();

        return run;
    }

    private async Task<string> SeedJobsAsync(params (string Label, string AreaPath)[] postings)
    {
        Factory.EnsureDatabaseCreated();

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var run = Guid.NewGuid().ToString("N")[..8];
        var employerId = global::Diaspora.Modules.Jobs.Domain.Identifiers.EmployerProfileId.New();
        var ownerId = UserId.New();
        var now = DateTimeOffset.UtcNow;

        foreach (var (label, areaPath) in postings)
        {
            var posting = global::Diaspora.Modules.Jobs.Domain.Postings.JobPosting.Create(
                global::Diaspora.Modules.Jobs.Domain.Identifiers.JobPostingId.New(),
                employerId,
                ownerId,
                global::Diaspora.Modules.Jobs.Domain.ValueObjects.JobTitle.Create($"{label} {run}").Value,
                global::Diaspora.Modules.Jobs.Domain.ValueObjects.JobSlug
                    .Create($"viewport-{run}-{label}").Value,
                global::Diaspora.Modules.Jobs.Domain.ValueObjects.JobDescription
                    .Create($"A seeded posting for the viewport suite: {label}.").Value,
                "technology",
                global::Diaspora.Modules.Jobs.Domain.Enums.JobType.FullTime,
                global::Diaspora.Modules.Jobs.Domain.Enums.ExperienceLevel.Mid,
                global::Diaspora.Modules.Jobs.Domain.ValueObjects.JobLocationContext.Create(
                    "GB", "Greater London", "London", Guid.NewGuid(), areaPath).Value,
                global::Diaspora.Modules.Jobs.Domain.ValueObjects.WorkArrangement.Create(
                    global::Diaspora.Modules.Jobs.Domain.Enums.WorkplaceType.OnSite, false).Value,
                now).Value;

            posting.Publish(now, openImmediately: true).IsSuccess.Should().BeTrue();

            dbContext.Set<global::Diaspora.Modules.Jobs.Domain.Postings.JobPosting>().Add(posting);
        }

        await dbContext.SaveChangesAsync();

        return run;
    }

    /// <summary>
    /// Writes a square outline around Peckham. The catalogue seeds none, and asserting on a
    /// boundary the deployment never imported would be asserting on nothing.
    /// </summary>
    private async Task<Guid> SetPeckhamBoundaryAsync()
    {
        Factory.EnsureDatabaseCreated();

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var area = await dbContext.Set<AdministrativeArea>()
            .FirstAsync(node => node.Level == AdministrativeLevel.Neighborhood
                && node.Name.Value == "Peckham");

        area.SetBoundary(GeographicBoundary.TryCreate(
        [
            GeoCoordinate.Create(51.48d, -0.08d),
            GeoCoordinate.Create(51.48d, -0.06d),
            GeoCoordinate.Create(51.46d, -0.06d),
            GeoCoordinate.Create(51.46d, -0.08d)
        ]).Value,
        BoundaryProvenance.TryCreate(
            "Test fixture", "CC0-1.0", "Test fixture", null, new DateOnly(2026, 9, 17)).Value);

        await dbContext.SaveChangesAsync();

        return area.Id.Value;
    }

    private async Task<string> NeighborhoodPathAsync(string name)
    {
        Factory.EnsureDatabaseCreated();

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var area = await dbContext.Set<AdministrativeArea>()
            .AsNoTracking()
            .FirstAsync(node => node.Level == AdministrativeLevel.Neighborhood
                && node.Name.Value == name);

        return area.Path.Value;
    }

    private async Task<(string London, string Manchester)> CityPathsAsync()
    {
        Factory.EnsureDatabaseCreated();

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var cities = await dbContext.Set<AdministrativeArea>()
            .AsNoTracking()
            .Where(node => node.Level == AdministrativeLevel.City)
            .ToListAsync();

        return (
            cities.First(city => city.Name.Value == "London").Path.Value,
            cities.First(city => city.Name.Value == "Manchester").Path.Value);
    }

    private Task<List<string>> CounterResourceTypesAsync()
    {
        using var scope = Factory.Services.CreateScope();

        var counters = scope.ServiceProvider
            .GetRequiredService<LocationResourceCounterRegistry>();

        return Task.FromResult(counters.All.Select(counter => counter.ResourceType).ToList());
    }

    // ── Plumbing ────────────────────────────────────────────────────────────────────────────

    private async Task<ListingPageDto> SearchHousingAsync(string query) =>
        await ReadDataAsync<ListingPageDto>(
            await HttpClient.GetAsync($"/api/v1/housing/search?limit=50&{query}"));

    private static async Task<T> ReadDataAsync<T>(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();

        // The body, not just the status. A bare EnsureSuccessStatusCode on a 500 tells a reader
        // nothing about which of six modules refused, and the problem document names it.
        response.IsSuccessStatusCode.Should().BeTrue(
            "the request should have succeeded, but the server answered {0}: {1}",
            response.StatusCode,
            body);

        using var document = JsonDocument.Parse(body);

        return document.RootElement.GetProperty("data").Deserialize<T>(JsonOptions)!;
    }

    // Re-declared by hand. Binding these to the modules' own records would make the tests pass
    // whenever the solution compiles, which is the one thing a contract test must never do.
    private sealed record ListingPageDto(List<ListingCardDto> Items);

    private sealed record ListingCardDto(string Title);

    private sealed record EventPageDto(List<ListingCardDto> Items);

    private sealed record JobPageDto(List<ListingCardDto> Items);

    private sealed record ProviderPageDto(List<ProviderCardDto> Items);

    private sealed record ProviderCardDto(string Name);

    private sealed record ClusterPageDto(
        List<ClusterDto> Clusters,
        string Level,
        List<string> ResourceTypes,
        int MinimumCellCount,
        int SuppressedCells,
        bool Truncated);

    private sealed record ClusterDto(
        Guid AreaId,
        string Name,
        string Slug,
        string Level,
        double Latitude,
        double Longitude,
        Dictionary<string, int> Counts,
        int Total);

    private sealed record HubDto(string Name, Dictionary<string, int> ResourceCounts);

    private sealed record ReferenceDto(
        string? Neighborhood, double? DistanceKm, string? Confidence);

    private sealed record AreaDetailDto(bool HasBoundary, BoundaryDto? Boundary);

    private sealed record BoundaryDto(
        List<VertexDto> Vertices, BoundsDto Extent, int SimplifiedTo);

    private sealed record VertexDto(double Latitude, double Longitude);

    private sealed record BoundsDto(double North, double South, double East, double West);
}
