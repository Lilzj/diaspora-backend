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
/// A landlord's listing counts, on every move a property makes — 20 Sep 2026.
/// <para>
/// <b>`RecordListingCounts` had one caller.</b> The publish handler. So a landlord who archived a
/// property, let it, withdrew it or deleted it kept the figure they had at their last publication
/// until they published something else — and the figure could therefore only ever be too high,
/// which is the direction that flatters.
/// </para>
/// <para>
/// <b>And the figure it recorded was the wrong one.</b> It came from the query behind the creation
/// quota, which counts drafts, occupied and unavailable properties on purpose — the quota exists to
/// stop a script creating drafts. Read as a count of a landlord's advertisements it counted their
/// private drafts and their let homes, so it was wrong on the day it was written as well as stale
/// ever after.
/// </para>
/// <para>
/// <b>Driven through the aggregate and the real save.</b> The counts are written by domain event
/// handlers, which this platform dispatches inside <c>SaveChangesAsync</c> and persists in the
/// round that follows. A test that called the refresher directly would prove the arithmetic and
/// nothing about whether any of these moves reaches it.
/// </para>
/// </summary>
[Collection(HousingCollection.Name)]
public sealed class LandlordListingCountTests : PooledIdentityTestBase
{
    public LandlordListingCountTests(CustomWebApplicationFactory factory) : base(factory) =>
        factory.EnsureDatabaseCreated();

    [Fact]
    public async Task Publishing_a_property_puts_it_on_the_landlords_count()
    {
        var landlordId = await RegisterLandlordAsync();

        await SeedAsync(landlordId, publish: true);

        var statistics = await StatisticsOf(landlordId);

        statistics.ActiveListings.Should().Be(1);
        statistics.TotalListings.Should().Be(1);
    }

    /// <summary>
    /// <b>The defect.</b> Nothing recounted on withdrawal, so the property stayed on the landlord's
    /// figure until they happened to publish another one.
    /// </summary>
    [Fact]
    public async Task Withdrawing_a_property_takes_it_off_the_count()
    {
        var landlordId = await RegisterLandlordAsync();
        var listingId = await SeedAsync(landlordId, publish: true);

        await MoveAsync(listingId, listing =>
            listing.Archive(DateTimeOffset.UtcNow, UserId.New(), reason: null));

        var statistics = await StatisticsOf(landlordId);

        statistics.ActiveListings.Should().Be(0, "an archived property is not on the market");
        statistics.TotalListings.Should().Be(1, "but the landlord still has it");
    }

    /// <summary>
    /// Off the market without being archived. Its own documentation calls it "temporarily off the
    /// market", which is exactly what the count has to reflect.
    /// </summary>
    [Fact]
    public async Task Making_a_property_unavailable_takes_it_off_the_count()
    {
        var landlordId = await RegisterLandlordAsync();
        var listingId = await SeedAsync(landlordId, publish: true);

        await MoveAsync(listingId, listing =>
            listing.MakeUnavailable(DateTimeOffset.UtcNow, UserId.New(), reason: null));

        (await StatisticsOf(landlordId)).ActiveListings.Should().Be(0);
    }

    /// <summary>
    /// <b>A soft delete moves no status</b>, so the status-changed handler never hears about it.
    /// It is also the case where a stale count is least defensible.
    /// </summary>
    [Fact]
    public async Task Deleting_a_property_takes_it_off_the_count()
    {
        var landlordId = await RegisterLandlordAsync();
        var listingId = await SeedAsync(landlordId, publish: true);

        await MoveAsync(listingId, listing =>
        {
            listing.MarkDeleted(DateTimeOffset.UtcNow, userId: null);
            return Diaspora.SharedKernel.Results.Result.Success();
        });

        (await StatisticsOf(landlordId)).ActiveListings.Should().Be(0);
    }

    /// <summary>
    /// <b>The second half.</b> The figure used to come from the creation quota's query, in which
    /// drafts count deliberately — so a landlord with three unpublished drafts advertised three
    /// properties they had never shown anybody.
    /// </summary>
    [Fact]
    public async Task A_draft_is_not_on_the_market_but_is_still_theirs()
    {
        var landlordId = await RegisterLandlordAsync();

        await SeedAsync(landlordId, publish: true);
        await SeedAsync(landlordId, publish: false);

        var statistics = await StatisticsOf(landlordId);

        statistics.ActiveListings.Should().Be(1, "a draft is visible to nobody but its landlord");
        statistics.TotalListings.Should().Be(2);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    private async Task<LandlordProfileId> RegisterLandlordAsync()
    {
        var run = Guid.NewGuid().ToString("N")[..8];

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var profile = LandlordProfile.Register(
            LandlordProfileId.New(),
            UserId.New(),
            LandlordIdentity.Create(
                $"Counting Landlord {run}", LandlordKind.Individual, null, null).Value,
            HousingSlug.Create($"counting-landlord-{run}"),
            "GB",
            LandlordContact.Create(null, null, null, showPublicly: false).Value,
            DateTimeOffset.UtcNow).Value;

        dbContext.Set<LandlordProfile>().Add(profile);
        await dbContext.SaveChangesAsync();

        return profile.Id;
    }

    private async Task<HousingListingId> SeedAsync(LandlordProfileId landlordId, bool publish)
    {
        var run = Guid.NewGuid().ToString("N")[..8];
        var now = DateTimeOffset.UtcNow;

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var listing = HousingListing.Draft(
            HousingListingId.New(),
            landlordId,
            UserId.New(),
            HousingTitle.Create($"Counted property {run}").Value,
            HousingSlug.Create($"counted-property-{run}"),
            HousingDescription.Create("A property that exists to be counted, and then not.").Value,
            "apartment",
            HousingPrice.Create(900m, "GBP", RentPeriod.PerMonth, null).Value,
            HousingCapacity.Create(1, 1m, maximumOccupants: 2).Value,
            HousingAvailability.Create(null, totalUnits: 1).Value,
            HousingLocationContext.Create("GB", "Greater London", "London").Value,
            PropertyAddress.Create(latitude: 51.4742d, longitude: -0.0691d).Value,
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

    /// <summary>
    /// Loads the property, makes the move, and saves — which is what dispatches the event the
    /// counts hang off.
    /// </summary>
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

    private async Task<LandlordStatistics> StatisticsOf(LandlordProfileId landlordId)
    {
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var profile = await dbContext.Set<LandlordProfile>().FindAsync(landlordId);

        profile.Should().NotBeNull();

        return profile!.Statistics;
    }
}
