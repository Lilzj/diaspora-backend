using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Modules.Housing.Domain.ValueObjects;
using Diaspora.Modules.Location.Domain.Areas;
using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// Cost of living from Diaspora's own listings — 17 Sep 2026 (FRONTEND-MAPS-GAP-014).
/// <para>
/// The user decided the index comes from what members list: the median monthly asking rent of the
/// homes filed in a neighbourhood against its country's median, published only above a minimum
/// number of homes, housing only. Its own class because it seeds homes, and the geographic
/// intelligence suite beside it counts homes into its scores.
/// </para>
/// <para>
/// The seed is built so a wrong rule gives a wrong number rather than the same one: weekly rents
/// that only match if converted, a rent in another currency that moves the median if counted, and
/// a nightly price that is not a rent at all.
/// </para>
/// </summary>
public sealed class CostOfLivingRentIndexTests : IdentityTestBase
{
    private const string Root = "/api/v1/geo";

    public CostOfLivingRentIndexTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task A_neighbourhood_with_enough_homes_gets_a_rent_index_against_its_country_and_nothing_else()
    {
        var peckham = await PathOfAsync("Peckham");
        var fallowfield = await PathOfAsync("Fallowfield");

        // Peckham: five homes, median 1,700 a month.
        await SeedHomesAsync(peckham, "London", "Peckham",
            (1500m, "GBP", RentPeriod.PerMonth), (1600m, "GBP", RentPeriod.PerMonth),
            (1700m, "GBP", RentPeriod.PerMonth), (1800m, "GBP", RentPeriod.PerMonth),
            (1900m, "GBP", RentPeriod.PerMonth));

        // Elsewhere in the country: eight at 800 a month, and nine at 250 a week. The weekly rents
        // decide the country median, so the index is only right if a week is 12/52 of a month:
        // read as monthly the median would be 800 (index 213), at four weeks a month 1,000 (170).
        await SeedHomesAsync(fallowfield, "Manchester", "Fallowfield",
            [
                .. Enumerable.Repeat((800m, "GBP", RentPeriod.PerMonth), 8),
                .. Enumerable.Repeat((250m, "GBP", RentPeriod.PerWeek), 9)
            ]);

        // Not counted: a rent in euros (left out, and said so) and a nightly price (not a rent).
        await SeedHomesAsync(peckham, "London", "Peckham",
            (99_999m, "EUR", RentPeriod.PerMonth), (5m, "GBP", RentPeriod.PerNight));

        await SignInAsync();

        var cost = await IdentityApiClient.ReadDataAsync<CostShape>(
            await HttpClient.GetAsync($"{Root}/neighborhoods/peckham/cost-of-living"));

        cost.IsAvailable.Should().BeTrue();
        cost.OverallIndex.Should().BeNull("housing alone is not the cost of living");
        cost.Currency.Should().Be("GBP");
        cost.BaselineName.Should().Contain("United Kingdom");

        var rent = cost.Categories.Should().ContainSingle().Subject;
        rent.Id.Should().Be("housing");

        // 22 homes in the country: eight at 800, nine at 1,083.33, Peckham's five. Median 1,083.33.
        rent.Index.Should().Be(157, "1,700 against 1,083.33 is 156.9");
        rent.TypicalMonthlyAmount.Should().Be(1700m);
        rent.TypicalMonthlyCurrency.Should().Be("GBP");

        cost.Freshness.SourceName.Should().Be("Diaspora listings");
        cost.Freshness.SampleSize.Should().Be(5);
        cost.Freshness.Methodology.Should().Contain("5 homes").And.Contain("22");
        cost.Freshness.Limitations.Should().Contain(line => line.Contains("left out rather than converted"));
        cost.Freshness.Limitations.Should().Contain(line => line.StartsWith("Housing only"));
    }

    [Fact]
    public async Task A_neighbourhood_with_too_few_homes_gets_no_index_and_is_told_why()
    {
        await SignInAsync();

        var cost = await IdentityApiClient.ReadDataAsync<CostShape>(
            await HttpClient.GetAsync($"{Root}/neighborhoods/brixton/cost-of-living"));

        cost.IsAvailable.Should().BeFalse();
        cost.Categories.Should().BeEmpty();
        cost.Currency.Should().BeNull();
        cost.Freshness.SampleSize.Should().Be(0);
        cost.Freshness.Methodology.Should().Contain("0 homes").And.Contain("at least 5");
    }

    // ── Plumbing ─────────────────────────────────────────────────────────────────────────────────

    private async Task SignInAsync()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);
    }

    private async Task<string> PathOfAsync(string neighbourhood)
    {
        Factory.EnsureDatabaseCreated();

        using var scope = Factory.Services.CreateScope();

        var areas = await scope.ServiceProvider.GetRequiredService<DiasporaDbContext>()
            .Set<AdministrativeArea>().AsNoTracking()
            .Where(area => area.Level == AdministrativeLevel.Neighborhood)
            .ToListAsync();

        return areas.Single(area => area.Name.Value == neighbourhood).Path.Value;
    }

    private async Task SeedHomesAsync(
        string areaPath,
        string city,
        string neighbourhood,
        params (decimal Amount, string Currency, RentPeriod Period)[] rents)
    {
        Factory.EnsureDatabaseCreated();

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var landlordId = LandlordProfileId.New();
        var ownerId = UserId.New();
        var now = DateTimeOffset.UtcNow;

        foreach (var (amount, currency, period) in rents)
        {
            var label = Guid.NewGuid().ToString("N")[..10];
            var nightly = period == RentPeriod.PerNight;

            var listing = HousingListing.Draft(
                HousingListingId.New(),
                landlordId,
                ownerId,
                HousingTitle.Create($"Rent index home {label}").Value,
                HousingSlug.Create($"rent-index-{label}"),
                HousingDescription.Create($"A seeded home for the rent index suite: {label}.").Value,
                "apartment",
                HousingPrice.Create(amount, currency, period).Value,
                HousingCapacity.Create(1, 1m, maximumOccupants: 2).Value,
                HousingAvailability.Create(null, totalUnits: 1).Value,
                HousingLocationContext.Create(
                    "GB", null, city, neighbourhood, Guid.NewGuid(), areaPath).Value,
                PropertyAddress.Undisclosed,
                StayPolicy.Create(acceptsShortStay: nightly, acceptsLongStay: !nightly).Value,
                now).Value;

            listing.AttachMedia(Guid.NewGuid(), HousingMediaRole.Cover, null, now);
            listing.Publish(now).IsSuccess.Should().BeTrue("a home that failed to publish is not in any sample");

            dbContext.Set<HousingListing>().Add(listing);
        }

        await dbContext.SaveChangesAsync();
    }

    private sealed record CostShape
    {
        public bool IsAvailable { get; init; }

        public int? OverallIndex { get; init; }

        public string? BaselineName { get; init; }

        public string? Currency { get; init; }

        public List<CategoryShape> Categories { get; init; } = [];

        public FreshnessShape Freshness { get; init; } = new();
    }

    private sealed record CategoryShape
    {
        public string Id { get; init; } = "";

        public int Index { get; init; }

        public decimal? TypicalMonthlyAmount { get; init; }

        public string? TypicalMonthlyCurrency { get; init; }
    }

    private sealed record FreshnessShape
    {
        public string SourceName { get; init; } = "";

        public string? Methodology { get; init; }

        public List<string> Limitations { get; init; } = [];

        public int? SampleSize { get; init; }
    }
}
