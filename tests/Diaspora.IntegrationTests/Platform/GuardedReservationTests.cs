using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Housing;
using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Landlords;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Modules.Housing.Domain.Reservations;
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

namespace Diaspora.IntegrationTests.Platform;

/// <summary>
/// The window B10 and B11 both documented and neither could close, closed — through each module's
/// own port, with the platform primitive underneath.
/// <para>
/// <b>Every test here loads the aggregate before the competing hold commits.</b> That is the whole
/// design of the file, and without it none of these tests would prove anything: a caller that loads
/// <em>after</em> its competitor has committed sees the true count and its aggregate refuses on its
/// own, which is the behaviour that already worked. The interesting case — the one that oversold —
/// is two callers each holding a snapshot that says a unit is free, neither of them stale in any
/// way EF's concurrency token can see, because a buyer taking stock deliberately does not bump the
/// seller's revision. Both used to commit.
/// </para>
/// <para>
/// Two scopes rather than two threads. The suite's SQLite database lives inside one connection, so
/// genuine parallelism is not available here and pretending otherwise would make the assertion
/// weaker rather than stronger; what matters is that two change trackers each believe they may
/// proceed and the database decides between them. The parallel case — a thousand callers, each on
/// its own connection — is proved in <c>AtomicContentionTests</c> against a database that can
/// actually have two transactions.
/// </para>
/// </summary>
public sealed class GuardedReservationTests : IntegrationTestBase
{
    public GuardedReservationTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ── Marketplace ──────────────────────────────────────────────────────

    /// <summary>
    /// <b>The overselling test.</b> Two buyers, one phone, and both aggregates say yes.
    /// </summary>
    [Fact]
    public async Task Two_buyers_holding_the_last_unit_from_stale_snapshots_do_not_both_get_it()
    {
        var listingId = await SeedListingAsync(quantity: 1);

        using var first = Factory.Services.CreateScope();
        using var second = Factory.Services.CreateScope();

        // Both load while the shelf still says one.
        var firstListing = await LoadListingAsync(first, listingId);
        var secondListing = await LoadListingAsync(second, listingId);

        firstListing.Inventory.Available.Should().Be(1);
        secondListing.Inventory.Available.Should().Be(1);

        var now = DateTimeOffset.UtcNow;

        var firstHold = await first.ServiceProvider
            .GetRequiredService<IListingStockGuard>()
            .HoldAsync(firstListing, 1, now);

        firstHold.IsSuccess.Should().BeTrue();

        await first.ServiceProvider.GetRequiredService<DiasporaDbContext>().SaveChangesAsync();

        // The second buyer's aggregate still believes there is one. The database does not.
        var secondHold = await second.ServiceProvider
            .GetRequiredService<IListingStockGuard>()
            .HoldAsync(secondListing, 1, now);

        secondHold.IsFailure.Should().BeTrue(
            "the guard re-checks the arithmetic against the row, not against the snapshot");

        // The losing scope does not save, which is what every handler does with a failed hold —
        // and is why the buyer sees "insufficient stock" rather than the 409 they would get from
        // saving a snapshot the winner's status change has already made stale.
        var settled = await ReadInventoryAsync(listingId);

        settled.Reserved.Should().Be(1, "one unit was held, once");
        settled.Available.Should().Be(0);
    }

    /// <summary>
    /// A refused hold must leave nothing behind. The aggregate was mutated in memory to reach the
    /// decision; if that mutation survived, a caller that went on to save for some other reason
    /// would persist a hold nobody was granted.
    /// </summary>
    [Fact]
    public async Task A_refused_marketplace_hold_leaves_no_phantom_hold_behind()
    {
        var listingId = await SeedListingAsync(quantity: 1);

        using var winner = Factory.Services.CreateScope();
        using var loser = Factory.Services.CreateScope();

        var winning = await LoadListingAsync(winner, listingId);
        var losing = await LoadListingAsync(loser, listingId);

        var now = DateTimeOffset.UtcNow;

        await winner.ServiceProvider.GetRequiredService<IListingStockGuard>()
            .HoldAsync(winning, 1, now);

        await winner.ServiceProvider.GetRequiredService<DiasporaDbContext>().SaveChangesAsync();

        (await loser.ServiceProvider.GetRequiredService<IListingStockGuard>()
            .HoldAsync(losing, 1, now)).IsFailure.Should().BeTrue();

        losing.Inventory.Reserved.Should().Be(
            0, "the in-memory hold is rolled back when the database refuses it");

        losing.Inventory.Available.Should().Be(
            1, "and the snapshot is left exactly as it was loaded");

        (await ReadInventoryAsync(listingId)).Reserved.Should().Be(1);
    }

    /// <summary>
    /// <b>The reconciliation, end to end.</b> A hold taken against a stale snapshot is written with
    /// the count the database arbitrated, not the one the aggregate computed — and the caller's own
    /// changes still save alongside it.
    /// </summary>
    [Fact]
    public async Task A_hold_against_a_stale_snapshot_persists_the_database_count()
    {
        var listingId = await SeedListingAsync(quantity: 10);

        using var slow = Factory.Services.CreateScope();
        using var fast = Factory.Services.CreateScope();

        var slowListing = await LoadListingAsync(slow, listingId);
        var now = DateTimeOffset.UtcNow;

        // Four units go while the slow scope is still holding a snapshot that says ten are free.
        var fastListing = await LoadListingAsync(fast, listingId);

        await fast.ServiceProvider.GetRequiredService<IListingStockGuard>()
            .HoldAsync(fastListing, 4, now);

        await fast.ServiceProvider.GetRequiredService<DiasporaDbContext>().SaveChangesAsync();

        (await slow.ServiceProvider.GetRequiredService<IListingStockGuard>()
            .HoldAsync(slowListing, 2, now)).IsSuccess.Should().BeTrue();

        slowListing.Inventory.Reserved.Should().Be(
            6, "the tracked aggregate is reconciled with what the database settled on");

        await slow.ServiceProvider.GetRequiredService<DiasporaDbContext>().SaveChangesAsync();

        var settled = await ReadInventoryAsync(listingId);

        settled.Reserved.Should().Be(6, "and the save does not write the stale six back as two");
        settled.Available.Should().Be(4);
    }

    /// <summary>
    /// <b>Untracked stock counts holds without rationing them, and the guard must not change
    /// that.</b> A listing the seller has told the platform not to count still records interest —
    /// <c>ListingInventory.Hold</c> increments <c>Reserved</c> and refuses nothing — and what
    /// actually stops it is the aggregate's own <c>AcceptsCommitments</c>, which closes the listing
    /// once nothing is available. That is B10's behaviour and it is unchanged: the guard recognises
    /// untracked stock and stands aside rather than imposing arithmetic on a count the seller
    /// disclaimed.
    /// </summary>
    [Fact]
    public async Task Untracked_stock_is_rationed_by_the_aggregate_rather_than_the_guard()
    {
        var listingId = await SeedListingAsync(quantity: 2, tracked: false);

        using var scope = Factory.Services.CreateScope();

        var listing = await LoadListingAsync(scope, listingId);
        var guard = scope.ServiceProvider.GetRequiredService<IListingStockGuard>();
        var now = DateTimeOffset.UtcNow;

        (await guard.HoldAsync(listing, 1, now)).IsSuccess.Should().BeTrue();
        (await guard.HoldAsync(listing, 1, now)).IsSuccess.Should().BeTrue();

        (await guard.HoldAsync(listing, 1, now)).IsFailure.Should().BeTrue(
            "the listing closes to commitments once nothing is available, tracked or not");

        await scope.ServiceProvider.GetRequiredService<DiasporaDbContext>().SaveChangesAsync();

        (await ReadInventoryAsync(listingId)).Reserved.Should().Be(2);
    }

    /// <summary>
    /// The full lifecycle still behaves as B10 specified: hold, release, hold, settle. Behaviour
    /// preservation is the other half of this phase's claim, and the arithmetic is where it shows.
    /// </summary>
    [Fact]
    public async Task The_marketplace_stock_lifecycle_is_unchanged()
    {
        var listingId = await SeedListingAsync(quantity: 3);

        using var scope = Factory.Services.CreateScope();

        var listing = await LoadListingAsync(scope, listingId);
        var guard = scope.ServiceProvider.GetRequiredService<IListingStockGuard>();
        var context = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();
        var now = DateTimeOffset.UtcNow;

        (await guard.HoldAsync(listing, 2, now)).IsSuccess.Should().BeTrue();
        (await guard.ReleaseAsync(listing, 1, now)).IsSuccess.Should().BeTrue();
        (await guard.SettleAsync(
            listing, 1, now, UserId.New(), null, 250m, "EUR")).IsSuccess.Should().BeTrue();

        await context.SaveChangesAsync();

        var settled = await ReadInventoryAsync(listingId);

        settled.Reserved.Should().Be(0);
        settled.Sold.Should().Be(1);
        settled.Available.Should().Be(2);
    }

    // ── Housing ──────────────────────────────────────────────────────────

    /// <summary>
    /// The same window, in the module that declared a port against a primitive that did not exist
    /// yet. Nothing in Housing changed to get this except the class behind the port.
    /// </summary>
    [Fact]
    public async Task Two_seekers_holding_the_last_bed_from_stale_snapshots_do_not_both_get_it()
    {
        var listingId = await SeedHousingAsync(totalUnits: 1);

        using var first = Factory.Services.CreateScope();
        using var second = Factory.Services.CreateScope();

        // Loading through the repository is what puts the aggregate in each scope's change
        // tracker, so the guard's own load returns the snapshot rather than a fresh read.
        var firstListing = await LoadHousingAsync(first, listingId);
        var secondListing = await LoadHousingAsync(second, listingId);

        firstListing.Availability.AvailableUnits.Should().Be(1);
        secondListing.Availability.AvailableUnits.Should().Be(1);

        var now = DateTimeOffset.UtcNow;

        (await first.ServiceProvider.GetRequiredService<IHousingUnitReservationGuard>()
            .TryHoldAsync(listingId, 1, now)).IsSuccess.Should().BeTrue();

        await first.ServiceProvider.GetRequiredService<DiasporaDbContext>().SaveChangesAsync();

        var refused = await second.ServiceProvider
            .GetRequiredService<IHousingUnitReservationGuard>()
            .TryHoldAsync(listingId, 1, now);

        refused.IsFailure.Should().BeTrue("the room is gone, whatever the snapshot says");
        refused.Error.Code.Should().Be(HousingErrorCodes.NoUnitsAvailable);

        secondListing.Availability.ReservedUnits.Should().Be(
            0, "and the losing scope holds nothing it could accidentally persist");

        await second.ServiceProvider.GetRequiredService<DiasporaDbContext>().SaveChangesAsync();

        var settled = await ReadAvailabilityAsync(listingId);

        settled.ReservedUnits.Should().Be(1);
        settled.AvailableUnits.Should().Be(0);
    }

    /// <summary>
    /// Hold, occupy, vacate, release — the cycle B11 specified, unchanged. Occupation is not
    /// terminal here, which is the one place Housing's arithmetic differs from Marketplace's.
    /// </summary>
    [Fact]
    public async Task The_housing_unit_lifecycle_is_unchanged()
    {
        var listingId = await SeedHousingAsync(totalUnits: 4);

        using var scope = Factory.Services.CreateScope();

        var guard = scope.ServiceProvider.GetRequiredService<IHousingUnitReservationGuard>();
        var context = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();
        var now = DateTimeOffset.UtcNow;

        (await guard.TryHoldAsync(listingId, 3, now)).IsSuccess.Should().BeTrue();
        (await guard.ReleaseAsync(listingId, 1, now)).IsSuccess.Should().BeTrue();
        (await guard.OccupyAsync(listingId, 2, now)).IsSuccess.Should().BeTrue();

        await context.SaveChangesAsync();

        var occupied = await ReadAvailabilityAsync(listingId);

        occupied.ReservedUnits.Should().Be(0);
        occupied.OccupiedUnits.Should().Be(2);
        occupied.AvailableUnits.Should().Be(2);

        (await guard.VacateAsync(listingId, 2, now)).IsSuccess.Should().BeTrue();

        await context.SaveChangesAsync();

        var vacated = await ReadAvailabilityAsync(listingId);

        vacated.OccupiedUnits.Should().Be(0);
        vacated.AvailableUnits.Should().Be(4, "a let flat always comes back");
    }

    /// <summary>
    /// A release must never be refused, however far the counts have drifted — a room held forever
    /// is the one outcome worse than an overbooking, and the clamp is now in the statement as well
    /// as in the value object.
    /// </summary>
    [Fact]
    public async Task Releasing_more_units_than_are_held_is_clamped_rather_than_refused()
    {
        var listingId = await SeedHousingAsync(totalUnits: 3);

        using var scope = Factory.Services.CreateScope();

        var guard = scope.ServiceProvider.GetRequiredService<IHousingUnitReservationGuard>();
        var context = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();
        var now = DateTimeOffset.UtcNow;

        (await guard.TryHoldAsync(listingId, 1, now)).IsSuccess.Should().BeTrue();
        (await guard.ReleaseAsync(listingId, 3, now)).IsSuccess.Should().BeTrue();

        await context.SaveChangesAsync();

        var settled = await ReadAvailabilityAsync(listingId);

        settled.ReservedUnits.Should().Be(0);
        settled.AvailableUnits.Should().Be(3);
    }

    // ── Seeding and reading ──────────────────────────────────────────────

    private static async Task<MarketplaceListing> LoadListingAsync(
        IServiceScope scope, MarketplaceListingId id) =>
        (await scope.ServiceProvider.GetRequiredService<IMarketplaceListingRepository>()
            .GetAsync(id))!;

    private static async Task<HousingListing> LoadHousingAsync(
        IServiceScope scope, HousingListingId id) =>
        (await scope.ServiceProvider.GetRequiredService<IHousingListingRepository>()
            .GetAsync(id))!;

    private async Task<ListingInventory> ReadInventoryAsync(MarketplaceListingId id)
    {
        using var scope = Factory.Services.CreateScope();

        var listing = await scope.ServiceProvider
            .GetRequiredService<IMarketplaceListingRepository>()
            .GetForReadAsync(id);

        return listing!.Inventory;
    }

    private async Task<HousingAvailability> ReadAvailabilityAsync(HousingListingId id)
    {
        using var scope = Factory.Services.CreateScope();

        var listing = await scope.ServiceProvider
            .GetRequiredService<IHousingListingRepository>()
            .GetForReadAsync(id);

        return listing!.Availability;
    }

    private async Task<MarketplaceListingId> SeedListingAsync(int quantity, bool tracked = true)
    {
        Factory.EnsureDatabaseCreated();

        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var listing = MarketplaceListing.Create(
            MarketplaceListingId.New(),
            SellerProfileId.New(),
            UserId.New(),
            ListingTitle.Create("Guarded item").Value,
            ListingSlug.Create($"guarded-item-{Guid.NewGuid():N}").Value,
            ListingDescription.Create("An item two buyers reach for at once.").Value,
            "furniture",
            ListingType.FixedPrice,
            ProductCondition.Good,
            ListingPrice.Create(250m, "EUR", isNegotiable: false, minimumOffer: null).Value,
            ListingLocationContext.Create("DE", cityName: "Berlin").Value,
            DateTimeOffset.UtcNow).Value;

        listing.AttachMedia(Guid.NewGuid(), ListingMediaRole.Cover);

        listing.SetQuantity(quantity, tracked, baseRevision: null)
            .IsSuccess.Should().BeTrue();

        listing.Publish(DateTimeOffset.UtcNow);

        context.Set<MarketplaceListing>().Add(listing);

        await context.SaveChangesAsync();

        return listing.Id;
    }

    private async Task<HousingListingId> SeedHousingAsync(int totalUnits)
    {
        Factory.EnsureDatabaseCreated();

        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var now = DateTimeOffset.UtcNow;

        var listing = HousingListing.Draft(
            HousingListingId.New(),
            LandlordProfileId.New(),
            UserId.New(),
            HousingTitle.Create("Guarded hostel room").Value,
            HousingSlug.Create($"guarded-hostel-room-{Guid.NewGuid():N}"),
            HousingDescription.Create("A room two seekers reach for at once.").Value,
            "hostel",
            HousingPrice.Create(45m, "GBP", RentPeriod.PerNight, null).Value,
            HousingCapacity.Create(1, 1m, maximumOccupants: 1).Value,
            HousingAvailability.Create(null, totalUnits: totalUnits).Value,
            HousingLocationContext.Create(
                "GB", "Greater London", "London", "Peckham", Guid.NewGuid(),
                "/GB/greater-london/london/peckham").Value,
            PropertyAddress.Undisclosed,
            StayPolicy.Create(acceptsShortStay: true, acceptsLongStay: false).Value,
            now).Value;

        listing.AttachMedia(Guid.NewGuid(), HousingMediaRole.Cover, null, now);
        listing.Publish(now);

        context.Set<HousingListing>().Add(listing);

        await context.SaveChangesAsync();

        return listing.Id;
    }
}
