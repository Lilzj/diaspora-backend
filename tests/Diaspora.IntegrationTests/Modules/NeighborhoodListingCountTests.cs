using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Modules.Housing.Domain.Neighborhoods;
using Diaspora.Modules.Housing.Domain.ValueObjects;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// What a neighbourhood says it has, against what a member can actually find there — 21 Sep 2026.
/// <para>
/// <b>The count fell when a property was let and rose when one was published, and never moved
/// otherwise.</b> So a neighbourhood kept advertising homes that had been archived, taken off the
/// market, hidden by a moderator, made private or deleted. The occupied handler stated the reason
/// the count has to be fresh — a directory that still advertised a let property would send members
/// to a home somebody else is living in — and that argument was never applied to the other five
/// ways a home leaves the market.
/// </para>
/// <para>
/// <b>Three conditions, not one.</b> A property is counted when it is public, clear of moderation
/// and Published, Available or Reserved. A lifecycle move, a moderation decision and a landlord
/// making the listing private each end that, and each arrives on a different event — so a fix that
/// followed only the status would still be wrong on two paths out of three. There is a test here
/// for each.
/// </para>
/// <para>
/// <b>Driven through the aggregate and a real save</b>, because the counts are written by domain
/// event handlers this platform dispatches inside <c>SaveChangesAsync</c>. Calling the refresher
/// directly would prove the arithmetic and nothing about whether any of these moves reaches it.
/// </para>
/// </summary>
public sealed class NeighborhoodListingCountTests : IdentityTestBase
{
    public NeighborhoodListingCountTests(CustomWebApplicationFactory factory) : base(factory) =>
        factory.EnsureDatabaseCreated();

    [Fact]
    public async Task Publishing_a_home_adds_it_to_its_neighbourhood()
    {
        var area = await SeedNeighborhoodAsync();

        await SeedHomeAsync(area, publish: true);

        (await CountOf(area)).Should().Be(1);
    }

    /// <summary>
    /// <b>The defect.</b> Nothing recounted on withdrawal, so the directory went on offering it.
    /// </summary>
    [Fact]
    public async Task Archiving_a_home_takes_it_out_of_the_neighbourhood()
    {
        var area = await SeedNeighborhoodAsync();
        var home = await SeedHomeAsync(area, publish: true);

        await OnTheMarket(area);

        await MoveAsync(home, listing =>
            listing.Archive(DateTimeOffset.UtcNow, UserId.New(), reason: null));

        (await CountOf(area)).Should().Be(0);
    }

    [Fact]
    public async Task Taking_a_home_off_the_market_takes_it_out_of_the_neighbourhood()
    {
        var area = await SeedNeighborhoodAsync();
        var home = await SeedHomeAsync(area, publish: true);

        await OnTheMarket(area);

        await MoveAsync(home, listing =>
            listing.MakeUnavailable(DateTimeOffset.UtcNow, UserId.New(), reason: null));

        (await CountOf(area)).Should().Be(0);
    }

    /// <summary>
    /// <b>The case that shaped the fix.</b> A soft-deleted property is invisible to every ordinary
    /// read, so the handler cannot load it to ask which neighbourhood to recount — which is why
    /// the lookup reads past the delete filter, and why this test exists to hold it there.
    /// </summary>
    [Fact]
    public async Task Deleting_a_home_takes_it_out_of_the_neighbourhood()
    {
        var area = await SeedNeighborhoodAsync();
        var home = await SeedHomeAsync(area, publish: true);

        await OnTheMarket(area);

        await MoveAsync(home, listing =>
        {
            listing.MarkDeleted(DateTimeOffset.UtcNow, userId: null);
            return Diaspora.SharedKernel.Results.Result.Success();
        });

        (await CountOf(area)).Should().Be(0);
    }

    /// <summary>
    /// Moderation is one of the three conditions, and moves no status — so the lifecycle handler
    /// never hears it, and the directory advertised a home nobody could open.
    /// </summary>
    [Fact]
    public async Task Hiding_a_home_by_moderation_takes_it_out_of_the_neighbourhood()
    {
        var area = await SeedNeighborhoodAsync();
        var home = await SeedHomeAsync(area, publish: true);

        await OnTheMarket(area);

        await MoveAsync(home, listing =>
        {
            listing.Hide("under review", UserId.New(), DateTimeOffset.UtcNow);
            return Diaspora.SharedKernel.Results.Result.Success();
        });

        (await CountOf(area)).Should().Be(0);
    }

    /// <summary>
    /// The quietest of the three: an ordinary edit, with no status move and no moderator.
    /// </summary>
    [Fact]
    public async Task Making_a_home_private_takes_it_out_of_the_neighbourhood()
    {
        var area = await SeedNeighborhoodAsync();
        var home = await SeedHomeAsync(area, publish: true);

        await OnTheMarket(area);

        await MoveAsync(home, listing =>
            listing.SetVisibility(HousingVisibility.Private, baseRevision: null));

        (await CountOf(area)).Should().Be(0);
    }

    /// <summary>
    /// And it comes back. A count that only ever fell would be the same defect facing the other
    /// way — a neighbourhood under-reporting what it has.
    /// </summary>
    [Fact]
    public async Task A_home_that_comes_back_is_counted_again()
    {
        var area = await SeedNeighborhoodAsync();
        var home = await SeedHomeAsync(area, publish: true);

        await OnTheMarket(area);

        await MoveAsync(home, listing =>
            listing.Archive(DateTimeOffset.UtcNow, UserId.New(), reason: null));

        (await CountOf(area)).Should().Be(0, "it was withdrawn");

        await MoveAsync(home, listing =>
            listing.RestoreFromArchive(DateTimeOffset.UtcNow, UserId.New()));

        (await CountOf(area)).Should().Be(
            0, "a restore lands in draft, which nobody but its landlord can see");

        await MoveAsync(home, listing => listing.Publish(DateTimeOffset.UtcNow));

        (await CountOf(area)).Should().Be(1, "and published again it is on the market again");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>The assertion that stops the rest being vacuous.</b> Every test below withdraws a home
    /// and expects nought — which a neighbourhood that never counted it in the first place gives
    /// just as readily. Disabling the refresher entirely left five of these seven green until this
    /// was added: they were proving that nothing had happened, twice.
    /// </summary>
    private async Task OnTheMarket(Guid areaId) =>
        (await CountOf(areaId)).Should().Be(
            1, "the home has to be counted before its removal can mean anything");

    private async Task<Guid> SeedNeighborhoodAsync()
    {
        var areaId = Guid.NewGuid();
        var run = Guid.NewGuid().ToString("N")[..8];

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        dbContext.Set<NeighborhoodProfile>().Add(NeighborhoodProfile.Create(
            NeighborhoodProfileId.New(),
            areaId,
            "MU",
            $"Counted Quarter {run}",
            $"counted-quarter-{run}",
            DateTimeOffset.UtcNow).Value);

        await dbContext.SaveChangesAsync();

        return areaId;
    }

    private async Task<HousingListingId> SeedHomeAsync(Guid areaId, bool publish)
    {
        var run = Guid.NewGuid().ToString("N")[..8];
        var now = DateTimeOffset.UtcNow;

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var listing = HousingListing.Draft(
            HousingListingId.New(),
            LandlordProfileId.New(),
            UserId.New(),
            HousingTitle.Create($"Counted home {run}").Value,
            HousingSlug.Create($"counted-home-{run}"),
            HousingDescription.Create("A home seeded to be counted, and then not.").Value,
            "apartment",
            HousingPrice.Create(15_000m, "MUR", RentPeriod.PerMonth, null).Value,
            HousingCapacity.Create(2, 1m, maximumOccupants: 3).Value,
            HousingAvailability.Create(null, totalUnits: 1).Value,
            HousingLocationContext.Create("MU", cityName: "Rose Hill", neighborhoodAreaId: areaId)
                .Value,
            PropertyAddress.Undisclosed,
            StayPolicy.Create(
                acceptsShortStay: false, acceptsLongStay: true, isSharedAccommodation: false).Value,
            now).Value;

        if (publish)
        {
            listing.AttachMedia(Guid.NewGuid(), HousingMediaRole.Cover, null, now);
            listing.Publish(now).IsSuccess.Should().BeTrue("the seed has to reach the market");
        }

        dbContext.Set<HousingListing>().Add(listing);
        await dbContext.SaveChangesAsync();

        return listing.Id;
    }

    private async Task MoveAsync(
        HousingListingId listingId,
        Func<HousingListing, Diaspora.SharedKernel.Results.Result> move)
    {
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var listing = await dbContext.Set<HousingListing>().FindAsync(listingId);

        listing.Should().NotBeNull();

        move(listing!).IsSuccess.Should().BeTrue("the move itself has to succeed");

        await dbContext.SaveChangesAsync();
    }

    private async Task<int> CountOf(Guid areaId)
    {
        using var scope = Factory.Services.CreateScope();

        var profile = await scope.ServiceProvider
            .GetRequiredService<INeighborhoodProfileRepository>()
            .GetByAreaForReadAsync(areaId);

        profile.Should().NotBeNull();

        return profile!.ListingCount;
    }
}
