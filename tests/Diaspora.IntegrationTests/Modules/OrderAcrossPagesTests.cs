using System.Net.Http.Json;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Modules.Housing.Domain.ValueObjects;
using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.Modules.Marketplace.Domain.Sellers;
using Diaspora.Modules.Marketplace.Domain.ValueObjects;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using ProviderDomain = Diaspora.Modules.Providers.Domain;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// "Distance" and "Rating" ordered across every page, not inside one — 15 Sep 2026.
/// <para>
/// <b>Each page was sorted on its own.</b> Homes were ordered by distance on the phone, twenty at a
/// time; a provider distance search reordered one page by proximity; marketplace "Rating" sorted
/// whatever had loaded. A nearer home or provider, or a better-rated seller, on page two stayed
/// below page one. Every test here pages one or two rows at a time through rows seeded in the
/// opposite order to the one expected, so a page-by-page sort cannot pass.
/// </para>
/// </summary>
[Collection(DiscoveryCollection.Name)]
public sealed class OrderAcrossPagesTests : PooledIdentityTestBase
{
    public OrderAcrossPagesTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ── Homes: nearest first ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Measured from what a stranger is shown. The next-door home's address is published only to
    /// its neighbourhood, so its pin is fuzzed onto the search point and it comes first — although
    /// its real position is further away than the public home's. Sorting by the stored coordinate
    /// would have put the public home first, and in doing so told the member which fuzzed home was
    /// really nearer.
    /// </summary>
    [Fact]
    public async Task Nearest_homes_page_in_distance_order_measured_from_the_published_position()
    {
        var run = await SeedHomesAsync();
        Api.ClearAuthentication();

        var (titles, appliedSorts, pages) = await PageHomesAsync(
            $"query={run}&countryCode=MU&latitude=-20.2400&longitude=57.4800&radiusKm=5&sort=Nearest&limit=1");

        titles.Should().Equal(
            $"zulu fuzzed next door {run}",
            $"yankee public street {run}",
            $"x-ray fuzzed two km {run}",
            $"whiskey public four km {run}");

        pages.Should().Be(4, "one home per page, and the one outside the circle is not a fifth");
        appliedSorts.Should().OnlyContain(sort => sort == "Nearest");
    }

    [Fact]
    public async Task Nearest_without_a_point_says_it_answered_newest_first()
    {
        var run = await SeedHomesAsync();
        Api.ClearAuthentication();

        var page = await IdentityApiClient.ReadDataAsync<HomePageShape>(
            await HttpClient.GetAsync($"/api/v1/housing/search?query={run}&countryCode=MU&sort=Nearest&limit=50"));

        page.AppliedSort.Should().Be("Newest");
        page.Items.Should().HaveCount(5);
    }

    // ── Providers: nearest first ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Nearest_providers_page_in_distance_order()
    {
        var run = await SeedProvidersAsync(
            ("echo seven km", -20.3030, 57.4769),
            ("alpha half km", -20.2455, 57.4769),
            ("delta four km", -20.2770, 57.4769),
            ("outside twenty km", -20.4200, 57.4769),
            ("charlie two km", -20.2590, 57.4769),
            ("bravo one km", -20.2500, 57.4769));

        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var names = new List<string>();
        var appliedSorts = new List<string?>();
        string? cursor = null;
        var pages = 0;

        do
        {
            var query = $"/api/v1/providers?query={run}&latitude=-20.2410&longitude=57.4769&radiusKm=10&sort=distance&limit=2"
                + (cursor is null ? string.Empty : $"&cursor={Uri.EscapeDataString(cursor)}");

            var page = await IdentityApiClient.ReadDataAsync<ProviderPageShape>(await HttpClient.GetAsync(query));

            names.AddRange(page.Items.Select(item => item.Name));
            appliedSorts.Add(page.AppliedSort);
            cursor = page.HasMore ? page.NextCursor : null;
            pages++;
        }
        while (cursor is not null && pages < 10);

        names.Should().Equal(
            $"alpha half km {run}",
            $"bravo one km {run}",
            $"charlie two km {run}",
            $"delta four km {run}",
            $"echo seven km {run}");

        appliedSorts.Should().OnlyContain(sort => sort == "Distance");
    }

    /// <summary>
    /// With no circle there is no distance order to give, and the providers come back in the
    /// recommended order under that name — not ranked by proximity a page at a time, which is the
    /// order this replaced, reported as something else.
    /// </summary>
    [Fact]
    public async Task Distance_without_a_point_says_providers_answered_in_the_recommended_order()
    {
        var run = await SeedProvidersAsync(
            ("golf near", -20.2455, 57.4769),
            ("hotel far", -20.3030, 57.4769));

        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var page = await IdentityApiClient.ReadDataAsync<ProviderPageShape>(
            await HttpClient.GetAsync($"/api/v1/providers?query={run}&sort=distance&limit=20"));

        page.AppliedSort.Should().Be("Recommended");
        page.Items.Should().HaveCount(2);
    }

    // ── Items: highest-rated seller first ───────────────────────────────────────────────────

    [Fact]
    public async Task Highest_rated_items_page_by_their_sellers_rating_across_pages()
    {
        var run = await SeedRatedItemsAsync();
        Api.ClearAuthentication();

        var titles = new List<string>();
        string? cursor = null;
        var pages = 0;

        do
        {
            var response = await HttpClient.PostAsJsonAsync(
                "/api/v1/marketplace/search",
                new { text = run, sort = "HighestRated", limit = 2, cursor });

            var page = await IdentityApiClient.ReadDataAsync<ItemPageShape>(response);

            titles.AddRange(page.Items.Select(item => item.Title));
            cursor = page.HasMore ? page.NextCursor : null;
            pages++;
        }
        while (cursor is not null && pages < 10);

        titles.Should().Equal(
            $"five stars {run}",
            $"four and a half from four {run}",
            $"four and a half from two {run}",
            $"unrated newer {run}",
            $"unrated older {run}");

        pages.Should().Be(3);
    }

    // ── Plumbing ────────────────────────────────────────────────────────────────────────────

    private async Task<(List<string> Titles, List<string> AppliedSorts, int Pages)> PageHomesAsync(string query)
    {
        var titles = new List<string>();
        var appliedSorts = new List<string>();
        string? cursor = null;
        var pages = 0;

        do
        {
            var url = $"/api/v1/housing/search?{query}"
                + (cursor is null ? string.Empty : $"&cursor={Uri.EscapeDataString(cursor)}");

            var page = await IdentityApiClient.ReadDataAsync<HomePageShape>(await HttpClient.GetAsync(url));

            titles.AddRange(page.Items.Select(item => item.Title));
            appliedSorts.Add(page.AppliedSort);
            cursor = page.HasMore ? page.NextCursor : null;
            pages++;
        }
        while (cursor is not null && pages < 10);

        return (titles, appliedSorts, pages);
    }

    /// <summary>
    /// Five homes around a search point on the fuzzing grid. Titles, and so slugs, run in the
    /// reverse of distance order, so an order by slug or by insertion cannot pass.
    /// </summary>
    private async Task<string> SeedHomesAsync()
    {
        Factory.EnsureDatabaseCreated();

        var run = Guid.NewGuid().ToString("N")[..8];
        var landlordId = LandlordProfileId.New();
        var now = DateTimeOffset.UtcNow;

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        dbContext.Set<HousingListing>().AddRange(
            // 13 km: outside a 5 km circle.
            Home(landlordId, $"victor outside {run}", -20.3600, 57.4800, AddressVisibility.NeighborhoodOnly, now),
            // 4.4 km, public.
            Home(landlordId, $"whiskey public four km {run}", -20.2800, 57.4800, AddressVisibility.Public, now),
            // Stored 2.2 km away, published at 2.2 km.
            Home(landlordId, $"x-ray fuzzed two km {run}", -20.2600, 57.4800, AddressVisibility.NeighborhoodOnly, now),
            // 0.33 km, public: nearer than the next home really is.
            Home(landlordId, $"yankee public street {run}", -20.2430, 57.4800, AddressVisibility.Public, now),
            // Stored 0.55 km away, published on the search point itself.
            Home(landlordId, $"zulu fuzzed next door {run}", -20.2449, 57.4800, AddressVisibility.NeighborhoodOnly, now));

        await dbContext.SaveChangesAsync();

        return run;
    }

    private static HousingListing Home(
        LandlordProfileId landlordId,
        string title,
        double latitude,
        double longitude,
        AddressVisibility visibility,
        DateTimeOffset now)
    {
        var listing = HousingListing.Draft(
            HousingListingId.New(),
            landlordId,
            UserId.New(),
            HousingTitle.Create(title).Value,
            HousingSlug.Create(title),
            HousingDescription.Create("A home seeded for the nearest-first tests.").Value,
            "apartment",
            HousingPrice.Create(15_000m, "MUR", RentPeriod.PerMonth, null).Value,
            HousingCapacity.Create(2, 1m, maximumOccupants: 3).Value,
            HousingAvailability.Create(totalUnits: 1).Value,
            HousingLocationContext.Create("MU", cityName: "Rose Hill").Value,
            PropertyAddress.Create("Royal Road", null, null, latitude, longitude, visibility).Value,
            StayPolicy.Create(acceptsShortStay: false, acceptsLongStay: true, isSharedAccommodation: false).Value,
            now).Value;

        listing.AttachMedia(Guid.NewGuid(), HousingMediaRole.Cover, null, now).IsSuccess.Should().BeTrue();
        listing.Publish(now).IsSuccess.Should().BeTrue();

        return listing;
    }

    private async Task<string> SeedProvidersAsync(params (string Label, double Latitude, double Longitude)[] providers)
    {
        Factory.EnsureDatabaseCreated();

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var run = Guid.NewGuid().ToString("N")[..8];
        var ownerId = UserId.New();
        var now = DateTimeOffset.UtcNow;

        foreach (var (label, latitude, longitude) in providers)
        {
            var provider = ProviderDomain.Providers.Provider.Register(
                ProviderDomain.Identifiers.ProviderId.New(),
                ProviderDomain.ValueObjects.ProviderName.Create($"{label} {run}").Value,
                ProviderDomain.ValueObjects.ProviderSlug.Create($"nearest-{run}-{label.Replace(' ', '-')}").Value,
                ProviderDomain.Enums.ProviderType.Individual,
                ProviderDomain.Identifiers.ServiceCategoryId.New(),
                ownerId,
                ProviderDomain.Identifiers.ProviderProfileId.New(),
                ProviderDomain.Identifiers.ProviderMembershipId.New(),
                now,
                ProviderDomain.ValueObjects.ProviderLocationContext.Create(
                    countryCode: "MU",
                    cityName: "Rose Hill",
                    latitude: latitude,
                    longitude: longitude)).Value;

            provider.AddContactMethod(
                ProviderDomain.Providers.ProviderContactMethod.Create(
                    ProviderDomain.Identifiers.ProviderContactMethodId.New(),
                    ProviderDomain.Enums.ContactMethodKind.Phone,
                    "+23057001234",
                    isPrimary: true).Value).IsSuccess.Should().BeTrue();

            provider.Publish(now).IsSuccess.Should().BeTrue();

            dbContext.Set<ProviderDomain.Providers.Provider>().Add(provider);
        }

        await dbContext.SaveChangesAsync();

        return run;
    }

    private async Task<string> SeedRatedItemsAsync()
    {
        Factory.EnsureDatabaseCreated();

        var run = Guid.NewGuid().ToString("N")[..8];
        var now = DateTimeOffset.UtcNow;

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        // Buckets are one to five stars.
        var five = Seller(dbContext, run, "five", SellerReviewSummary.FromDistribution(0, 0, 0, 0, 2), now);
        var fromTwo = Seller(dbContext, run, "two", SellerReviewSummary.FromDistribution(0, 0, 0, 1, 1), now);
        var fromFour = Seller(dbContext, run, "four", SellerReviewSummary.FromDistribution(0, 0, 0, 2, 2), now);
        var unrated = Seller(dbContext, run, "none", SellerReviewSummary.Empty, now);

        dbContext.Set<MarketplaceListing>().AddRange(
            Item(unrated, $"unrated older {run}", now.AddHours(-3)),
            Item(fromTwo, $"four and a half from two {run}", now.AddHours(-2)),
            Item(five, $"five stars {run}", now.AddHours(-5)),
            Item(fromFour, $"four and a half from four {run}", now.AddHours(-4)),
            Item(unrated, $"unrated newer {run}", now.AddHours(-1)));

        await dbContext.SaveChangesAsync();

        return run;
    }

    private static SellerProfileId Seller(
        DiasporaDbContext dbContext, string run, string label, SellerReviewSummary summary, DateTimeOffset now)
    {
        var id = SellerProfileId.New();

        var seller = SellerProfile.Create(
            id,
            UserId.New(),
            SellerIdentity.Create($"Rated Store {label}", null, null, SellerKind.Individual).Value,
            ListingSlug.Create($"rated-store-{label}-{run}").Value,
            "MU",
            now).Value;

        seller.ApplyReviewSummary(summary);
        seller.ClearDomainEvents();
        dbContext.Set<SellerProfile>().Add(seller);

        return id;
    }

    private static MarketplaceListing Item(SellerProfileId sellerId, string title, DateTimeOffset publishedAt)
    {
        var listing = MarketplaceListing.Create(
            MarketplaceListingId.New(),
            sellerId,
            UserId.New(),
            ListingTitle.Create(title).Value,
            ListingSlug.Create($"rated-{Guid.NewGuid():N}").Value,
            ListingDescription.Create("An item seeded for the highest-rated sort test.").Value,
            "electronics",
            ListingType.FixedPrice,
            ProductCondition.Good,
            ListingPrice.Create(1_000m, "MUR", isNegotiable: false, minimumOffer: null).Value,
            ListingLocationContext.Create("MU", null, "Rose Hill", null, null, null).Value,
            publishedAt).Value;

        listing.AttachMedia(Guid.NewGuid(), ListingMediaRole.Cover);
        listing.Publish(publishedAt).IsSuccess.Should().BeTrue();

        return listing;
    }

    private sealed record HomePageShape
    {
        public List<TitleShape> Items { get; init; } = [];

        public string? NextCursor { get; init; }

        public bool HasMore { get; init; }

        public string AppliedSort { get; init; } = string.Empty;
    }

    private sealed record ItemPageShape
    {
        public List<TitleShape> Items { get; init; } = [];

        public string? NextCursor { get; init; }

        public bool HasMore { get; init; }
    }

    private sealed record TitleShape
    {
        public string Title { get; init; } = string.Empty;
    }

    private sealed record ProviderPageShape
    {
        public List<NameShape> Items { get; init; } = [];

        public string? NextCursor { get; init; }

        public bool HasMore { get; init; }

        public string? AppliedSort { get; init; }
    }

    private sealed record NameShape
    {
        public string Name { get; init; } = string.Empty;
    }
}
