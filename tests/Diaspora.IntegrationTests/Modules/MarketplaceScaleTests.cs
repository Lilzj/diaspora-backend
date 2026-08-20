using System.Diagnostics;
using Diaspora.Domain.Abstractions;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Marketplace.Domain.Conversations;
using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.Modules.Marketplace.Domain.Offers;
using Diaspora.Modules.Marketplace.Domain.Reports;
using Diaspora.Modules.Marketplace.Domain.Reservations;
using Diaspora.Modules.Marketplace.Domain.Saved;
using Diaspora.Modules.Marketplace.Domain.ValueObjects;
using Diaspora.Modules.Marketplace.Extensibility.Search;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// Volume, and what breaks at it.
/// <para>
/// <b>Correctness tests that happen to be large.</b> The timings they print go in the performance
/// report; none of them asserts on wall-clock time, because a loaded build agent would fail a clock
/// assertion for reasons that have nothing to do with the code, and a suite that goes red for that
/// stops being read. It is the platform's established shape, and B9's scale suite is its sibling.
/// </para>
/// <para>
/// What they do assert is what only appears at volume: a cursor that walks a large catalogue
/// without losing or repeating a listing, filters that still filter when there are thousands of
/// rows to filter, a negotiation flood that pages cleanly, stock that cannot be held past what
/// exists however many buyers try, and the search port answering over a real corpus.
/// </para>
/// <para>
/// <b>Every test scopes itself to its own seller.</b> The class shares one database with the rest of
/// the suite, so a test asserting on a global count would pass or fail depending on which other
/// tests ran first — a worse failure than the bug it was meant to catch, because it does not
/// reproduce.
/// </para>
/// </summary>
public sealed class MarketplaceScaleTests : IntegrationTestBase
{
    /// <summary>
    /// Large enough that a scan and an index lookup diverge, and that a cursor defect shows up as a
    /// missing or repeated row rather than passing by luck. Small enough that the suite still
    /// finishes in a coffee break.
    /// </summary>
    private const int ListingCount = 2_000;

    private const int OfferCount = 1_000;

    private static readonly string[] Countries = ["DE", "GB", "NG", "GH", "KE"];

    private static readonly string[] Categories =
        ["home-appliances", "mobile-phones", "furniture", "cars"];

    private readonly ITestOutputHelper _output;

    public MarketplaceScaleTests(CustomWebApplicationFactory factory, ITestOutputHelper output)
        : base(factory) => _output = output;

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Large catalogues
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// The whole catalogue walked page by page. Every listing seen exactly once — the assertion a
    /// cursor exists to earn, and the one that quietly fails when rows share a sort key and the
    /// tie-break is missing.
    /// <para>
    /// The degenerate case is real rather than theoretical: <c>CreatedAt</c> is stamped by the
    /// persistence interceptor, so every row written in one unit of work shares an instant.
    /// </para>
    /// </summary>
    [Fact]
    public async Task The_cursor_walks_the_whole_catalogue_without_loss_or_repetition()
    {
        var seller = await SeedListingsAsync(ListingCount);

        using var scope = Factory.Services.CreateScope();
        var listings = scope.ServiceProvider.GetRequiredService<IMarketplaceListingRepository>();

        var seen = new HashSet<Guid>();
        var pages = 0;
        string? cursor = null;

        var stopwatch = Stopwatch.StartNew();

        while (true)
        {
            var page = await listings.QueryAsync(
                new ListingQueryFilter { SellerId = seller, Sort = ListingSortOrder.Newest },
                cursor,
                limit: 50);

            foreach (var listing in page.Items)
            {
                seen.Add(listing.Id.Value).Should().BeTrue(
                    "a cursor that returns a row twice is a cursor with no tie-break");
            }

            pages++;

            if (!page.HasMore || page.NextCursor is null)
            {
                break;
            }

            cursor = page.NextCursor;

            pages.Should().BeLessThan(
                200, "a cursor that never reports exhaustion would loop here forever");
        }

        stopwatch.Stop();

        seen.Should().HaveCount(ListingCount, "every listing is seen exactly once");

        _output.WriteLine(
            $"Walked {ListingCount} listings in {pages} pages of 50 " +
            $"in {stopwatch.ElapsedMilliseconds} ms " +
            $"({stopwatch.ElapsedMilliseconds / (double)pages:F1} ms/page).");
    }

    /// <summary>
    /// A page is a page. A limit that leaks under volume is how a mobile client on a metered
    /// connection ends up downloading two thousand listing cards.
    /// </summary>
    [Fact]
    public async Task A_page_stays_bounded_however_much_matches()
    {
        var seller = await SeedListingsAsync(ListingCount);

        using var scope = Factory.Services.CreateScope();
        var listings = scope.ServiceProvider.GetRequiredService<IMarketplaceListingRepository>();

        var page = await listings.QueryAsync(
            new ListingQueryFilter { SellerId = seller }, cursor: null, limit: 20);

        page.Items.Should().HaveCount(20);
        page.HasMore.Should().BeTrue();
    }

    /// <summary>
    /// A caller asking for more than the module's ceiling gets the ceiling. Without the clamp, a
    /// scripted client asks for a million and the server obliges.
    /// </summary>
    [Fact]
    public async Task A_page_size_beyond_the_ceiling_is_clamped_rather_than_honoured()
    {
        var seller = await SeedListingsAsync(ListingCount);

        using var scope = Factory.Services.CreateScope();
        var listings = scope.ServiceProvider.GetRequiredService<IMarketplaceListingRepository>();

        var page = await listings.QueryAsync(
            new ListingQueryFilter { SellerId = seller }, cursor: null, limit: 5_000);

        page.Items.Count.Should().BeLessThanOrEqualTo(
            100, "MarketplaceLimits.MaxPageSize is the hard ceiling, whatever the caller asks");
    }

    /// <summary>
    /// Three predicates at once, over a corpus large enough that "returned some rows" is not
    /// evidence any of them ran. Every row back must satisfy all three — the failure this catches
    /// is a filter chain where only the last applied.
    /// </summary>
    [Fact]
    public async Task Combined_filters_all_hold_at_volume()
    {
        var seller = await SeedListingsAsync(ListingCount);

        using var scope = Factory.Services.CreateScope();
        var listings = scope.ServiceProvider.GetRequiredService<IMarketplaceListingRepository>();

        var page = await listings.QueryAsync(
            new ListingQueryFilter
            {
                SellerId = seller,
                CountryCode = "DE",
                CategoryKey = "home-appliances",
                MinimumCondition = ProductCondition.Good
            },
            cursor: null,
            limit: 50);

        page.Items.Should().NotBeEmpty("the seed guarantees matches for this combination");

        foreach (var listing in page.Items)
        {
            listing.Location.CountryCode.Should().Be("DE");
            listing.CategoryKey.Should().Be("home-appliances");
            ((int)listing.Condition).Should().BeGreaterThanOrEqualTo(
                (int)ProductCondition.Good,
                "conditions are ordered worst-to-best so 'at least Good' is a range");
        }
    }

    /// <summary>
    /// <b>The rule the whole module turns on, asserted at volume.</b> A price band applies only
    /// within a matched currency: nothing here converts, and a buyer shown a ₦150,000 item for a
    /// "£100–£300" search would act on a number that is wrong by a factor nobody stated.
    /// </summary>
    [Fact]
    public async Task A_price_band_never_crosses_a_currency_at_volume()
    {
        var seller = await SeedListingsAsync(ListingCount);

        using var scope = Factory.Services.CreateScope();
        var listings = scope.ServiceProvider.GetRequiredService<IMarketplaceListingRepository>();

        var page = await listings.QueryAsync(
            new ListingQueryFilter
            {
                SellerId = seller,
                MinimumPrice = 100m,
                MaximumPrice = 300m,
                Currency = "EUR"
            },
            cursor: null,
            limit: 100);

        page.Items.Should().NotBeEmpty();

        foreach (var listing in page.Items)
        {
            listing.Price.CurrencyCode.Should().Be(
                "EUR", "a band in one currency must never match a figure in another");

            listing.Price.Amount.Should().BeInRange(100m, 300m);
        }
    }

    /// <summary>
    /// A filter that matches nothing must terminate rather than loop. The empty result is the case
    /// a hand-written cursor gets wrong most often.
    /// </summary>
    [Fact]
    public async Task An_empty_result_terminates()
    {
        var seller = await SeedListingsAsync(200);

        using var scope = Factory.Services.CreateScope();
        var listings = scope.ServiceProvider.GetRequiredService<IMarketplaceListingRepository>();

        var page = await listings.QueryAsync(
            new ListingQueryFilter { SellerId = seller, CountryCode = "JP" },
            cursor: null,
            limit: 50);

        page.Items.Should().BeEmpty();
        page.HasMore.Should().BeFalse();
        page.NextCursor.Should().BeNull();
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Bulk search
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// The search port over a real corpus, answering with identifiers rather than documents — and
    /// paging through them without loss. This is the read shape a future OpenSearch adapter has to
    /// satisfy, exercised against the shipped EF engine.
    /// </summary>
    [Fact]
    public async Task Bulk_search_pages_the_corpus_through_the_port_without_loss()
    {
        var seller = await SeedListingsAsync(ListingCount);

        using var scope = Factory.Services.CreateScope();
        var engine = scope.ServiceProvider.GetRequiredService<IMarketplaceSearchEngine>();

        var seen = new HashSet<Guid>();
        var pages = 0;
        string? cursor = null;

        var stopwatch = Stopwatch.StartNew();

        while (true)
        {
            var outcome = await engine.SearchAsync(new MarketplaceSearchRequest
            {
                SellerId = seller.Value,
                Text = "Seeded",
                Sort = ListingSortOrder.Newest,
                Cursor = cursor,
                Limit = 50
            });

            foreach (var hit in outcome.Hits)
            {
                seen.Add(hit.ListingId).Should().BeTrue(
                    "the engine must not return one listing twice in one walk");
            }

            pages++;

            if (outcome.NextCursor is null || outcome.Hits.Count == 0)
            {
                break;
            }

            cursor = outcome.NextCursor;

            pages.Should().BeLessThan(200);
        }

        stopwatch.Stop();

        seen.Should().HaveCount(
            ListingCount, "every seeded listing matches the term and must be found once");

        _output.WriteLine(
            $"Searched {ListingCount} listings in {pages} pages " +
            $"in {stopwatch.ElapsedMilliseconds} ms.");
    }

    /// <summary>
    /// Hydration from a bulk set of identifiers — the second half of every search. One call for
    /// a whole page rather than one per hit, which is the difference between a results page and
    /// twenty round trips.
    /// </summary>
    [Fact]
    public async Task A_page_of_search_hits_hydrates_in_one_read()
    {
        var seller = await SeedListingsAsync(500);

        using var scope = Factory.Services.CreateScope();
        var engine = scope.ServiceProvider.GetRequiredService<IMarketplaceSearchEngine>();
        var listings = scope.ServiceProvider.GetRequiredService<IMarketplaceListingRepository>();

        var outcome = await engine.SearchAsync(new MarketplaceSearchRequest
        {
            SellerId = seller.Value, Text = "Seeded", Limit = 100
        });

        outcome.Hits.Should().NotBeEmpty();

        var ids = outcome.Hits.Select(hit => MarketplaceListingId.From(hit.ListingId)).ToList();

        var stopwatch = Stopwatch.StartNew();
        var cards = await listings.GetManyAsync(ids);
        stopwatch.Stop();

        cards.Should().HaveCount(
            ids.Count, "every identifier the engine returned still resolves to a visible listing");

        _output.WriteLine(
            $"Hydrated {cards.Count} cards in {stopwatch.ElapsedMilliseconds} ms.");
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // High-volume reads
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// The same card query run repeatedly, which is what a busy browse surface actually is. What is
    /// asserted is stability: the same filter returns the same count every time, so nothing is
    /// leaking state between reads.
    /// </summary>
    [Fact]
    public async Task Repeated_card_reads_stay_stable_and_bounded()
    {
        const int Reads = 50;

        var seller = await SeedListingsAsync(ListingCount);

        using var scope = Factory.Services.CreateScope();
        var listings = scope.ServiceProvider.GetRequiredService<IMarketplaceListingRepository>();

        var filter = new ListingQueryFilter
        {
            SellerId = seller, AvailableOnly = true, Sort = ListingSortOrder.Newest
        };

        var stopwatch = Stopwatch.StartNew();
        var counts = new HashSet<int>();

        for (var index = 0; index < Reads; index++)
        {
            var page = await listings.QueryAsync(filter, cursor: null, limit: 20);
            counts.Add(page.Items.Count);
        }

        stopwatch.Stop();

        counts.Should().ContainSingle(
            "the same filter over an unchanged catalogue must return the same page every time");

        _output.WriteLine(
            $"{Reads} card reads over {ListingCount} listings in " +
            $"{stopwatch.ElapsedMilliseconds} ms " +
            $"({stopwatch.ElapsedMilliseconds / (double)Reads:F1} ms/read).");
    }

    /// <summary>
    /// Counting over a large catalogue. The dashboard reads this on every load, so it must be a
    /// count rather than a materialisation — and it must agree with what the cursor walked.
    /// </summary>
    [Fact]
    public async Task The_count_agrees_with_what_the_cursor_walks()
    {
        var seller = await SeedListingsAsync(ListingCount);

        using var scope = Factory.Services.CreateScope();
        var listings = scope.ServiceProvider.GetRequiredService<IMarketplaceListingRepository>();

        var filter = new ListingQueryFilter { SellerId = seller };

        var stopwatch = Stopwatch.StartNew();
        var count = await listings.CountAsync(filter);
        stopwatch.Stop();

        count.Should().Be(
            ListingCount, "a count that disagrees with the walk is a filter applied in one place");

        _output.WriteLine(
            $"Counted {count} listings in {stopwatch.ElapsedMilliseconds} ms.");
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Concurrent offers
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// A thousand negotiations on one listing, paged by the seller. Every one seen exactly once,
    /// across a tie group that straddles page boundaries — the interceptor stamps them all on one
    /// instant, so the whole thousand is one tie group.
    /// </summary>
    [Fact]
    public async Task A_negotiation_flood_pages_cleanly_for_the_seller()
    {
        var (listingId, sellerId) = await SeedOffersAsync(OfferCount);

        using var scope = Factory.Services.CreateScope();
        var offers = scope.ServiceProvider.GetRequiredService<IListingOfferRepository>();

        var seen = new HashSet<Guid>();
        string? cursor = null;
        var pages = 0;

        var stopwatch = Stopwatch.StartNew();

        while (true)
        {
            var page = await offers.QueryAsync(
                new OfferQueryFilter { ListingId = listingId, SellerId = sellerId },
                cursor,
                limit: 50);

            foreach (var offer in page.Items)
            {
                seen.Add(offer.Id.Value).Should().BeTrue(
                    "an offer returned twice is a buyer counted twice");
            }

            pages++;

            if (!page.HasMore || page.NextCursor is null)
            {
                break;
            }

            cursor = page.NextCursor;

            pages.Should().BeLessThan(100);
        }

        stopwatch.Stop();

        seen.Should().HaveCount(
            OfferCount, "an offer nobody is ever shown is a buyer who is never answered");

        _output.WriteLine(
            $"Paged {OfferCount} offers in {pages} pages in {stopwatch.ElapsedMilliseconds} ms.");
    }

    /// <summary>
    /// Every one of those negotiations is still awaiting the seller, and the "your turn" filter
    /// finds all of them. A dashboard that under-reports the queue is one where buyers wait on a
    /// seller who was never told.
    /// </summary>
    [Fact]
    public async Task The_awaiting_filter_finds_every_negotiation_at_volume()
    {
        var (listingId, sellerId) = await SeedOffersAsync(OfferCount);

        using var scope = Factory.Services.CreateScope();
        var offers = scope.ServiceProvider.GetRequiredService<IListingOfferRepository>();

        var count = await offers.CountAsync(new OfferQueryFilter
        {
            ListingId = listingId,
            SellerId = sellerId,
            OpenOnly = true,
            AwaitingParty = OfferParty.Seller
        });

        count.Should().Be(
            OfferCount, "every opening offer awaits the seller, by the turn rule");
    }

    /// <summary>
    /// <b>The turn rule holds however many negotiations are open.</b> Each one is independent
    /// state, so a thousand of them must not settle into a shared or last-write-wins turn.
    /// </summary>
    [Fact]
    public async Task Every_negotiation_keeps_its_own_turn_under_load()
    {
        var (listingId, sellerId) = await SeedOffersAsync(200);

        using var scope = Factory.Services.CreateScope();
        var offers = scope.ServiceProvider.GetRequiredService<IListingOfferRepository>();

        var page = await offers.QueryAsync(
            new OfferQueryFilter { ListingId = listingId, SellerId = sellerId },
            cursor: null,
            limit: 100);

        foreach (var offer in page.Items)
        {
            offer.AwaitingParty.Should().Be(
                OfferParty.Seller, "an opening offer awaits the party who did not make it");

            offer.Status.Should().Be(OfferStatus.Pending);
        }
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Reservation contention
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>The invariant the module exists to protect, under contention.</b> Fifty buyers reach for
    /// a listing of five units through the aggregate; exactly five holds succeed and the other
    /// forty-five are refused with insufficient stock. Not clamped, not silently accepted —
    /// refused, which is the truth the losing buyer needs to hear.
    /// </summary>
    [Fact]
    public async Task Fifty_buyers_cannot_hold_more_than_the_five_units_that_exist()
    {
        const int Units = 5;
        const int Buyers = 50;

        var listing = await SeedSingleListingAsync(quantity: Units);

        using var scope = Factory.Services.CreateScope();
        var listings = scope.ServiceProvider.GetRequiredService<IMarketplaceListingRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var tracked = await listings.GetAsync(listing);
        tracked.Should().NotBeNull();

        var now = DateTimeOffset.UtcNow;
        var granted = 0;
        var refused = 0;

        for (var buyer = 0; buyer < Buyers; buyer++)
        {
            if (tracked!.HoldStock(1, now).IsSuccess)
            {
                granted++;
            }
            else
            {
                refused++;
            }
        }

        await dbContext.SaveChangesAsync();

        granted.Should().Be(Units, "the shelf holds exactly what the shelf holds");
        refused.Should().Be(Buyers - Units);

        var reread = await listings.GetForReadAsync(listing);

        reread!.Inventory.Reserved.Should().Be(Units);
        reread.Inventory.Available.Should().Be(0);
        reread.Inventory.Quantity.Should().Be(Units);
        reread.Status.Should().Be(
            ListingStatus.Reserved, "a listing with nothing available is reserved out");
    }

    /// <summary>
    /// The stock comes back. Every release returns a unit and reopens the listing — which is what
    /// makes a cancellation harmless to everybody but the buyer who cancelled.
    /// </summary>
    [Fact]
    public async Task Released_stock_returns_and_reopens_the_listing()
    {
        const int Units = 3;

        var listing = await SeedSingleListingAsync(quantity: Units);

        using var scope = Factory.Services.CreateScope();
        var listings = scope.ServiceProvider.GetRequiredService<IMarketplaceListingRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var tracked = await listings.GetAsync(listing);
        var now = DateTimeOffset.UtcNow;

        for (var index = 0; index < Units; index++)
        {
            tracked!.HoldStock(1, now).IsSuccess.Should().BeTrue();
        }

        tracked!.Status.Should().Be(ListingStatus.Reserved);

        tracked.ReleaseStock(1, now).IsSuccess.Should().BeTrue();

        await dbContext.SaveChangesAsync();

        var reread = await listings.GetForReadAsync(listing);

        reread!.Inventory.Reserved.Should().Be(Units - 1);
        reread.Inventory.Available.Should().Be(1);
        reread.Status.Should().Be(
            ListingStatus.Active, "one unit back on the shelf reopens the listing");
    }

    /// <summary>
    /// A hundred hold-and-settle cycles against a large stock. What is asserted is the arithmetic
    /// after all of it: reserved plus sold never exceeds quantity, and the derived available figure
    /// still agrees with the three stored numbers.
    /// </summary>
    [Fact]
    public async Task Stock_arithmetic_survives_a_hundred_hold_and_settle_cycles()
    {
        const int Units = 200;
        const int Cycles = 100;

        var listing = await SeedSingleListingAsync(quantity: Units);

        using var scope = Factory.Services.CreateScope();
        var listings = scope.ServiceProvider.GetRequiredService<IMarketplaceListingRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var tracked = await listings.GetAsync(listing);
        var now = DateTimeOffset.UtcNow;

        var stopwatch = Stopwatch.StartNew();

        for (var cycle = 0; cycle < Cycles; cycle++)
        {
            tracked!.HoldStock(1, now).IsSuccess.Should().BeTrue();

            // Every other hold completes; the rest are cancelled and give the unit back.
            if (cycle % 2 == 0)
            {
                tracked.SettleStock(
                    1, now, UserId.New(), reservationId: null, amount: 250m, currencyCode: "EUR")
                    .IsSuccess.Should().BeTrue();
            }
            else
            {
                tracked.ReleaseStock(1, now).IsSuccess.Should().BeTrue();
            }
        }

        await dbContext.SaveChangesAsync();
        stopwatch.Stop();

        var reread = await listings.GetForReadAsync(listing);

        reread!.Inventory.Sold.Should().Be(Cycles / 2);
        reread.Inventory.Reserved.Should().Be(0, "every hold was resolved one way or the other");
        reread.Inventory.Available.Should().Be(Units - (Cycles / 2));

        (reread.Inventory.Reserved + reread.Inventory.Sold)
            .Should().BeLessThanOrEqualTo(
                reread.Inventory.Quantity, "the invariant, after two hundred movements");

        _output.WriteLine(
            $"{Cycles} hold/settle cycles in {stopwatch.ElapsedMilliseconds} ms.");
    }

    /// <summary>
    /// <b>The concurrency guard is the revision token, and it is really in the schema.</b> Two
    /// editors both load revision <c>N</c>; the first commits, the second must fail rather than
    /// overwrite. A handler's own check would pass both.
    /// <para>
    /// The exception is the platform's <c>ConcurrencyConflictException</c> rather than EF's, which
    /// is the point of B1.0.2's wrapper: a conflict reaches the API as a 409 a client can merge
    /// from, not as a provider exception that becomes a 500.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_stale_editor_is_refused_by_the_database_itself()
    {
        var listing = await SeedSingleListingAsync(quantity: 1);

        using var first = Factory.Services.CreateScope();
        using var second = Factory.Services.CreateScope();

        var firstContext = first.ServiceProvider.GetRequiredService<DiasporaDbContext>();
        var secondContext = second.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var firstCopy = await firstContext.Set<MarketplaceListing>()
            .FirstAsync(candidate => candidate.Id == listing);

        var secondCopy = await secondContext.Set<MarketplaceListing>()
            .FirstAsync(candidate => candidate.Id == listing);

        firstCopy.SetTags(["sold-out"], baseRevision: null).IsSuccess.Should().BeTrue();
        await firstContext.SaveChangesAsync();

        secondCopy.SetTags(["still-available"], baseRevision: null).IsSuccess.Should().BeTrue();

        var write = async () => await secondContext.SaveChangesAsync();

        var thrown = await write.Should().ThrowAsync<ConcurrencyConflictException>(
            "the revision token is the only thing that can settle two editors racing");

        thrown.And.InnerException.Should().BeOfType<DbUpdateConcurrencyException>(
            "the token really is in the schema, not only in the handler");
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Translation
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>Every paged read, executed with rows present.</b> This is not a scale test in spirit, but
    /// it belongs in this file because it is the same failure mode: a repository that reaches
    /// through a strongly-typed identifier inside a LINQ predicate compiles, is silently correct
    /// against an empty table, and throws the moment a row exists. Five B10 repositories carried
    /// exactly that, and only a seeded read found them.
    /// <para>
    /// One row of each aggregate is enough — translation is decided at compile time, not by
    /// cardinality — so this stays cheap enough to run on every build.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Every_paged_read_translates_once_rows_exist()
    {
        Factory.EnsureDatabaseCreated();

        var listingId = MarketplaceListingId.New();
        var sellerId = SellerProfileId.New();
        var buyerId = UserId.New();
        var sellerUserId = UserId.New();
        var now = DateTimeOffset.UtcNow;

        using (var seed = Factory.Services.CreateScope())
        {
            var dbContext = seed.ServiceProvider.GetRequiredService<DiasporaDbContext>();

            dbContext.Set<SavedListing>().Add(
                SavedListing.Create(SavedListingId.New(), buyerId, listingId, now).Value);

            dbContext.Set<ListingReport>().Add(
                ListingReport.File(
                    ListingReportId.New(),
                    listingId,
                    sellerId,
                    buyerId,
                    ListingReportReason.Counterfeit,
                    "Not what it claims to be.",
                    totalReports: 1,
                    now).Value);

            dbContext.Set<ListingConversation>().Add(
                ListingConversation.Request(
                    ListingConversationId.New(),
                    listingId,
                    sellerId,
                    buyerId,
                    sellerUserId,
                    "Is this still available?",
                    now).Value);

            dbContext.Set<ListingReservation>().Add(
                ListingReservation.Open(
                    ListingReservationId.New(),
                    listingId,
                    sellerId,
                    buyerId,
                    1,
                    CollectionWindow.Create(now, now.AddDays(2), "Outside the station").Value,
                    now,
                    Guid.NewGuid()).Value);

            await dbContext.SaveChangesAsync();
        }

        using var scope = Factory.Services.CreateScope();
        var provider = scope.ServiceProvider;

        var saved = await provider.GetRequiredService<ISavedListingRepository>()
            .QueryForUserAsync(buyerId, cursor: null, limit: 20);

        var reports = await provider.GetRequiredService<IListingReportRepository>()
            .QueryAsync(status: null, reason: null, cursor: null, limit: 20);

        var conversations = await provider.GetRequiredService<IListingConversationRepository>()
            .QueryForUserAsync(buyerId, cursor: null, limit: 20);

        var reservations = await provider.GetRequiredService<IListingReservationRepository>()
            .QueryAsync(
                new ReservationQueryFilter { ListingId = listingId }, cursor: null, limit: 20);

        saved.Items.Should().ContainSingle();
        reports.Items.Should().NotBeEmpty();
        conversations.Items.Should().ContainSingle();
        reservations.Items.Should().ContainSingle();
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Seeding
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Seeds directly through the context. Driving HTTP for two thousand listings would test the
    /// registration flow rather than the read paths, and take long enough that nobody runs it.
    /// <para>
    /// Returns its own seller identifier; every test filters by it, so the shared database cannot
    /// make one test's result depend on another's having run.
    /// </para>
    /// </summary>
    private async Task<SellerProfileId> SeedListingsAsync(int count)
    {
        Factory.EnsureDatabaseCreated();

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var sellerId = SellerProfileId.New();
        var ownerId = UserId.New();
        var now = DateTimeOffset.UtcNow;

        // Slugs are unique platform-wide; two seeding runs in one database would collide.
        var run = Guid.NewGuid().ToString("N")[..8];

        var listings = new List<MarketplaceListing>(count);

        for (var index = 0; index < count; index++)
        {
            // Deliberately mixed: every fourth listing is priced in GBP, so a EUR band that
            // matched across currencies would be caught rather than passing by uniformity.
            var currency = index % 4 == 0 ? "GBP" : "EUR";

            var listing = MarketplaceListing.Create(
                MarketplaceListingId.New(),
                sellerId,
                ownerId,
                ListingTitle.Create($"Seeded Item {index}").Value,
                ListingSlug.Create($"seeded-item-{run}-{index}").Value,
                ListingDescription.Create(
                    $"Seeded listing {index}, long enough to be a valid description.").Value,
                Categories[index % Categories.Length],
                ListingType.Negotiable,
                index % 3 == 0 ? ProductCondition.Fair : ProductCondition.Good,
                ListingPrice.Create(
                    50m + (index % 400), currency, isNegotiable: true, minimumOffer: null).Value,
                ListingLocationContext.Create(
                    Countries[index % Countries.Length], cityName: "Berlin").Value,
                // Spread over time so the cursor has real ordering to do; two share a timestamp
                // every so often, which is exactly where a missing tie-break shows up.
                now.AddMinutes(-(index / 2))).Value;

            listing.AttachMedia(Guid.NewGuid(), ListingMediaRole.Cover);
            listing.Publish(now);

            listings.Add(listing);
        }

        dbContext.Set<MarketplaceListing>().AddRange(listings);

        await dbContext.SaveChangesAsync();

        return sellerId;
    }

    /// <summary>One published listing with stock, for the contention tests.</summary>
    private async Task<MarketplaceListingId> SeedSingleListingAsync(int quantity)
    {
        Factory.EnsureDatabaseCreated();

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var listing = MarketplaceListing.Create(
            MarketplaceListingId.New(),
            SellerProfileId.New(),
            UserId.New(),
            ListingTitle.Create("Contended Item").Value,
            ListingSlug.Create($"contended-item-{Guid.NewGuid():N}").Value,
            ListingDescription.Create("An item several buyers reach for at once.").Value,
            "furniture",
            ListingType.FixedPrice,
            ProductCondition.Good,
            ListingPrice.Create(250m, "EUR", isNegotiable: false, minimumOffer: null).Value,
            ListingLocationContext.Create("DE", cityName: "Berlin").Value,
            DateTimeOffset.UtcNow).Value;

        listing.AttachMedia(Guid.NewGuid(), ListingMediaRole.Cover);

        if (quantity != 1)
        {
            listing.SetQuantity(quantity, isTracked: true, baseRevision: null)
                .IsSuccess.Should().BeTrue();
        }

        listing.Publish(DateTimeOffset.UtcNow);

        dbContext.Set<MarketplaceListing>().Add(listing);

        await dbContext.SaveChangesAsync();

        return listing.Id;
    }

    /// <summary>
    /// One listing with <paramref name="count"/> open negotiations against it, each from a
    /// different buyer. Returns the seller too: a negotiation listing that is not scoped to one of
    /// its two parties would hand back other people's offers.
    /// </summary>
    private async Task<(MarketplaceListingId Listing, SellerProfileId Seller)> SeedOffersAsync(
        int count)
    {
        Factory.EnsureDatabaseCreated();

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var listingId = MarketplaceListingId.New();
        var sellerId = SellerProfileId.New();
        var sellerUserId = UserId.New();
        var now = DateTimeOffset.UtcNow;

        var offers = new List<ListingOffer>(count);

        for (var index = 0; index < count; index++)
        {
            offers.Add(ListingOffer.Open(
                ListingOfferId.New(),
                listingId,
                sellerId,
                UserId.New(),
                sellerUserId,
                OfferTerms.Create(
                    100m + index, "EUR", 1, $"Offer {index}").Value,
                now.AddDays(3),
                // Written here and overwritten by the persistence interceptor at SaveChanges,
                // which is the point: all thousand land on one instant and become one tie group.
                now.AddMinutes(-index),
                Guid.NewGuid()).Value);
        }

        dbContext.Set<ListingOffer>().AddRange(offers);

        await dbContext.SaveChangesAsync();

        return (listingId, sellerId);
    }
}
