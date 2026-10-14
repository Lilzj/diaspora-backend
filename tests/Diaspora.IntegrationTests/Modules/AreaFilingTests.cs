using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Modules.Housing.Domain.ValueObjects;
using Diaspora.Modules.Jobs.Application.Services;
using Diaspora.Modules.Location.Domain.Areas;
using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.Modules.Location.Extensibility;
using Diaspora.Modules.Location.Infrastructure.Hosting;
using Diaspora.Modules.Location.Integration;
using Diaspora.Modules.Marketplace.Application.Services;
using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.Modules.Marketplace.Domain.ValueObjects;
using Diaspora.Modules.Providers.Application.Services;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;
using JobDomain = Diaspora.Modules.Jobs.Domain;
using ProviderDomain = Diaspora.Modules.Providers.Domain;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// Every vertical files what it holds under the area it is in — 16 Sep 2026.
/// <para>
/// <b>The map's cluster endpoint was served, correct, and counted nothing.</b> It sums four resource
/// counters, each of which aggregates by prefix-matching a row's area path, and no row on the
/// platform had one: Housing filed only when a client sent an area id (none does), Jobs never asked
/// Location at all, and Marketplace and Providers filed only when the search resolver happened to
/// match the town. Housing was fixed first, from coordinates; this suite covers the rest, and the
/// sweep that files the rows already written.
/// </para>
/// <para>
/// <b>Every seeded row here starts the way production rows did:</b> a country and the names a
/// member typed, or a pair of coordinates, and no path. Suites that seed a path already set prove
/// the counters work on data the platform never produced — which is how this went unseen.
/// </para>
/// </summary>
public sealed class AreaFilingTests : IdentityTestBase
{
    private const string MauritiusViewport = "-20.0,-20.5,57.8,57.3";
    private const string PeckhamViewport = "51.49,51.46,-0.04,-0.10";

    public AreaFilingTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ── The locator ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// What a member types is not what the catalogue stores. LIKE is case-sensitive on Postgres,
    /// which is how "rose hill" once found nothing on this platform; the lookup lowers both sides.
    /// </summary>
    [Fact]
    public async Task A_town_typed_in_any_case_is_filed_under_the_catalogue_town()
    {
        var placement = await LocateAsync(new AreaFilingRequest
        {
            CountryCode = "MU",
            CityName = "  rose HILL "
        });

        placement.Should().NotBeNull();
        placement!.Name.Should().Be("Rose Hill");
        placement.Level.Should().Be(AdministrativeLevel.City);
        placement.Path.Should().Be(await PathOfAsync("Rose Hill", AdministrativeLevel.City));
    }

    /// <summary>"London" without a country is three cities on two continents.</summary>
    [Fact]
    public async Task A_town_named_without_a_country_is_not_filed()
    {
        (await LocateAsync(new AreaFilingRequest { CityName = "Rose Hill" })).Should().BeNull();
    }

    /// <summary>Exact names only: a town the catalogue lacks is not rounded to one it has.</summary>
    [Fact]
    public async Task A_town_the_catalogue_does_not_hold_is_not_filed()
    {
        (await LocateAsync(new AreaFilingRequest { CountryCode = "MU", CityName = "Rose" }))
            .Should().BeNull("\"Rose\" is not Rose Hill, however much of it matches");
    }

    /// <summary>A member who picked a neighbourhood from the hierarchy is not second-guessed.</summary>
    [Fact]
    public async Task An_area_the_row_already_names_is_the_answer()
    {
        var peckham = await AreaAsync("Peckham", AdministrativeLevel.Neighborhood);

        var placement = await LocateAsync(new AreaFilingRequest
        {
            CountryCode = "GB",
            AreaId = peckham.Id.Value,
            CityName = "Manchester"
        });

        placement!.AreaId.Should().Be(peckham.Id.Value);
        placement.Level.Should().Be(AdministrativeLevel.Neighborhood);
    }

    /// <summary>An area id from the wrong country is a mistake, and the row's own names win.</summary>
    [Fact]
    public async Task An_area_in_another_country_is_not_taken_on_trust()
    {
        var peckham = await AreaAsync("Peckham", AdministrativeLevel.Neighborhood);

        var placement = await LocateAsync(new AreaFilingRequest
        {
            CountryCode = "MU",
            AreaId = peckham.Id.Value,
            CityName = "Curepipe"
        });

        placement!.Name.Should().Be("Curepipe");
    }

    /// <summary>
    /// The ladder. Ealing is over fourteen kilometres from every London neighbourhood the catalogue
    /// holds, so it is in none of them — and twelve from central London, so it is in London.
    /// </summary>
    [Fact]
    public async Task A_point_between_neighbourhoods_is_filed_under_its_town()
    {
        var placement = await LocateAsync(new AreaFilingRequest
        {
            CountryCode = "GB",
            Latitude = 51.5130d,
            Longitude = -0.3089d
        });

        placement!.Name.Should().Be("London");
        placement.Level.Should().Be(AdministrativeLevel.City);
    }

    /// <summary>And a point near a neighbourhood's centre is filed in the neighbourhood.</summary>
    [Fact]
    public async Task A_point_near_a_neighbourhood_is_filed_in_it()
    {
        var placement = await LocateAsync(new AreaFilingRequest
        {
            CountryCode = "GB",
            Latitude = 51.4750d,
            Longitude = -0.0700d
        });

        placement!.Name.Should().Be("Peckham");
        placement.Level.Should().Be(AdministrativeLevel.Neighborhood);
    }

    /// <summary>
    /// The misfiling nearest-centre alone makes. Rose Hill and Quatre Bornes are two and a half
    /// kilometres apart; this pin is under half a kilometre from Rose Hill's centre and over two from
    /// Quatre Bornes'. The owner said Quatre Bornes, and a pin may refine a town, not replace it.
    /// </summary>
    [Fact]
    public async Task A_pin_nearer_the_next_town_is_filed_in_the_town_the_owner_named()
    {
        var placement = await LocateAsync(new AreaFilingRequest
        {
            CountryCode = "MU",
            CityName = "Quatre Bornes",
            Latitude = -20.2450d,
            Longitude = 57.4800d
        });

        placement!.Name.Should().Be("Quatre Bornes Center");
        placement.Level.Should().Be(AdministrativeLevel.Neighborhood);
    }

    /// <summary>
    /// With no town named, a pin goes to the nearest centre — unless a stored outline says which
    /// town it is in.
    /// <para>
    /// Until 17 Sep 2026 this asserted that the pin above, named nothing, went to Rose Hill: there
    /// was nothing better to go on. There is now. The Quatre Bornes outline imported from
    /// OpenStreetMap runs 55 metres north of that pin, so the pin is in Quatre Bornes, whichever
    /// centre is nearer; a pin a few metres the other side of the line still goes to Rose Hill.
    /// <c>AreaBoundaryTests</c> holds the outline rules themselves.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_pin_naming_no_town_is_filed_at_the_nearest_centre_unless_an_outline_places_it()
    {
        var northOfTheLine = await LocateAsync(new AreaFilingRequest
        {
            CountryCode = "MU",
            Latitude = -20.2430d,
            Longitude = 57.4800d
        });

        northOfTheLine!.Name.Should().Be("Rose Hill Central");

        var southOfTheLine = await LocateAsync(new AreaFilingRequest
        {
            CountryCode = "MU",
            Latitude = -20.2450d,
            Longitude = 57.4800d
        });

        southOfTheLine!.Name.Should().Be("Quatre Bornes Center");
    }

    /// <summary>
    /// A pin hundreds of kilometres from the town the owner named does not move the row out of it.
    /// The pin is the weaker witness: it is often where the phone was when the form was filled in.
    /// </summary>
    [Fact]
    public async Task A_pin_far_from_the_named_town_leaves_the_row_in_that_town()
    {
        var placement = await LocateAsync(new AreaFilingRequest
        {
            CountryCode = "GB",
            CityName = "Manchester",
            Latitude = 51.4742d,
            Longitude = -0.0691d
        });

        placement!.Name.Should().Be("Manchester");
        placement.Level.Should().Be(AdministrativeLevel.City);
    }

    // ── The verticals' own filing ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_marketplace_listing_naming_only_its_town_is_filed_under_the_town()
    {
        using var scope = CreateScope();
        var filing = scope.ServiceProvider.GetRequiredService<ListingAreaFiling>();

        var filed = await filing.FileAsync(
            ListingLocationContext.Create("MU", "Plaines Wilhems", "quatre bornes").Value);

        filed.AreaPath.Should().Be(await PathOfAsync("Quatre Bornes", AdministrativeLevel.City));
        filed.CityName.Should().Be("quatre bornes", "filing never rewrites what the seller typed");
    }

    [Fact]
    public async Task A_marketplace_listing_naming_its_neighbourhood_is_filed_in_it()
    {
        using var scope = CreateScope();
        var filing = scope.ServiceProvider.GetRequiredService<ListingAreaFiling>();

        var filed = await filing.FileAsync(
            ListingLocationContext.Create("MU", "Plaines Wilhems", "Curepipe", "Curepipe Town").Value);

        filed.AreaPath.Should().Be(await PathOfAsync("Curepipe Town", AdministrativeLevel.Neighborhood));
    }

    [Fact]
    public async Task A_job_posting_is_filed_under_the_town_it_names()
    {
        using var scope = CreateScope();
        var filing = scope.ServiceProvider.GetRequiredService<JobAreaFiling>();

        var filed = await filing.FileAsync(
            JobDomain.ValueObjects.JobLocationContext.Create("MU", null, "Port Louis").Value);

        filed.AreaPath.Should().Be(await PathOfAsync("Port Louis", AdministrativeLevel.City),
            "the city, not the district of the same name, because the posting named a city");
    }

    [Fact]
    public async Task A_provider_is_filed_from_its_coordinates()
    {
        using var scope = CreateScope();
        var filing = scope.ServiceProvider.GetRequiredService<ProviderAreaFiling>();

        var filed = await filing.FileAsync(
            ProviderDomain.ValueObjects.ProviderLocationContext.Create(
                countryCode: "GB", latitude: 51.4742d, longitude: -0.0691d));

        filed.HierarchyPath.Should().Be(await PathOfAsync("Peckham", AdministrativeLevel.Neighborhood));
    }

    /// <summary>
    /// The provider registration case: the search resolver did not recognise the town, so the
    /// stored place has no city — but the provider did type one.
    /// </summary>
    [Fact]
    public async Task A_provider_whose_scope_lost_the_town_is_filed_by_the_town_it_typed()
    {
        using var scope = CreateScope();
        var filing = scope.ServiceProvider.GetRequiredService<ProviderAreaFiling>();

        var filed = await filing.FileAsync(
            ProviderDomain.ValueObjects.ProviderLocationContext.Create(countryCode: "MU"),
            namedCity: "Rose Hill");

        filed.HierarchyPath.Should().Be(await PathOfAsync("Rose Hill", AdministrativeLevel.City));
    }

    // ── The sweep ────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Every vertical contributes a source, discovered rather than listed — the same guarantee the
    /// resource counters give. A vertical that ships no source is a vertical whose old rows stay
    /// invisible on the map for ever, silently.
    /// </summary>
    [Fact]
    public void Every_vertical_that_counts_by_area_contributes_a_filing_source()
    {
        using var scope = CreateScope();

        scope.ServiceProvider.GetServices<IAreaFilingSource>()
            .Select(source => source.ResourceType)
            .Should().BeEquivalentTo(["housing", "marketplace", "jobs", "providers"]);
    }

    /// <summary>
    /// The sweep is not hosted since 14 Oct 2026: the owner retired the place catalogue for filing
    /// and discovery, and a sweep that kept writing area paths would only write data nothing reads.
    /// </summary>
    [Fact]
    public void The_sweep_is_no_longer_hosted()
    {
        Factory.Services.GetServices<IHostedService>().OfType<AreaFilingSweep>()
            .Should().BeEmpty();
    }

    /// <summary>
    /// The backfill. Rows written the way production's were — names or coordinates and no path —
    /// are filed by one pass, and each lands where a new row written today would.
    /// </summary>
    [Fact]
    public async Task The_sweep_files_what_every_vertical_wrote_before_filing_existed()
    {
        var seeded = await SeedUnfiledRowsAsync();

        await RunSweepAsync();

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var home = await db.Set<HousingListing>().AsNoTracking()
            .SingleAsync(listing => listing.Id == seeded.HomeId);
        home.Location.AreaPath.Should().Be(await PathOfAsync("Rose Hill", AdministrativeLevel.City));
        home.Location.HasNeighborhood.Should().BeFalse("the landlord named a town, not a neighbourhood");

        var item = await db.Set<MarketplaceListing>().AsNoTracking()
            .SingleAsync(listing => listing.Id == seeded.ItemId);
        item.Location.AreaPath.Should().Be(await PathOfAsync("Quatre Bornes", AdministrativeLevel.City));

        var job = await db.Set<JobDomain.Postings.JobPosting>().AsNoTracking()
            .SingleAsync(posting => posting.Id == seeded.JobId);
        job.Location.AreaPath.Should().Be(await PathOfAsync("Curepipe", AdministrativeLevel.City));

        var provider = await db.Set<ProviderDomain.Providers.Provider>().AsNoTracking()
            .SingleAsync(row => row.Id == seeded.ProviderId);
        provider.Location.HierarchyPath.Should().Be(
            await PathOfAsync("Peckham", AdministrativeLevel.Neighborhood));
    }

    /// <summary>
    /// Idempotent: a second pass — or a second machine running the first — finds nothing to file,
    /// and a row it filed is not refiled somewhere else.
    /// </summary>
    [Fact]
    public async Task A_second_pass_files_nothing_and_moves_nothing()
    {
        var seeded = await SeedUnfiledRowsAsync();

        await RunSweepAsync();
        var again = await RunSweepAsync();

        again.Values.Should().AllSatisfy(count => count.Should().Be(0));

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        (await db.Set<JobDomain.Postings.JobPosting>().AsNoTracking()
                .SingleAsync(posting => posting.Id == seeded.JobId))
            .Location.AreaPath.Should().Be(await PathOfAsync("Curepipe", AdministrativeLevel.City));
    }

    /// <summary>
    /// Filing is a correction, not an edit: a backfilled row is not "recently updated", and its
    /// revision does not move under an owner who has the edit screen open.
    /// </summary>
    [Fact]
    public async Task Filing_a_row_is_not_an_edit()
    {
        var seeded = await SeedUnfiledRowsAsync();

        (int Revision, DateTimeOffset? ModifiedAt) before;

        using (var scope = CreateScope())
        {
            var home = await scope.ServiceProvider.GetRequiredService<DiasporaDbContext>()
                .Set<HousingListing>().AsNoTracking()
                .SingleAsync(listing => listing.Id == seeded.HomeId);

            before = (home.Revision, home.ModifiedAt);
        }

        await RunSweepAsync();

        using (var scope = CreateScope())
        {
            var home = await scope.ServiceProvider.GetRequiredService<DiasporaDbContext>()
                .Set<HousingListing>().AsNoTracking()
                .SingleAsync(listing => listing.Id == seeded.HomeId);

            home.Location.AreaPath.Should().NotBeNull();
            home.Revision.Should().Be(before.Revision,
                "an owner with the edit screen open must not have their save refused over it");
            home.ModifiedAt.Should().Be(before.ModifiedAt);
        }
    }

    /// <summary>
    /// <b>The map no longer depends on filing</b> — 14 Oct 2026, Phase 2B of retiring the place
    /// catalogue. Clusters are grid cells over each row's own coordinates, so filing rows under
    /// catalogue areas moves no count: the totals are the same before the sweep and after it.
    /// (Until then this test proved the opposite — that only filed rows reached the map.)
    /// </summary>
    [Fact]
    public async Task Filing_rows_moves_no_map_cluster_count()
    {
        await SeedUnfiledRowsAsync();

        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var towns = await ClustersAsync(MauritiusViewport, "City", "housing,marketplace,jobs");
        var peckham = await ClustersAsync(PeckhamViewport, "Neighborhood", "providers");

        await RunSweepAsync();

        var townsAfter = await ClustersAsync(MauritiusViewport, "City", "housing,marketplace,jobs");
        var peckhamAfter = await ClustersAsync(PeckhamViewport, "Neighborhood", "providers");

        foreach (var type in new[] { "housing", "marketplace", "jobs" })
        {
            Total(townsAfter, type).Should().Be(Total(towns, type), $"filing moved no {type} on the map");
        }

        Total(peckhamAfter, "providers").Should().Be(Total(peckham, "providers"));
        Total(peckhamAfter, "providers").Should().BeGreaterThan(
            0, "the provider seeded with Peckham's coordinates is on the map by its pin alone");
    }

    // ── Through the handlers ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The wiring, over HTTP, since 14 Oct 2026: a listing is no longer filed under a catalogue area
    /// on create, and an edit that moves it writes no new one. It is found by its coordinates, its
    /// country and the town its seller typed.
    /// </summary>
    [Fact]
    public async Task A_listing_is_not_filed_on_create_nor_refiled_when_an_edit_moves_it()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var seller = await HttpClient.PostAsJsonAsync(
            "/api/v1/marketplace/sellers",
            new { displayName = "Filing Tester", countryCode = "MU", kind = "Individual" },
            WireJson);

        seller.StatusCode.Should().Be(HttpStatusCode.Created, await seller.Content.ReadAsStringAsync());

        var title = $"Rice cooker {Guid.NewGuid():N}";

        var created = await HttpClient.PostAsJsonAsync(
            "/api/v1/marketplace/listings",
            new
            {
                title,
                description = "Barely used, works perfectly, collection only.",
                categoryKey = "bicycles",
                countryCode = "MU",
                neighbourhood = "Curepipe Town",
                listingType = "FixedPrice",
                condition = "Good",
                price = 800m,
                currency = "MUR",
                offersPickup = true,
                mediaIds = new[] { Guid.NewGuid() }
            },
            WireJson);

        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());

        var listingId = (await IdentityApiClient.ReadDataAsync<CreatedListingShape>(created)).ListingId;

        (await StoredListingPathAsync(listingId)).Should().BeNull(
            "nothing files a row under the catalogue on create any more");

        var edited = await HttpClient.PutAsJsonAsync(
            $"/api/v1/marketplace/listings/{listingId}",
            new
            {
                title,
                description = "Barely used, works perfectly, collection only.",
                countryCode = "MU",
                regionName = "Plaines Wilhems",
                cityName = "Rose Hill",
                condition = "Good",
                price = 800m,
                currency = "MUR",
                offersPickup = true
            },
            WireJson);

        edited.StatusCode.Should().Be(HttpStatusCode.OK, await edited.Content.ReadAsStringAsync());

        (await StoredListingPathAsync(listingId)).Should().BeNull(
            "an edit writes no catalogue area either");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────────────────────

    private static readonly JsonSerializerOptions WireJson = new(JsonSerializerDefaults.Web);

    private async Task<string?> StoredListingPathAsync(Guid listingId)
    {
        using var scope = CreateScope();

        return (await scope.ServiceProvider.GetRequiredService<DiasporaDbContext>()
            .Set<MarketplaceListing>().AsNoTracking()
            .SingleAsync(listing => listing.Id == MarketplaceListingId.From(listingId)))
            .Location.AreaPath;
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

    private async Task<AdministrativeArea> AreaAsync(string name, AdministrativeLevel level)
    {
        using var scope = CreateScope();

        return await scope.ServiceProvider.GetRequiredService<DiasporaDbContext>()
            .Set<AdministrativeArea>().AsNoTracking()
            .SingleAsync(area => area.Level == level && area.Name.Value == name);
    }

    private async Task<string> PathOfAsync(string name, AdministrativeLevel level) =>
        (await AreaAsync(name, level)).Path.Value;

    private async Task<IReadOnlyDictionary<string, int>> RunSweepAsync()
    {
        Factory.EnsureDatabaseCreated();

        // No longer hosted (14 Oct 2026): the class stays until Phase 3 deletes it, so its own
        // behaviour is still exercised here, built directly rather than found among the services.
        var sweep = ActivatorUtilities.CreateInstance<AreaFilingSweep>(Factory.Services);

        return await sweep.RunOnceAsync();
    }

    private async Task<ClusterPageShape> ClustersAsync(string bounds, string level, string resourceTypes) =>
        await IdentityApiClient.ReadDataAsync<ClusterPageShape>(
            await HttpClient.GetAsync(
                $"/api/v1/location/map/clusters?bounds={bounds}&level={level}&resourceTypes={resourceTypes}"));

    private static int Total(ClusterPageShape page, string resourceType) =>
        page.Clusters.Sum(cluster => cluster.Counts.TryGetValue(resourceType, out var count) ? count : 0);

    /// <summary>
    /// One row per vertical, written as production's were: a Rose Hill home with no coordinates, a
    /// Quatre Bornes listing, a Curepipe job and a provider with Peckham's coordinates — no paths.
    /// </summary>
    private async Task<SeededRows> SeedUnfiledRowsAsync()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var run = Guid.NewGuid().ToString("N")[..8];
        var now = DateTimeOffset.UtcNow;

        var home = HousingListing.Draft(
            HousingListingId.New(),
            LandlordProfileId.New(),
            UserId.New(),
            HousingTitle.Create($"Room in Rose Hill {run}").Value,
            HousingSlug.Create($"area-filing-home-{run}"),
            HousingDescription.Create("A room listed the way production's were: a town and no pin.").Value,
            "studio",
            HousingPrice.Create(9500m, "MUR", RentPeriod.PerMonth, null).Value,
            HousingCapacity.Create(1, 1m, maximumOccupants: 1).Value,
            HousingAvailability.Create(null, totalUnits: 1).Value,
            HousingLocationContext.Create("MU", "Plaines Wilhems", "Rose Hill").Value,
            PropertyAddress.Create().Value,
            StayPolicy.Create(acceptsShortStay: false, acceptsLongStay: true, isSharedAccommodation: true).Value,
            now).Value;

        home.AttachMedia(Guid.NewGuid(), HousingMediaRole.Cover, null, now);
        home.Publish(now).IsSuccess.Should().BeTrue();
        home.Location.AreaPath.Should().BeNull("otherwise this test would prove nothing");
        db.Set<HousingListing>().Add(home);

        var item = MarketplaceListing.Create(
            MarketplaceListingId.New(),
            SellerProfileId.New(),
            UserId.New(),
            ListingTitle.Create($"Basket set {run}").Value,
            ListingSlug.Create($"area-filing-item-{run}").Value,
            ListingDescription.Create("A listing that names its town and nothing else.").Value,
            "electronics",
            ListingType.Negotiable,
            ProductCondition.Good,
            ListingPrice.Create(500m, "MUR", isNegotiable: true, minimumOffer: null).Value,
            ListingLocationContext.Create("MU", "Plaines Wilhems", "Quatre Bornes").Value,
            now).Value;

        item.AttachMedia(Guid.NewGuid(), ListingMediaRole.Cover);
        item.Publish(now).IsSuccess.Should().BeTrue();
        item.Location.AreaPath.Should().BeNull();
        db.Set<MarketplaceListing>().Add(item);

        var job = JobDomain.Postings.JobPosting.Create(
            JobDomain.Identifiers.JobPostingId.New(),
            JobDomain.Identifiers.EmployerProfileId.New(),
            UserId.New(),
            JobDomain.ValueObjects.JobTitle.Create($"Care assistant {run}").Value,
            JobDomain.ValueObjects.JobSlug.Create($"area-filing-job-{run}").Value,
            JobDomain.ValueObjects.JobDescription.Create("A posting in a town, filed nowhere.").Value,
            "technology",
            JobDomain.Enums.JobType.FullTime,
            JobDomain.Enums.ExperienceLevel.Mid,
            JobDomain.ValueObjects.JobLocationContext.Create("MU", null, "Curepipe").Value,
            JobDomain.ValueObjects.WorkArrangement.Create(JobDomain.Enums.WorkplaceType.OnSite, false).Value,
            now).Value;

        job.Publish(now, openImmediately: true).IsSuccess.Should().BeTrue();
        job.Location.AreaPath.Should().BeNull();
        db.Set<JobDomain.Postings.JobPosting>().Add(job);

        var provider = ProviderDomain.Providers.Provider.Register(
            ProviderDomain.Identifiers.ProviderId.New(),
            ProviderDomain.ValueObjects.ProviderName.Create($"Peckham plumbing {run}").Value,
            ProviderDomain.ValueObjects.ProviderSlug.Create($"area-filing-provider-{run}").Value,
            ProviderDomain.Enums.ProviderType.Individual,
            ProviderDomain.Identifiers.ServiceCategoryId.New(),
            UserId.New(),
            ProviderDomain.Identifiers.ProviderProfileId.New(),
            ProviderDomain.Identifiers.ProviderMembershipId.New(),
            now,
            ProviderDomain.ValueObjects.ProviderLocationContext.Create(
                countryCode: "GB", latitude: 51.4742d, longitude: -0.0691d)).Value;

        provider.AddContactMethod(
            ProviderDomain.Providers.ProviderContactMethod.Create(
                ProviderDomain.Identifiers.ProviderContactMethodId.New(),
                ProviderDomain.Enums.ContactMethodKind.Phone,
                "+442079460000",
                isPrimary: true).Value).IsSuccess.Should().BeTrue();

        provider.Publish(now).IsSuccess.Should().BeTrue();
        provider.Location.HierarchyPath.Should().BeNull();
        db.Set<ProviderDomain.Providers.Provider>().Add(provider);

        await db.SaveChangesAsync();

        return new SeededRows(home.Id, item.Id, job.Id, provider.Id);
    }

    private sealed record SeededRows(
        HousingListingId HomeId,
        MarketplaceListingId ItemId,
        JobDomain.Identifiers.JobPostingId JobId,
        ProviderDomain.Identifiers.ProviderId ProviderId);

    private sealed record CreatedListingShape
    {
        public Guid ListingId { get; init; }
    }

    private sealed record ClusterPageShape
    {
        public List<ClusterShape> Clusters { get; init; } = [];
    }

    private sealed record ClusterShape
    {
        public string Name { get; init; } = string.Empty;

        public Dictionary<string, int> Counts { get; init; } = [];
    }
}
