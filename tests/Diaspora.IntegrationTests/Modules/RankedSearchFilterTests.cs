using System.Net.Http.Json;
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

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// "At least this furnished" and "at least this condition", against real rows — 15 Sep 2026.
/// <para>
/// <b>Both filters compared words, not ranks.</b> The two enums are stored by name, so
/// <c>listing.FurnishedStatus &gt;= floor</c> became a text comparison in SQL. On the live platform
/// "at least part-furnished" returned neither of the two furnished homes, and "at least Excellent"
/// returned a phone in Good condition. The same comparison let an unfurnished home through a
/// furnished-only search, because U follows F. SQLite compares text the same way Postgres does, so
/// these tests fail on the old predicate.
/// </para>
/// <para>
/// The rent-range test pins the contract the app's filter sheet relies on: a range narrows only
/// when it names its currency and its period.
/// </para>
/// </summary>
public sealed class RankedSearchFilterTests : IdentityTestBase
{
    public RankedSearchFilterTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ── Homes ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_furnished_search_returns_the_furnished_homes_and_nothing_less_furnished()
    {
        var run = await SeedHomesAsync(
            ("unfurnished", FurnishedStatus.Unfurnished, 15_000m, RentPeriod.PerMonth),
            ("part-furnished", FurnishedStatus.PartlyFurnished, 15_000m, RentPeriod.PerMonth),
            ("furnished", FurnishedStatus.Furnished, 15_000m, RentPeriod.PerMonth),
            ("fully-equipped", FurnishedStatus.FullyEquipped, 15_000m, RentPeriod.PerMonth));

        var titles = await SearchHomesAsync($"query={run}&countryCode=MU&minFurnishedStatus=Furnished");

        titles.Should().BeEquivalentTo([$"furnished {run}", $"fully-equipped {run}"]);
    }

    [Fact]
    public async Task A_part_furnished_search_keeps_the_furnished_homes()
    {
        var run = await SeedHomesAsync(
            ("unfurnished", FurnishedStatus.Unfurnished, 15_000m, RentPeriod.PerMonth),
            ("part-furnished", FurnishedStatus.PartlyFurnished, 15_000m, RentPeriod.PerMonth),
            ("furnished", FurnishedStatus.Furnished, 15_000m, RentPeriod.PerMonth),
            ("fully-equipped", FurnishedStatus.FullyEquipped, 15_000m, RentPeriod.PerMonth));

        var titles = await SearchHomesAsync($"query={run}&countryCode=MU&minFurnishedStatus=PartlyFurnished");

        titles.Should().BeEquivalentTo(
            [$"part-furnished {run}", $"furnished {run}", $"fully-equipped {run}"]);
    }

    [Fact]
    public async Task Exact_furnishing_returns_only_the_homes_furnished_that_way()
    {
        var run = await SeedHomesAsync(
            ("unfurnished", FurnishedStatus.Unfurnished, 15_000m, RentPeriod.PerMonth),
            ("part-furnished", FurnishedStatus.PartlyFurnished, 15_000m, RentPeriod.PerMonth),
            ("furnished", FurnishedStatus.Furnished, 15_000m, RentPeriod.PerMonth),
            ("fully-equipped", FurnishedStatus.FullyEquipped, 15_000m, RentPeriod.PerMonth));

        // A floor cannot say "unfurnished". The app filtered one page for it on the phone, so a later
        // page's unfurnished homes never appeared.
        (await SearchHomesAsync($"query={run}&countryCode=MU&furnishedStatuses=Unfurnished"))
            .Should().BeEquivalentTo([$"unfurnished {run}"]);

        (await SearchHomesAsync($"query={run}&countryCode=MU&furnishedStatuses=PartlyFurnished"))
            .Should().BeEquivalentTo([$"part-furnished {run}"]);

        (await SearchHomesAsync(
                $"query={run}&countryCode=MU&furnishedStatuses=Furnished&furnishedStatuses=FullyEquipped"))
            .Should().BeEquivalentTo([$"furnished {run}", $"fully-equipped {run}"]);
    }

    [Fact]
    public async Task A_rent_range_that_names_its_currency_and_period_keeps_only_the_rents_inside_it()
    {
        var run = await SeedHomesAsync(
            ("room", FurnishedStatus.Furnished, 9_500m, RentPeriod.PerMonth),
            ("flat", FurnishedStatus.Furnished, 22_000m, RentPeriod.PerMonth),
            ("weekly", FurnishedStatus.Furnished, 9_000m, RentPeriod.PerWeek));

        var titles = await SearchHomesAsync(
            $"query={run}&countryCode=MU&minRent=5000&maxRent=10000&currency=MUR&rentPeriod=PerMonth");

        titles.Should().BeEquivalentTo([$"room {run}"], "a weekly rent is not a monthly one");
    }

    // ── Items ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task An_excellent_or_better_search_returns_no_item_in_worse_condition()
    {
        var run = await SeedItemsAsync(
            ProductCondition.Fair,
            ProductCondition.Good,
            ProductCondition.Excellent,
            ProductCondition.LikeNew,
            ProductCondition.New);

        var titles = await SearchItemsAsync(new { text = run, minimumCondition = "Excellent", limit = 50 });

        titles.Should().BeEquivalentTo([$"Excellent {run}", $"LikeNew {run}", $"New {run}"]);
    }

    [Fact]
    public async Task A_good_or_better_search_leaves_out_an_item_the_question_does_not_apply_to()
    {
        var run = await SeedItemsAsync(
            ProductCondition.NotApplicable,
            ProductCondition.ForParts,
            ProductCondition.Fair,
            ProductCondition.Good,
            ProductCondition.New);

        var titles = await SearchItemsAsync(new { text = run, minimumCondition = "Good", limit = 50 });

        titles.Should().BeEquivalentTo([$"Good {run}", $"New {run}"]);
    }

    [Fact]
    public async Task Pickup_only_returns_the_items_offered_neither_delivered_nor_shipped()
    {
        var run = await SeedFulfilmentAsync(
            ("collect", FulfillmentOptions.Create(offersPickup: true, offersDelivery: false, offersShipping: false).Value),
            ("delivered", FulfillmentOptions.Create(offersPickup: true, offersDelivery: true, offersShipping: false).Value),
            ("posted", FulfillmentOptions.Create(offersPickup: false, offersDelivery: false, offersShipping: true).Value));

        var titles = await SearchItemsAsync(new { text = run, pickupOnly = true, limit = 50 });

        titles.Should().BeEquivalentTo([$"collect {run}"]);
    }

    // ── Seeding ─────────────────────────────────────────────────────────────────────────────

    private async Task<string> SeedFulfilmentAsync(params (string Label, FulfillmentOptions Fulfillment)[] items)
    {
        Factory.EnsureDatabaseCreated();

        var run = Guid.NewGuid().ToString("N")[..8];
        var sellerId = SellerProfileId.New();
        var ownerId = UserId.New();
        var now = DateTimeOffset.UtcNow;

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        foreach (var (label, fulfillment) in items)
        {
            var listing = MarketplaceListing.Create(
                MarketplaceListingId.New(),
                sellerId,
                ownerId,
                ListingTitle.Create($"{label} {run}").Value,
                ListingSlug.Create($"fulfilment-{run}-{label}").Value,
                ListingDescription.Create("An item seeded for the pickup filter test.").Value,
                "electronics",
                ListingType.FixedPrice,
                ProductCondition.Good,
                ListingPrice.Create(1_000m, "MUR", isNegotiable: false, minimumOffer: null).Value,
                ListingLocationContext.Create("MU", null, "Rose Hill", null, null, null).Value,
                now).Value;

            listing.UpdateDetails(
                    listing.Title,
                    listing.Description,
                    listing.Condition,
                    listing.Price,
                    listing.Location,
                    fulfillment,
                    listing.Product,
                    listing.ContactPreference,
                    listing.Visibility,
                    baseRevision: null)
                .IsSuccess.Should().BeTrue();

            listing.AttachMedia(Guid.NewGuid(), ListingMediaRole.Cover);
            listing.Publish(now).IsSuccess.Should().BeTrue();

            dbContext.Set<MarketplaceListing>().Add(listing);
        }

        await dbContext.SaveChangesAsync();

        return run;
    }

    private async Task<string> SeedHomesAsync(
        params (string Label, FurnishedStatus Furnished, decimal Rent, RentPeriod Period)[] homes)
    {
        Factory.EnsureDatabaseCreated();

        var run = Guid.NewGuid().ToString("N")[..8];
        var landlordId = LandlordProfileId.New();
        var now = DateTimeOffset.UtcNow;

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        foreach (var (label, furnished, rent, period) in homes)
        {
            var title = $"{label} {run}";

            var listing = HousingListing.Draft(
                HousingListingId.New(),
                landlordId,
                UserId.New(),
                HousingTitle.Create(title).Value,
                HousingSlug.Create($"ranked-{run}-{label}"),
                HousingDescription.Create("A home seeded for the ranked filter tests.").Value,
                "apartment",
                HousingPrice.Create(rent, "MUR", period, null).Value,
                HousingCapacity.Create(2, 1m, maximumOccupants: 3).Value,
                HousingAvailability.Create(totalUnits: 1).Value,
                HousingLocationContext.Create("MU", cityName: "Rose Hill").Value,
                PropertyAddress.Undisclosed,
                StayPolicy.Create(acceptsShortStay: false, acceptsLongStay: true, isSharedAccommodation: false).Value,
                now).Value;

            listing.UpdateContent(baseRevision: null, changedBy: null, furnishedStatus: furnished)
                .IsSuccess.Should().BeTrue();
            listing.FurnishedStatus.Should().Be(furnished);

            listing.AttachMedia(Guid.NewGuid(), HousingMediaRole.Cover, null, now).IsSuccess.Should().BeTrue();

            // Asserted: a seed that failed to publish would pass every "not returned" assertion.
            listing.Publish(now).IsSuccess.Should().BeTrue();

            dbContext.Set<HousingListing>().Add(listing);
        }

        await dbContext.SaveChangesAsync();

        return run;
    }

    private async Task<string> SeedItemsAsync(params ProductCondition[] conditions)
    {
        Factory.EnsureDatabaseCreated();

        var run = Guid.NewGuid().ToString("N")[..8];
        var sellerId = SellerProfileId.New();
        var ownerId = UserId.New();
        var now = DateTimeOffset.UtcNow;

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        foreach (var condition in conditions)
        {
            var listing = MarketplaceListing.Create(
                MarketplaceListingId.New(),
                sellerId,
                ownerId,
                ListingTitle.Create($"{condition} {run}").Value,
                ListingSlug.Create($"ranked-{run}-{condition.ToString().ToLowerInvariant()}").Value,
                ListingDescription.Create("An item seeded for the ranked filter tests.").Value,
                "electronics",
                ListingType.FixedPrice,
                condition,
                ListingPrice.Create(1_000m, "MUR", isNegotiable: false, minimumOffer: null).Value,
                ListingLocationContext.Create("MU", null, "Rose Hill", null, null, null).Value,
                now).Value;

            listing.AttachMedia(Guid.NewGuid(), ListingMediaRole.Cover);
            listing.Publish(now).IsSuccess.Should().BeTrue();

            dbContext.Set<MarketplaceListing>().Add(listing);
        }

        await dbContext.SaveChangesAsync();

        return run;
    }

    // ── Plumbing ────────────────────────────────────────────────────────────────────────────

    private async Task<List<string>> SearchHomesAsync(string query)
    {
        Api.ClearAuthentication();

        var page = await IdentityApiClient.ReadDataAsync<PageShape>(
            await HttpClient.GetAsync($"/api/v1/housing/search?limit=100&{query}"));

        return page.Items.Select(item => item.Title).ToList();
    }

    private async Task<List<string>> SearchItemsAsync(object body)
    {
        Api.ClearAuthentication();

        var page = await IdentityApiClient.ReadDataAsync<PageShape>(
            await HttpClient.PostAsJsonAsync("/api/v1/marketplace/search", body));

        return page.Items.Select(item => item.Title).ToList();
    }

    // Declared by hand, not bound to the modules' records, so a contract change fails here.
    private sealed record PageShape
    {
        public List<CardShape> Items { get; init; } = [];
    }

    private sealed record CardShape
    {
        public string Title { get; init; } = string.Empty;
    }
}
