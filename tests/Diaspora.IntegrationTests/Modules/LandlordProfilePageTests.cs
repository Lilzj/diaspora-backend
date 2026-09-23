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
/// The two reads a landlord's profile page makes — 16 Sep 2026.
/// <para>
/// <b>The page listed nothing and named nobody.</b> It read <c>host</c> and <c>listings</c> from
/// <c>GET /housing/landlords/{idOrSlug}</c>, which sends neither: the route answers one landlord
/// profile, and there was no way at all to ask for a landlord's homes. So a member who tapped
/// "View profile" on a home got an empty screen.
/// </para>
/// <para>
/// The homes now come from the ordinary search, narrowed by <c>landlordId</c>. Narrowing only:
/// every other visibility rule still applies, which these tests hold it to by seeding a draft and
/// a second landlord's home and expecting neither.
/// </para>
/// </summary>
[Collection(HousingCollection.Name)]
public sealed class LandlordProfilePageTests : PooledIdentityTestBase
{
    private const string Root = "/api/v1/housing";

    public LandlordProfilePageTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task A_landlords_profile_names_them_and_counts_their_homes()
    {
        var seeded = await SeedAsync();
        Api.ClearAuthentication();

        var profile = await IdentityApiClient.ReadDataAsync<ProfileShape>(
            await HttpClient.GetAsync($"{Root}/landlords/{seeded.Landlord.Value}"));

        profile.Id.Should().Be(seeded.Landlord.Value);
        profile.DisplayName.Should().Be($"Profile Lettings {seeded.Run}");
        profile.Kind.Should().Be(nameof(LandlordKind.Individual));
        profile.Trust.Should().NotBeNull();
        profile.IsOwner.Should().BeFalse("a stranger is reading it");

        // **Two, not three: the draft is not one of their advertisements.** This assertion read
        // three until 20 Sep 2026, and the comment that stood here was an accurate account of a
        // defect: `ActiveListings` was filled from the creation quota's query, which counts drafts,
        // occupied and unavailable homes on purpose — a quota exists to stop a script making
        // drafts — and it was recomputed only when a home was published, so it never came down.
        // The page worked around it by counting the homes it lists instead of printing this.
        //
        // Both halves are fixed, so the figure now means what its name says. Note what this read
        // proves about the old one: the route is anonymous, so a stranger looking at a landlord's
        // public profile was being told how many unpublished drafts that landlord was holding.
        profile.Statistics!.ActiveListings.Should().Be(
            2, "the draft is theirs but it is not on the market");
    }

    [Fact]
    public async Task A_landlords_homes_are_the_published_ones_and_nobody_elses()
    {
        var seeded = await SeedAsync();
        Api.ClearAuthentication();

        var page = await IdentityApiClient.ReadDataAsync<PageShape>(
            await HttpClient.GetAsync($"{Root}/search?landlordId={seeded.Landlord.Value}&limit=50"));

        page.Items.Select(item => item.Title).Should().BeEquivalentTo(
            [$"theirs newer {seeded.Run}", $"theirs older {seeded.Run}"]);
    }

    [Fact]
    public async Task No_landlord_asked_for_is_every_landlords_homes()
    {
        var seeded = await SeedAsync();
        Api.ClearAuthentication();

        var page = await IdentityApiClient.ReadDataAsync<PageShape>(
            await HttpClient.GetAsync($"{Root}/search?query={seeded.Run}&limit=50"));

        page.Items.Should().HaveCount(3, "an empty landlord filter narrows nothing");

        // The client sends an empty guid when a landlord profile has no identifier to send; it
        // must not be read as "the landlord whose id is all zeroes", which is nobody.
        var empty = await IdentityApiClient.ReadDataAsync<PageShape>(
            await HttpClient.GetAsync($"{Root}/search?query={seeded.Run}&landlordId={Guid.Empty}&limit=50"));

        empty.Items.Should().HaveCount(3);
    }

    private async Task<(string Run, LandlordProfileId Landlord)> SeedAsync()
    {
        Factory.EnsureDatabaseCreated();

        var run = Guid.NewGuid().ToString("N")[..8];
        var now = DateTimeOffset.UtcNow;

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var theirs = Landlord(dbContext, $"Profile Lettings {run}", $"profile-lettings-{run}", now);
        var somebodyElse = Landlord(dbContext, $"Other Lettings {run}", $"other-lettings-{run}", now);

        dbContext.Set<HousingListing>().AddRange(
            Home(theirs, $"theirs older {run}", now.AddHours(-3), publish: true),
            Home(theirs, $"theirs newer {run}", now.AddHours(-1), publish: true),
            Home(theirs, $"theirs draft {run}", now.AddHours(-2), publish: false),
            Home(somebodyElse, $"someone elses {run}", now.AddHours(-4), publish: true));

        await dbContext.SaveChangesAsync();

        return (run, theirs);
    }

    private static LandlordProfileId Landlord(
        DiasporaDbContext dbContext, string name, string slug, DateTimeOffset now)
    {
        var id = LandlordProfileId.New();

        dbContext.Set<LandlordProfile>().Add(LandlordProfile.Register(
            id,
            UserId.New(),
            LandlordIdentity.Create(name, LandlordKind.Individual, null).Value,
            HousingSlug.Create(slug),
            "MU",
            LandlordContact.None,
            now).Value);

        return id;
    }

    private static HousingListing Home(
        LandlordProfileId landlordId, string title, DateTimeOffset publishedAt, bool publish)
    {
        var listing = HousingListing.Draft(
            HousingListingId.New(),
            landlordId,
            UserId.New(),
            HousingTitle.Create(title).Value,
            HousingSlug.Create(title),
            HousingDescription.Create("A home seeded for the landlord profile page tests.").Value,
            "apartment",
            HousingPrice.Create(15_000m, "MUR", RentPeriod.PerMonth, null).Value,
            HousingCapacity.Create(2, 1m, maximumOccupants: 3).Value,
            HousingAvailability.Create(totalUnits: 1).Value,
            HousingLocationContext.Create("MU", cityName: "Rose Hill").Value,
            PropertyAddress.Undisclosed,
            StayPolicy.Create(acceptsShortStay: false, acceptsLongStay: true, isSharedAccommodation: false).Value,
            publishedAt).Value;

        if (publish)
        {
            listing.AttachMedia(Guid.NewGuid(), HousingMediaRole.Cover, null, publishedAt).IsSuccess.Should().BeTrue();
            listing.Publish(publishedAt).IsSuccess.Should().BeTrue();
        }

        return listing;
    }

    private sealed record ProfileShape
    {
        public Guid Id { get; init; }

        public string DisplayName { get; init; } = string.Empty;

        public string Kind { get; init; } = string.Empty;

        public bool IsOwner { get; init; }

        public TrustShape? Trust { get; init; }

        public StatisticsShape? Statistics { get; init; }
    }

    private sealed record TrustShape
    {
        public bool IsVerified { get; init; }
    }

    private sealed record StatisticsShape
    {
        public int ActiveListings { get; init; }

        public int TotalListings { get; init; }
    }

    private sealed record PageShape
    {
        public List<CardShape> Items { get; init; } = [];
    }

    private sealed record CardShape
    {
        public string Title { get; init; } = string.Empty;
    }
}
