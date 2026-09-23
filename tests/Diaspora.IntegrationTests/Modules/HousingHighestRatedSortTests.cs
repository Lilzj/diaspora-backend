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
/// "Rating" on the Housing tab, ordered by the server across every page — 15 Sep 2026.
/// <para>
/// <b>The app sorted one page of twenty homes by their landlord's rating, on the phone.</b> A
/// better-rated home on page two could never rise above page one, and paging would have appended
/// server order after a phone-sorted page. The order is now <c>sort=HighestRated</c>: the exact
/// average, then how many reviews, then newest — unrated landlords last, since no reviews is not a
/// bad review.
/// </para>
/// </summary>
[Collection(HousingCollection.Name)]
public sealed class HousingHighestRatedSortTests : PooledIdentityTestBase
{
    private const string Root = "/api/v1/housing";

    public HousingHighestRatedSortTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Highest_rated_pages_through_rated_landlords_first_and_the_unrated_newest_first()
    {
        var run = await SeedAsync();
        Api.ClearAuthentication();

        var titles = new List<string>();
        string? cursor = null;
        var pages = 0;

        do
        {
            var query = $"{Root}/search?query={run}&countryCode=MU&sort=HighestRated&limit=2"
                + (cursor is null ? string.Empty : $"&cursor={Uri.EscapeDataString(cursor)}");

            var page = await IdentityApiClient.ReadDataAsync<PageShape>(await HttpClient.GetAsync(query));

            titles.AddRange(page.Items.Select(item => item.Title));
            cursor = page.HasMore ? page.NextCursor : null;
            pages++;
        }
        while (cursor is not null && pages < 10);

        titles.Should().Equal(
            [
                $"five stars {run}",
                $"four and a half from four {run}",
                $"four and a half from two {run}",
                $"unrated newer {run}",
                $"unrated older {run}"
            ],
            "the order must hold across page boundaries, not only inside one page");

        pages.Should().Be(3);
    }

    [Fact]
    public async Task A_rated_landlords_card_carries_the_rating_and_an_unrated_ones_carries_none()
    {
        var run = await SeedAsync();
        Api.ClearAuthentication();

        var page = await IdentityApiClient.ReadDataAsync<PageShape>(
            await HttpClient.GetAsync($"{Root}/search?query={run}&countryCode=MU&limit=50"));

        var rated = page.Items.Single(item => item.Title == $"four and a half from four {run}");
        rated.Landlord!.Rating.Should().NotBeNull();
        rated.Landlord.Rating!.Average.Should().Be(4.5d);
        rated.Landlord.Rating.TotalReviews.Should().Be(4);

        page.Items.Single(item => item.Title == $"unrated newer {run}").Landlord!.Rating.Should().BeNull();
    }

    private async Task<string> SeedAsync()
    {
        Factory.EnsureDatabaseCreated();

        var run = Guid.NewGuid().ToString("N")[..8];
        var now = DateTimeOffset.UtcNow;

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        // Buckets are one to five stars.
        var fiveStars = Landlord(dbContext, run, "five", LandlordReviewSummary.FromDistribution(0, 0, 0, 0, 2), now);
        var fourFromTwo = Landlord(dbContext, run, "two", LandlordReviewSummary.FromDistribution(0, 0, 0, 1, 1), now);
        var fourFromFour = Landlord(dbContext, run, "four", LandlordReviewSummary.FromDistribution(0, 0, 0, 2, 2), now);
        var unrated = Landlord(dbContext, run, "none", LandlordReviewSummary.Empty, now);

        // Published oldest first where it matters: among the unrated, newest must lead.
        dbContext.Set<HousingListing>().AddRange(
            Home(unrated, $"unrated older {run}", now.AddHours(-3)),
            Home(fourFromTwo, $"four and a half from two {run}", now.AddHours(-2)),
            Home(fiveStars, $"five stars {run}", now.AddHours(-5)),
            Home(fourFromFour, $"four and a half from four {run}", now.AddHours(-4)),
            Home(unrated, $"unrated newer {run}", now.AddHours(-1)));

        await dbContext.SaveChangesAsync();

        return run;
    }

    private static LandlordProfileId Landlord(
        DiasporaDbContext dbContext, string run, string label, LandlordReviewSummary summary, DateTimeOffset now)
    {
        var id = LandlordProfileId.New();

        var landlord = LandlordProfile.Register(
            id,
            UserId.New(),
            LandlordIdentity.Create($"Rated Lettings {label}", LandlordKind.Individual, null).Value,
            HousingSlug.Create($"rated-lettings-{label}-{run}"),
            "MU",
            LandlordContact.None,
            now).Value;

        landlord.ApplyReviewSummary(summary);
        dbContext.Set<LandlordProfile>().Add(landlord);

        return id;
    }

    private static HousingListing Home(LandlordProfileId landlordId, string title, DateTimeOffset publishedAt)
    {
        var listing = HousingListing.Draft(
            HousingListingId.New(),
            landlordId,
            UserId.New(),
            HousingTitle.Create(title).Value,
            HousingSlug.Create(title),
            HousingDescription.Create("A home seeded for the highest-rated sort tests.").Value,
            "apartment",
            HousingPrice.Create(15_000m, "MUR", RentPeriod.PerMonth, null).Value,
            HousingCapacity.Create(2, 1m, maximumOccupants: 3).Value,
            HousingAvailability.Create(totalUnits: 1).Value,
            HousingLocationContext.Create("MU", cityName: "Rose Hill").Value,
            PropertyAddress.Undisclosed,
            StayPolicy.Create(acceptsShortStay: false, acceptsLongStay: true, isSharedAccommodation: false).Value,
            publishedAt).Value;

        listing.AttachMedia(Guid.NewGuid(), HousingMediaRole.Cover, null, publishedAt).IsSuccess.Should().BeTrue();
        listing.Publish(publishedAt).IsSuccess.Should().BeTrue();
        listing.PublishedAt.Should().Be(publishedAt);

        return listing;
    }

    private sealed record PageShape
    {
        public List<CardShape> Items { get; init; } = [];

        public string? NextCursor { get; init; }

        public bool HasMore { get; init; }
    }

    private sealed record CardShape
    {
        public string Title { get; init; } = string.Empty;

        public LandlordShape? Landlord { get; init; }
    }

    private sealed record LandlordShape
    {
        public RatingShape? Rating { get; init; }
    }

    private sealed record RatingShape
    {
        public double Average { get; init; }

        public int TotalReviews { get; init; }
    }
}
