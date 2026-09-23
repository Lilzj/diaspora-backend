using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Landlords;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Modules.Housing.Domain.ValueObjects;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// Finding a home by distance, by country and by what was typed — 15 Sep 2026.
/// <para>
/// <b>Seeded the way the homes on the live platform actually are:</b> a position and a town name,
/// and no area, because no create screen files a listing under one. Against rows like these,
/// "within 5 km" answered nothing (it narrowed to the nearest catalogued town's area), and typing
/// the town in lower case matched nothing (LIKE is case-sensitive on Postgres).
/// </para>
/// </summary>
[Collection(HousingCollection.Name)]
public sealed class HousingSearchByPlaceTests : PooledIdentityTestBase
{
    private const string Root = "/api/v1/housing";

    public HousingSearchByPlaceTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task A_radius_search_finds_the_homes_within_it_by_their_position_and_says_how_far()
    {
        var homes = await SeedAsync();
        Api.ClearAuthentication();

        var results = await IdentityApiClient.ReadDataAsync<SearchShape>(
            await HttpClient.GetAsync(
                $"{Root}/search?latitude=-20.2410&longitude=57.4769&radiusKm=5&countryCode=MU&limit=100"));

        var near = results.Items.Should().ContainSingle(item => item.Id == homes.RoseHill).Subject;
        near.DistanceKm.Should().NotBeNull().And.BeLessThan(1.5d);

        results.Items.Should().NotContain(item => item.Id == homes.Curepipe, "Curepipe is about 9 km away");
        results.Items.Should().NotContain(
            item => item.Id == homes.Unplaced, "a home with no position cannot be inside a circle");
    }

    [Fact]
    public async Task A_wider_radius_reaches_the_next_town()
    {
        var homes = await SeedAsync();
        Api.ClearAuthentication();

        var results = await IdentityApiClient.ReadDataAsync<SearchShape>(
            await HttpClient.GetAsync(
                $"{Root}/search?latitude=-20.2410&longitude=57.4769&radiusKm=25&countryCode=MU&limit=100"));

        results.Items.Should().Contain(item => item.Id == homes.RoseHill);
        results.Items.Should().Contain(item => item.Id == homes.Curepipe);
    }

    [Fact]
    public async Task A_country_search_finds_every_home_in_the_country()
    {
        var homes = await SeedAsync();
        Api.ClearAuthentication();

        var results = await IdentityApiClient.ReadDataAsync<SearchShape>(
            await HttpClient.GetAsync($"{Root}/search?countryCode=MU&limit=100"));

        results.Items.Select(item => item.Id).Should().Contain([homes.RoseHill, homes.Curepipe, homes.Unplaced]);
        results.Items.Should().OnlyContain(item => item.DistanceKm == null, "there was no point to measure from");
    }

    [Fact]
    public async Task Typing_a_town_in_lower_case_finds_its_homes()
    {
        var homes = await SeedAsync();
        Api.ClearAuthentication();

        var results = await IdentityApiClient.ReadDataAsync<SearchShape>(
            await HttpClient.GetAsync($"{Root}/search?query=rose%20hill&countryCode=MU&limit=100"));

        results.Items.Should().Contain(item => item.Id == homes.RoseHill);
        results.Items.Should().NotContain(item => item.Id == homes.Curepipe);
    }

    [Fact]
    public async Task A_town_search_keeps_the_homes_filed_without_an_area()
    {
        var homes = await SeedAsync();

        using var scope = Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IHousingListingRepository>();

        var page = await repository.QueryAsync(
            new HousingListingQueryFilter
            {
                CountryCode = "MU",
                AreaPathPrefix = "/MU/plaines-wilhems/rose-hill",
                AreaPathCityFallback = "Rose Hill",
                Limit = 100
            },
            cursor: null,
            limit: 100);

        page.Items.Select(listing => listing.Id.Value).Should().Contain(homes.RoseHill);
        page.Items.Select(listing => listing.Id.Value).Should().NotContain(homes.Curepipe);
    }

    private async Task<(Guid RoseHill, Guid Curepipe, Guid Unplaced)> SeedAsync()
    {
        Factory.EnsureDatabaseCreated();

        var now = DateTimeOffset.UtcNow;
        var landlordId = LandlordProfileId.New();
        var tag = Guid.NewGuid().ToString("N")[..8];

        var roseHill = Home(landlordId, $"Flat near the Plaza {tag}", "Rose Hill", -20.2415, 57.4772, now);
        var curepipe = Home(landlordId, $"House by the botanical garden {tag}", "Curepipe", -20.3163, 57.5259, now);
        var unplaced = Home(landlordId, $"Room in Port Louis {tag}", "Port Louis", null, null, now);

        using var seed = Factory.Services.CreateScope();
        var dbContext = seed.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        dbContext.Set<LandlordProfile>().Add(
            LandlordProfile.Register(
                landlordId,
                UserId.New(),
                LandlordIdentity.Create("Place Search Lettings", LandlordKind.Individual, null).Value,
                HousingSlug.Create($"place-search-lettings-{tag}"),
                "MU",
                LandlordContact.None,
                now).Value);

        dbContext.Set<HousingListing>().AddRange(roseHill, curepipe, unplaced);

        await dbContext.SaveChangesAsync();

        return (roseHill.Id.Value, curepipe.Id.Value, unplaced.Id.Value);
    }

    private static HousingListing Home(
        LandlordProfileId landlordId,
        string title,
        string town,
        double? latitude,
        double? longitude,
        DateTimeOffset now)
    {
        var listing = HousingListing.Draft(
            HousingListingId.New(),
            landlordId,
            UserId.New(),
            HousingTitle.Create(title).Value,
            HousingSlug.Create(title),
            HousingDescription.Create("A home seeded for the place search tests.").Value,
            "apartment",
            HousingPrice.Create(15_000m, "MUR", RentPeriod.PerMonth, null).Value,
            HousingCapacity.Create(2, 1m, maximumOccupants: 3).Value,
            HousingAvailability.Create(totalUnits: 1).Value,
            HousingLocationContext.Create("MU", cityName: town).Value,
            PropertyAddress.Create(
                "Royal Road", null, null, latitude, longitude, AddressVisibility.NeighborhoodOnly).Value,
            StayPolicy.Create(acceptsShortStay: false, acceptsLongStay: true, isSharedAccommodation: false).Value,
            now).Value;

        listing.AttachMedia(Guid.NewGuid(), HousingMediaRole.Cover, null, now).IsSuccess.Should().BeTrue();
        listing.Publish(now).IsSuccess.Should().BeTrue();

        return listing;
    }

    private sealed record SearchShape
    {
        public List<CardShape> Items { get; init; } = [];
    }

    private sealed record CardShape
    {
        public Guid Id { get; init; }

        public double? DistanceKm { get; init; }
    }
}
