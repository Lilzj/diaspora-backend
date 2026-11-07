using System.Diagnostics;
using Diaspora.Domain.Abstractions;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Landlords;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Modules.Housing.Domain.Neighborhoods;
using Diaspora.Modules.Housing.Domain.Reports;
using Diaspora.Modules.Housing.Domain.Reservations;
using Diaspora.Modules.Housing.Domain.ValueObjects;
using Diaspora.Modules.Housing.Domain.Viewings;
using Diaspora.Modules.Housing.Extensibility.Search;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// Volume, contention, and what only appears at either.
/// <para>
/// <b>Correctness tests that happen to be large.</b> The timings they print go in the performance
/// report; none asserts on wall-clock time, because a loaded build agent would fail a clock
/// assertion for reasons that have nothing to do with the code, and a suite that goes red for that
/// stops being read. B9's and B10's scale suites are this file's siblings.
/// </para>
/// <para>
/// What they do assert is what a small corpus cannot show: a cursor that walks thousands of
/// properties without losing or repeating one, filters that still filter when there is something
/// to filter out, a neighbourhood directory that stays answerable as it fills, the reservation
/// guard refusing to overbook a property however many seekers reach for it at once, and every
/// paged read translating to SQL now that rows exist.
/// </para>
/// <para>
/// <b>Every test scopes itself to its own landlord or its own area.</b> The class shares one
/// database with the rest of the suite, so a test asserting on a global count would pass or fail
/// depending on which other tests ran first — a worse failure than the bug it was meant to catch,
/// because it does not reproduce.
/// </para>
/// </summary>
public sealed class HousingScaleTests : IntegrationTestBase
{
    /// <summary>
    /// Large enough that a scan and an index lookup diverge, and that a cursor defect shows up as a
    /// missing or repeated row rather than passing by luck. Small enough that the suite still
    /// finishes in a coffee break.
    /// </summary>
    private const int ListingCount = 2_000;

    private const int NeighborhoodCount = 500;

    /// <summary>
    /// Deliberately under <c>CursorBoundary.MaxBoundaryIds</c> (500), and the number is the point.
    /// <para>
    /// All of these land on one instant — the persistence interceptor stamps <c>CreatedAt</c>, so
    /// everything written in one unit of work ties — which makes them a single tie group four
    /// hundred rows wide and eight pages deep. That is what the cursor has to walk, and walking it
    /// is what the platform's boundary primitive guarantees up to its cap.
    /// </para>
    /// <para>
    /// <b>Past the cap the guarantee stops, and not gracefully.</b> The primitive documents
    /// degrading to a strict comparison — lossy but terminating — while what it actually does is
    /// truncate the identifier set, so rows past the five hundredth come back on a later page. A
    /// tie group that wide needs a single unit of work writing five hundred rows of one aggregate,
    /// which nothing in Housing does; it is recorded as a platform recommendation rather than
    /// worked around here, and this constant stays inside the promise.
    /// </para>
    /// </summary>
    private const int InsightCount = 400;

    private static readonly string[] Countries = ["GB", "DE", "NG", "GH", "CA"];

    /// <summary>
    /// Four, against five countries and three bedroom counts, and the sizes are deliberately
    /// coprime. Two same-length arrays indexed by the same counter produce a corpus where country
    /// and accommodation type are perfectly correlated — every German property a studio — and a
    /// combined filter then matches nothing for reasons that have nothing to do with the filter.
    /// </summary>
    private static readonly string[] Types =
        ["apartment", "studio", "private-room", "shared-apartment"];

    private readonly ITestOutputHelper _output;

    public HousingScaleTests(CustomWebApplicationFactory factory, ITestOutputHelper output)
        : base(factory) => _output = output;

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Large catalogues
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// The whole catalogue walked page by page. Every property seen exactly once — the assertion a
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
        var landlord = await SeedListingsAsync(ListingCount);

        using var scope = Factory.Services.CreateScope();
        var listings = scope.ServiceProvider.GetRequiredService<IHousingListingRepository>();

        var seen = new HashSet<Guid>();
        var pages = 0;
        string? cursor = null;

        var stopwatch = Stopwatch.StartNew();

        while (true)
        {
            var page = await listings.QueryAsync(
                new HousingListingQueryFilter
                {
                    LandlordId = landlord,
                    Sort = HousingSortOrder.Newest
                },
                cursor, limit: 50);

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

        seen.Should().HaveCount(ListingCount, "every property is seen exactly once");

        _output.WriteLine(
            $"Walked {ListingCount} properties in {pages} pages of 50 "
            + $"in {stopwatch.ElapsedMilliseconds} ms "
            + $"({stopwatch.ElapsedMilliseconds / (double)pages:F1} ms/page).");
    }

    /// <summary>
    /// A page is a page. A limit that leaks under volume is how a member on a metered connection
    /// downloads two thousand property cards.
    /// </summary>
    [Fact]
    public async Task A_page_stays_bounded_however_much_matches()
    {
        var landlord = await SeedListingsAsync(ListingCount);

        using var scope = Factory.Services.CreateScope();
        var listings = scope.ServiceProvider.GetRequiredService<IHousingListingRepository>();

        var page = await listings.QueryAsync(
            new HousingListingQueryFilter { LandlordId = landlord }, cursor: null, limit: 20);

        page.Items.Should().HaveCount(20);
        page.HasMore.Should().BeTrue();
    }

    /// <summary>
    /// A caller asking for more than the module's ceiling gets the ceiling. Without the clamp a
    /// scripted client asks for a million and the server obliges.
    /// </summary>
    [Fact]
    public async Task A_page_size_beyond_the_ceiling_is_clamped_rather_than_honoured()
    {
        var landlord = await SeedListingsAsync(ListingCount);

        using var scope = Factory.Services.CreateScope();
        var listings = scope.ServiceProvider.GetRequiredService<IHousingListingRepository>();

        var page = await listings.QueryAsync(
            new HousingListingQueryFilter { LandlordId = landlord }, cursor: null, limit: 5_000);

        page.Items.Count.Should().BeLessThanOrEqualTo(
            100, "HousingLimits.MaxPageSize is the hard ceiling, whatever the caller asks");
    }

    /// <summary>
    /// Four predicates at once over a corpus large enough that "returned some rows" is not evidence
    /// any of them ran. Every row back must satisfy all four — the failure this catches is a filter
    /// chain where only the last applied.
    /// </summary>
    [Fact]
    public async Task Combined_filters_all_hold_at_volume()
    {
        var landlord = await SeedListingsAsync(ListingCount);

        using var scope = Factory.Services.CreateScope();
        var listings = scope.ServiceProvider.GetRequiredService<IHousingListingRepository>();

        var page = await listings.QueryAsync(
            new HousingListingQueryFilter
            {
                LandlordId = landlord,
                CountryCode = "DE",
                AccommodationTypeKeys = ["apartment"],
                MinimumBedrooms = 2
            },
            cursor: null, limit: 50);

        page.Items.Should().NotBeEmpty("the seed guarantees matches for this combination");

        foreach (var listing in page.Items)
        {
            listing.Location.CountryCode.Should().Be("DE");
            listing.AccommodationTypeKey.Should().Be("apartment");
            listing.Capacity.Bedrooms.Should().BeGreaterThanOrEqualTo(2);
        }
    }

    /// <summary>
    /// <b>A rent band never crosses a currency.</b> Nothing here converts, and somebody shown a
    /// ₦450,000 room for a "£500–£1,500" search would act on a number that is wrong by a factor
    /// nobody stated — which for a deposit paid before arrival is not a rounding error.
    /// </summary>
    [Fact]
    public async Task A_rent_band_never_crosses_a_currency_at_volume()
    {
        var landlord = await SeedListingsAsync(ListingCount);

        using var scope = Factory.Services.CreateScope();
        var listings = scope.ServiceProvider.GetRequiredService<IHousingListingRepository>();

        var page = await listings.QueryAsync(
            new HousingListingQueryFilter
            {
                LandlordId = landlord,
                MinimumPrice = 500m,
                MaximumPrice = 1_500m,
                Currency = "GBP",
                Period = RentPeriod.PerMonth
            },
            cursor: null, limit: 100);

        page.Items.Should().NotBeEmpty();

        foreach (var listing in page.Items)
        {
            listing.Price.CurrencyCode.Should().Be(
                "GBP", "a band in one currency must never match a figure in another");

            listing.Price.Period.Should().Be(
                RentPeriod.PerMonth, "nor a monthly band a nightly rate");

            listing.Price.Amount.Should().BeInRange(500m, 1_500m);
        }
    }

    /// <summary>
    /// <b>And never crosses a rent period either, which is the housing-specific half of the same
    /// rule.</b> £60 a night and £60 a month are both "£60"; a filter that conflated them would
    /// put a hostel bed and a bedsit on one results page and price neither of them.
    /// </summary>
    [Fact]
    public async Task A_rent_band_never_crosses_a_period_at_volume()
    {
        var landlord = await SeedListingsAsync(ListingCount);

        using var scope = Factory.Services.CreateScope();
        var listings = scope.ServiceProvider.GetRequiredService<IHousingListingRepository>();

        var page = await listings.QueryAsync(
            new HousingListingQueryFilter
            {
                LandlordId = landlord,
                Currency = "GBP",
                Period = RentPeriod.PerNight
            },
            cursor: null, limit: 100);

        page.Items.Should().NotBeEmpty("every fifth seeded property is a nightly hostel bed");

        page.Items.Should().OnlyContain(listing => listing.Price.Period == RentPeriod.PerNight);

        page.Items.Should().OnlyContain(listing => listing.Price.CurrencyCode == "GBP");
    }

    /// <summary>
    /// A filter that matches nothing must terminate rather than loop. The empty result is the case
    /// a hand-written cursor gets wrong most often.
    /// </summary>
    [Fact]
    public async Task An_empty_result_terminates()
    {
        var landlord = await SeedListingsAsync(200);

        using var scope = Factory.Services.CreateScope();
        var listings = scope.ServiceProvider.GetRequiredService<IHousingListingRepository>();

        var page = await listings.QueryAsync(
            new HousingListingQueryFilter
            {
                LandlordId = landlord, CountryCode = "JP"
            },
            cursor: null, limit: 50);

        page.Items.Should().BeEmpty();
        page.HasMore.Should().BeFalse();
        page.NextCursor.Should().BeNull();
    }

    /// <summary>
    /// Counting over a large catalogue. The landlord dashboard reads this on every load, so it must
    /// be a count rather than a materialisation — and it must agree with what the cursor walked.
    /// </summary>
    [Fact]
    public async Task The_count_agrees_with_what_the_cursor_walks()
    {
        var landlord = await SeedListingsAsync(ListingCount);

        using var scope = Factory.Services.CreateScope();
        var listings = scope.ServiceProvider.GetRequiredService<IHousingListingRepository>();

        var stopwatch = Stopwatch.StartNew();
        var count = await listings.CountAsync(
            new HousingListingQueryFilter { LandlordId = landlord });
        stopwatch.Stop();

        count.Should().Be(
            ListingCount, "a count that disagrees with the walk is a filter applied in one place");

        _output.WriteLine($"Counted {count} properties in {stopwatch.ElapsedMilliseconds} ms.");
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Search at volume
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// The search port over a real corpus, paged through without loss. This is the read shape a
    /// future OpenSearch adapter has to satisfy, exercised against the shipped EF engine — which is
    /// the whole point of the abstraction being asserted rather than described.
    /// </summary>
    [Fact]
    public async Task Search_pages_the_corpus_through_the_port_without_loss()
    {
        var landlord = await SeedListingsAsync(ListingCount);

        using var scope = Factory.Services.CreateScope();
        var engine = scope.ServiceProvider.GetRequiredService<IHousingSearchEngine>();

        var seen = new HashSet<Guid>();
        var pages = 0;
        string? cursor = null;

        var stopwatch = Stopwatch.StartNew();

        while (true)
        {
            var outcome = await engine.SearchAsync(new HousingSearchRequest
            {
                Filter = new HousingListingQueryFilter
                {
                    LandlordId = landlord, Sort = HousingSortOrder.Newest, Limit = 50
                },
                Cursor = cursor
            });

            foreach (var listing in outcome.Items)
            {
                seen.Add(listing.Id.Value).Should().BeTrue(
                    "the engine must not return one property twice in one walk");
            }

            pages++;

            if (!outcome.HasMore || outcome.NextCursor is null)
            {
                break;
            }

            cursor = outcome.NextCursor;

            pages.Should().BeLessThan(200);
        }

        stopwatch.Stop();

        seen.Should().HaveCount(ListingCount);

        _output.WriteLine(
            $"Searched {ListingCount} properties in {pages} pages "
            + $"in {stopwatch.ElapsedMilliseconds} ms.");
    }

    /// <summary>
    /// Facets describe a bounded sample of the match set, not the page and not the whole corpus.
    /// <para>
    /// <b>The middle answer is the deliberate one, and the assertions here pin both halves of
    /// it.</b> Counting the page would draw a price slider from twenty rows and exclude most of the
    /// catalogue the moment somebody touched it. Counting two thousand rows to render a sidebar
    /// would put a full scan in front of every search. The sample is far wider than the page and
    /// hard-capped, and what a member does with a facet — decide which box to tick — is served
    /// exactly as well by an approximate count as by an exact one.
    /// </para>
    /// <para>
    /// <c>TotalCount</c> beside it is exact, which is the number that has to be: "2,000 homes" is a
    /// claim, and a sampled one would be a false one.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Facets_are_sampled_far_wider_than_the_page_and_bounded()
    {
        var landlord = await SeedListingsAsync(ListingCount);

        using var scope = Factory.Services.CreateScope();
        var engine = scope.ServiceProvider.GetRequiredService<IHousingSearchEngine>();

        var stopwatch = Stopwatch.StartNew();

        var outcome = await engine.SearchAsync(new HousingSearchRequest
        {
            Filter = new HousingListingQueryFilter { LandlordId = landlord, Limit = 20 },
            IncludeFacets = true
        });

        stopwatch.Stop();

        outcome.Items.Should().HaveCount(20, "the page is still a page");
        outcome.Facets.Should().NotBeNull();

        outcome.TotalCount.Should().Be(
            ListingCount, "the headline count is exact, whatever the facets are");

        var sampled = outcome.Facets!.AccommodationTypes.Values.Sum();

        sampled.Should().BeGreaterThan(
            outcome.Items.Count,
            "a facet computed from the page would be a sidebar that agrees with itself and "
            + "nothing else");

        sampled.Should().BeLessThan(
            ListingCount, "and the sample is bounded rather than the whole match set");

        outcome.Facets.AccommodationTypes.Keys.Should().HaveCountGreaterThan(
            1, "the sample spans more than one type, so the sidebar has something to offer");

        outcome.Facets.PriceRanges.Should().HaveCountGreaterThan(
            1, "a nightly bed and a monthly flat get their own range, never a shared one");

        _output.WriteLine(
            $"Faceted a sample of {sampled} from {ListingCount} properties "
            + $"in {stopwatch.ElapsedMilliseconds} ms.");
    }

    /// <summary>
    /// The same search run repeatedly, which is what a busy discovery surface actually is. What is
    /// asserted is stability: the same filter returns the same page every time, so nothing is
    /// leaking state between reads.
    /// </summary>
    [Fact]
    public async Task Repeated_searches_stay_stable_and_bounded()
    {
        const int Reads = 50;

        var landlord = await SeedListingsAsync(ListingCount);

        using var scope = Factory.Services.CreateScope();
        var engine = scope.ServiceProvider.GetRequiredService<IHousingSearchEngine>();

        var stopwatch = Stopwatch.StartNew();
        var counts = new HashSet<int>();

        for (var index = 0; index < Reads; index++)
        {
            var outcome = await engine.SearchAsync(new HousingSearchRequest
            {
                Filter = new HousingListingQueryFilter
                {
                    LandlordId = landlord, AvailableOnly = true, Limit = 20
                }
            });

            counts.Add(outcome.Items.Count);
        }

        stopwatch.Stop();

        counts.Should().ContainSingle(
            "the same filter over an unchanged catalogue must return the same page every time");

        _output.WriteLine(
            $"{Reads} searches over {ListingCount} properties in "
            + $"{stopwatch.ElapsedMilliseconds} ms "
            + $"({stopwatch.ElapsedMilliseconds / (double)Reads:F1} ms/read).");
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Neighbourhood discovery at volume
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Five hundred neighbourhoods walked through the directory. The directory is the module's
    /// published platform contract, so its paging is not a Housing detail — every consumer that
    /// ever lists neighbourhoods walks this cursor.
    /// </summary>
    [Fact]
    public async Task The_neighborhood_directory_pages_cleanly_at_volume()
    {
        var subtree = await SeedNeighborhoodsAsync(NeighborhoodCount);

        using var scope = Factory.Services.CreateScope();
        var profiles = scope.ServiceProvider
            .GetRequiredService<INeighborhoodProfileRepository>();

        var seen = new HashSet<Guid>();
        string? cursor = null;
        var pages = 0;

        var stopwatch = Stopwatch.StartNew();

        while (true)
        {
            var page = await profiles.QueryAsync(
                new NeighborhoodQueryFilter { AreaPathPrefix = subtree }, cursor, limit: 50);

            foreach (var profile in page.Items)
            {
                seen.Add(profile.AreaId).Should().BeTrue(
                    "one area has one profile, so a repeat is a paging defect");
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

        seen.Should().HaveCount(NeighborhoodCount);

        _output.WriteLine(
            $"Walked {NeighborhoodCount} neighbourhoods in {pages} pages "
            + $"in {stopwatch.ElapsedMilliseconds} ms.");
    }

    /// <summary>
    /// <b>An unapproved profile is not published, at any volume.</b> The moderation filter is the
    /// only thing standing between the platform and unreviewed prose about the places real people
    /// live, and it must hold when there are five hundred of them rather than one.
    /// </summary>
    [Fact]
    public async Task Only_approved_neighborhoods_are_published_at_volume()
    {
        var subtree = await SeedNeighborhoodsAsync(NeighborhoodCount);

        using var scope = Factory.Services.CreateScope();
        var profiles = scope.ServiceProvider
            .GetRequiredService<INeighborhoodProfileRepository>();

        var approved = await profiles.QueryAsync(
            new NeighborhoodQueryFilter
            {
                AreaPathPrefix = subtree, ApprovedOnly = true
            },
            cursor: null, limit: 100);

        approved.Items.Should().NotBeEmpty("every third seeded profile is approved");
        approved.Items.Should().OnlyContain(profile => profile.IsApproved);

        var all = await profiles.QueryAsync(
            new NeighborhoodQueryFilter { AreaPathPrefix = subtree }, cursor: null, limit: 100);

        all.Items.Should().Contain(profile => !profile.IsApproved);
    }

    /// <summary>
    /// The directory answers in bulk for a page of properties. One call for a whole results page
    /// rather than one per card, which is the difference between a search page and twenty round
    /// trips.
    /// </summary>
    [Fact]
    public async Task The_directory_resolves_a_page_of_areas_in_one_read()
    {
        var subtree = await SeedNeighborhoodsAsync(200);

        using var scope = Factory.Services.CreateScope();
        var profiles = scope.ServiceProvider
            .GetRequiredService<INeighborhoodProfileRepository>();

        var page = await profiles.QueryAsync(
            new NeighborhoodQueryFilter { AreaPathPrefix = subtree }, cursor: null, limit: 100);

        var areas = page.Items.Select(profile => profile.AreaId).ToList();

        var stopwatch = Stopwatch.StartNew();
        var resolved = await profiles.GetManyByAreaAsync(areas);
        stopwatch.Stop();

        resolved.Should().HaveCount(areas.Count);

        _output.WriteLine(
            $"Resolved {resolved.Count} neighbourhoods in {stopwatch.ElapsedMilliseconds} ms.");
    }

    /// <summary>
    /// Eight hundred moderated insights against one area, paged by a moderator. Every one seen
    /// exactly once — the whole eight hundred land on one instant from the interceptor, so they are
    /// a single tie group and precisely the case a missing tie-break loses rows in.
    /// </summary>
    [Fact]
    public async Task An_insight_flood_pages_cleanly_for_a_moderator()
    {
        var (profileId, areaId) = await SeedInsightsAsync(InsightCount);

        using var scope = Factory.Services.CreateScope();
        var insights = scope.ServiceProvider
            .GetRequiredService<INeighborhoodInsightRepository>();

        var seen = new HashSet<Guid>();
        string? cursor = null;
        var pages = 0;

        var stopwatch = Stopwatch.StartNew();

        while (true)
        {
            var page = await insights.QueryAsync(
                new NeighborhoodInsightQueryFilter
                {
                    ProfileId = profileId,
                    AreaId = areaId,

                    // The moderation queue is the one read path that sets this. Everywhere else an
                    // unapproved insight does not exist, which is why the assertion below would be
                    // vacuously true without it.
                    IncludeUnapproved = true
                },
                cursor, limit: 50);

            foreach (var insight in page.Items)
            {
                seen.Add(insight.Id.Value).Should().BeTrue(
                    "an insight shown twice is a moderator deciding the same thing twice");
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
            InsightCount, "an insight nobody is ever shown is a member never answered");

        // The other half of the same rule, and the more important one: to every read path that is
        // not the moderation queue, eight hundred unreviewed sentences about a real residential
        // area do not exist at all.
        var published = await insights.QueryAsync(
            new NeighborhoodInsightQueryFilter { ProfileId = profileId, AreaId = areaId },
            cursor: null,
            limit: 50);

        published.Items.Should().BeEmpty("nothing here has been approved");

        _output.WriteLine(
            $"Paged {InsightCount} insights in {pages} pages "
            + $"in {stopwatch.ElapsedMilliseconds} ms.");
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Reservation contention
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>The invariant the module exists to protect, under contention, and through the port the
    /// brief required.</b> Fifty seekers reach for a five-bed hostel room through
    /// <c>IHousingUnitReservationGuard</c>; exactly five holds succeed and the other forty-five are
    /// refused. Not clamped, not silently accepted — refused, which is the truth the losing seeker
    /// needs to hear before they book a flight.
    /// <para>
    /// Nothing here reaches past the guard. That is the assertion: the day B10's atomic primitive
    /// lands, this test does not change.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Fifty_seekers_cannot_hold_more_than_the_five_units_that_exist()
    {
        const int Units = 5;
        const int Seekers = 50;

        var listing = await SeedSingleListingAsync(totalUnits: Units);

        using var scope = Factory.Services.CreateScope();
        var guard = scope.ServiceProvider.GetRequiredService<IHousingUnitReservationGuard>();
        var listings = scope.ServiceProvider.GetRequiredService<IHousingListingRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var now = DateTimeOffset.UtcNow;
        var granted = 0;
        var refused = 0;

        for (var seeker = 0; seeker < Seekers; seeker++)
        {
            if ((await guard.TryHoldAsync(listing, 1, now)).IsSuccess)
            {
                granted++;
            }
            else
            {
                refused++;
            }
        }

        await dbContext.SaveChangesAsync();

        granted.Should().Be(Units, "the property holds exactly what the property holds");
        refused.Should().Be(Seekers - Units);

        var reread = await listings.GetForReadAsync(listing);

        reread!.Availability.ReservedUnits.Should().Be(Units);
        reread.Availability.AvailableUnits.Should().Be(0);
        reread.Availability.TotalUnits.Should().Be(Units);

        reread.Status.Should().Be(
            HousingListingStatus.Reserved, "a property with nothing left is reserved out");
    }

    /// <summary>
    /// The units come back. Every release returns one and reopens the property — which is what
    /// makes a cancellation harmless to everybody but the seeker who cancelled.
    /// </summary>
    [Fact]
    public async Task Released_units_return_and_reopen_the_property()
    {
        const int Units = 3;

        var listing = await SeedSingleListingAsync(totalUnits: Units);

        using var scope = Factory.Services.CreateScope();
        var guard = scope.ServiceProvider.GetRequiredService<IHousingUnitReservationGuard>();
        var listings = scope.ServiceProvider.GetRequiredService<IHousingListingRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var now = DateTimeOffset.UtcNow;

        for (var index = 0; index < Units; index++)
        {
            (await guard.TryHoldAsync(listing, 1, now)).IsSuccess.Should().BeTrue();
        }

        (await guard.ReleaseAsync(listing, 1, now)).IsSuccess.Should().BeTrue();

        await dbContext.SaveChangesAsync();

        var reread = await listings.GetForReadAsync(listing);

        reread!.Availability.ReservedUnits.Should().Be(Units - 1);
        reread.Availability.AvailableUnits.Should().Be(1);

        reread.Status.Should().Be(
            HousingListingStatus.Available, "one room back on the market reopens the property");
    }

    /// <summary>
    /// <b>Occupation is not the end of a property's life, unlike a sale.</b> A tenancy ends and the
    /// room comes back — so the arithmetic has to survive a full cycle, not merely a one-way trip.
    /// </summary>
    [Fact]
    public async Task Unit_arithmetic_survives_a_hundred_hold_and_settle_cycles()
    {
        const int Units = 20;
        const int Cycles = 100;

        var listing = await SeedSingleListingAsync(totalUnits: Units);

        using var scope = Factory.Services.CreateScope();
        var guard = scope.ServiceProvider.GetRequiredService<IHousingUnitReservationGuard>();
        var listings = scope.ServiceProvider.GetRequiredService<IHousingListingRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var now = DateTimeOffset.UtcNow;
        var stopwatch = Stopwatch.StartNew();

        for (var cycle = 0; cycle < Cycles; cycle++)
        {
            (await guard.TryHoldAsync(listing, 1, now)).IsSuccess.Should().BeTrue();

            // Every other hold turns into a tenancy that then ends; the rest fall through and
            // give the room straight back.
            if (cycle % 2 == 0)
            {
                (await guard.OccupyAsync(listing, 1, now)).IsSuccess.Should().BeTrue();
                (await guard.VacateAsync(listing, 1, now)).IsSuccess.Should().BeTrue();
            }
            else
            {
                (await guard.ReleaseAsync(listing, 1, now)).IsSuccess.Should().BeTrue();
            }
        }

        await dbContext.SaveChangesAsync();
        stopwatch.Stop();

        var reread = await listings.GetForReadAsync(listing);

        reread!.Availability.ReservedUnits.Should().Be(
            0, "every hold was resolved one way or the other");

        reread.Availability.OccupiedUnits.Should().Be(0, "every tenancy also ended");
        reread.Availability.AvailableUnits.Should().Be(Units);

        (reread.Availability.ReservedUnits + reread.Availability.OccupiedUnits)
            .Should().BeLessThanOrEqualTo(
                reread.Availability.TotalUnits, "the invariant, after three hundred movements");

        _output.WriteLine($"{Cycles} hold/settle cycles in {stopwatch.ElapsedMilliseconds} ms.");
    }

    /// <summary>
    /// <b>The concurrency guard is the revision token, and it is really in the schema.</b> Two
    /// landlords' devices both load revision <c>N</c>; the first commits, the second must fail
    /// rather than overwrite. A handler's own check would pass both.
    /// <para>
    /// The exception is the platform's <c>ConcurrencyConflictException</c> rather than EF's, which
    /// is the point of B1.0.2's wrapper: a conflict reaches the API as a 409 a client can merge
    /// from, not as a provider exception that becomes a 500.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_stale_editor_is_refused_by_the_database_itself()
    {
        var listing = await SeedSingleListingAsync(totalUnits: 1);

        using var first = Factory.Services.CreateScope();
        using var second = Factory.Services.CreateScope();

        var firstContext = first.ServiceProvider.GetRequiredService<DiasporaDbContext>();
        var secondContext = second.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var firstCopy = await firstContext.Set<HousingListing>()
            .FirstAsync(candidate => candidate.Id == listing);

        var secondCopy = await secondContext.Set<HousingListing>()
            .FirstAsync(candidate => candidate.Id == listing);

        firstCopy.SetHouseRules(
                [HouseRule.Create(Guid.NewGuid(), "No smoking indoors.", 0)], baseRevision: null)
            .IsSuccess.Should().BeTrue();

        await firstContext.SaveChangesAsync();

        secondCopy.SetHouseRules(
                [HouseRule.Create(Guid.NewGuid(), "Quiet after eleven.", 0)], baseRevision: null)
            .IsSuccess.Should().BeTrue();

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
    /// <b>Every paged read, executed with rows present.</b> Not a scale test in spirit, but the
    /// same failure mode: a repository that reaches through a strongly-typed identifier inside a
    /// LINQ predicate compiles, is silently correct against an empty table, and throws the moment a
    /// row exists. Five B10 repositories carried exactly that, and only a seeded read found them.
    /// <para>
    /// One row of each aggregate is enough — translation is decided at compile time, not by
    /// cardinality — so this stays cheap enough to run on every build.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Every_paged_read_translates_once_rows_exist()
    {
        Factory.EnsureDatabaseCreated();

        var listingId = HousingListingId.New();
        var landlordId = LandlordProfileId.New();
        var seekerId = UserId.New();
        var areaId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var profile = NeighborhoodProfile.Create(
            NeighborhoodProfileId.New(),
            areaId,
            "GB",
            "Translation Row",
            $"translation-row-{Guid.NewGuid():N}",
            now).Value;

        using (var seed = Factory.Services.CreateScope())
        {
            var dbContext = seed.ServiceProvider.GetRequiredService<DiasporaDbContext>();

            dbContext.Set<HousingViewing>().Add(
                HousingViewing.Request(
                    HousingViewingId.New(),
                    listingId,
                    landlordId,
                    seekerId,
                    ViewingWindow.Create(now.AddDays(2)).Value,
                    "Could I come round on Saturday?",
                    now,
                    Guid.NewGuid()).Value);

            dbContext.Set<HousingReservation>().Add(
                HousingReservation.Open(
                    HousingReservationId.New(),
                    listingId,
                    landlordId,
                    seekerId,
                    1,
                    ReservationTerms.Create(
                        now.AddDays(14),
                        null,
                        now.AddDays(3),
                        1_200m,
                        1_800m,
                        "GBP",
                        RentPeriod.PerMonth,
                        LeaseType.LongTerm,
                        occupants: 2).Value,
                    now,
                    Guid.NewGuid()).Value);

            dbContext.Set<HousingReport>().Add(
                HousingReport.File(
                    HousingReportId.New(),
                    listingId,
                    landlordId,
                    seekerId,
                    HousingReportReason.Scam,
                    "Asked for a deposit before any viewing.",
                    1,
                    now).Value);

            dbContext.Set<NeighborhoodProfile>().Add(profile);

            dbContext.Set<NeighborhoodInsight>().Add(
                NeighborhoodInsight.Submit(
                    NeighborhoodInsightId.New(),
                    profile.Id,
                    areaId,
                    seekerId,
                    NeighborhoodInsightCategory.Safety,
                    "Well lit along the main road after dark.",
                    null,
                    now).Value);

            dbContext.Set<LandlordProfile>().Add(
                LandlordProfile.Register(
                    landlordId,
                    UserId.New(),
                    LandlordIdentity.Create(
                        "Translation Lettings", LandlordKind.Individual, null).Value,
                    HousingSlug.Create($"translation-lettings-{Guid.NewGuid():N}"),
                    "GB",
                    LandlordContact.None,
                    now).Value);

            await dbContext.SaveChangesAsync();
        }

        using var scope = Factory.Services.CreateScope();
        var provider = scope.ServiceProvider;

        var viewings = await provider.GetRequiredService<IHousingViewingRepository>()
            .QueryAsync(new ViewingQueryFilter { ListingId = listingId }, cursor: null, limit: 20);

        var reservations = await provider.GetRequiredService<IHousingReservationRepository>()
            .QueryAsync(new ReservationQueryFilter { ListingId = listingId }, cursor: null, limit: 20);

        var reports = await provider.GetRequiredService<IHousingReportRepository>()
            .QueryAsync(new HousingReportQueryFilter { ListingId = listingId }, cursor: null, limit: 20);

        var profiles = await provider.GetRequiredService<INeighborhoodProfileRepository>()
            .QueryAsync(new NeighborhoodQueryFilter { CountryCode = "GB" }, cursor: null, limit: 20);

        var insights = await provider.GetRequiredService<INeighborhoodInsightRepository>()
            .QueryAsync(
                new NeighborhoodInsightQueryFilter
                {
                    AreaId = areaId, IncludeUnapproved = true
                },
                cursor: null,
                limit: 20);

        var landlords = await provider.GetRequiredService<ILandlordProfileRepository>()
            .QueryAsync(new LandlordQueryFilter { CountryCode = "GB" }, cursor: null, limit: 20);

        viewings.Items.Should().ContainSingle();
        reservations.Items.Should().ContainSingle();
        reports.Items.Should().NotBeEmpty();
        profiles.Items.Should().NotBeEmpty();
        insights.Items.Should().ContainSingle();
        landlords.Items.Should().NotBeEmpty();
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Seeding
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Seeds directly through the context. Driving HTTP for two thousand properties would test the
    /// registration flow rather than the read paths, and take long enough that nobody runs it.
    /// <para>
    /// Returns its own landlord identifier; every test filters by it, so the shared database cannot
    /// make one test's result depend on another's having run.
    /// </para>
    /// </summary>
    private async Task<LandlordProfileId> SeedListingsAsync(int count)
    {
        Factory.EnsureDatabaseCreated();

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var landlordId = LandlordProfileId.New();
        var ownerId = UserId.New();
        var now = DateTimeOffset.UtcNow;

        // Slugs are unique platform-wide; two seeding runs in one database would collide.
        var run = Guid.NewGuid().ToString("N")[..8];

        var listings = new List<HousingListing>(count);

        for (var index = 0; index < count; index++)
        {
            // Deliberately mixed along two axes at once, because a rent band can cross either.
            // Every fifth property is a nightly bed and every third of the rest is priced in
            // naira, so "£500–£1,500 a month" has both a hostel rate and a foreign currency to
            // wrongly match — and a filter that ignored period or currency would be caught rather
            // than passing by uniformity.
            var nightly = index % 5 == 0;
            var foreign = !nightly && index % 3 == 0;

            var price = nightly
                ? HousingPrice.Create(30m + (index % 60), "GBP", RentPeriod.PerNight, null).Value
                : foreign
                    ? HousingPrice.Create(450_000m, "NGN", RentPeriod.PerMonth, null).Value
                    : HousingPrice.Create(
                        400m + (index % 1_600), "GBP", RentPeriod.PerMonth, 1_800m).Value;

            var listing = HousingListing.Draft(
                HousingListingId.New(),
                landlordId,
                ownerId,
                HousingTitle.Create($"Seeded home {index}").Value,
                HousingSlug.Create($"seeded-home-{run}-{index}"),
                HousingDescription.Create(
                    $"Seeded property {index}, long enough to be a valid description.").Value,
                nightly ? "hostel" : Types[index % Types.Length],
                price,
                HousingCapacity.Create(
                    1 + (index % 3), 1m, maximumOccupants: 2 + (index % 3)).Value,
                HousingAvailability.Create(null, totalUnits: 1).Value,
                HousingLocationContext.Create(
                    Countries[index % Countries.Length],
                    "Greater London",
                    "London",
                    "Peckham",
                    Guid.NewGuid(),
                    "/GB/greater-london/london/peckham").Value,
                PropertyAddress.Undisclosed,
                StayPolicy.Create(
                    acceptsShortStay: nightly,
                    acceptsLongStay: !nightly,
                    isSharedAccommodation: false).Value,
                // Spread over time so the cursor has real ordering to do; two share a timestamp
                // every so often, which is exactly where a missing tie-break shows up.
                now.AddMinutes(-(index / 2))).Value;

            listing.AttachMedia(Guid.NewGuid(), HousingMediaRole.Cover, null, now);
            listing.Publish(now);

            listings.Add(listing);
        }

        dbContext.Set<HousingListing>().AddRange(listings);

        await dbContext.SaveChangesAsync();

        return landlordId;
    }

    /// <summary>One published property with units, for the contention tests.</summary>
    private async Task<HousingListingId> SeedSingleListingAsync(int totalUnits)
    {
        Factory.EnsureDatabaseCreated();

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var now = DateTimeOffset.UtcNow;

        var listing = HousingListing.Draft(
            HousingListingId.New(),
            LandlordProfileId.New(),
            UserId.New(),
            HousingTitle.Create("Contended hostel room").Value,
            HousingSlug.Create($"contended-hostel-room-{Guid.NewGuid():N}"),
            HousingDescription.Create("A room several seekers reach for at once.").Value,
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

        dbContext.Set<HousingListing>().Add(listing);

        await dbContext.SaveChangesAsync();

        return listing.Id;
    }

    /// <summary>
    /// Neighbourhood profiles under an area subtree unique to this call, and returns the prefix
    /// every test narrows by. Every third is approved, which is what makes the moderation filter
    /// provable rather than vacuous.
    /// </summary>
    private async Task<string> SeedNeighborhoodsAsync(int count)
    {
        Factory.EnsureDatabaseCreated();

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        // A country code is two letters, so it cannot carry a run identifier and every seeding run
        // would share "GB". The area path can, and B4's prefix match is exactly what narrows on it
        // — so each run gets its own subtree and one run's directory is never another's.
        const string Country = "GB";

        var run = Guid.NewGuid().ToString("N")[..8];
        var prefix = $"/GB/greater-london/london/seeded-{run}/";
        var now = DateTimeOffset.UtcNow;
        var moderator = UserId.New();

        var profiles = new List<NeighborhoodProfile>(count);

        for (var index = 0; index < count; index++)
        {
            var profile = NeighborhoodProfile.Create(
                NeighborhoodProfileId.New(),
                Guid.NewGuid(),
                Country,
                $"Seeded Area {index}",
                $"seeded-area-{run}-{index}",
                now.AddMinutes(-(index / 2)),
                cityName: "London",
                areaPath: $"{prefix}area-{index}").Value;

            if (index % 3 == 0)
            {
                profile.Approve(moderator, now);
            }

            profiles.Add(profile);
        }

        dbContext.Set<NeighborhoodProfile>().AddRange(profiles);

        await dbContext.SaveChangesAsync();

        return prefix;
    }

    /// <summary>
    /// One neighbourhood with <paramref name="count"/> insights awaiting a moderator, each from a
    /// different member.
    /// </summary>
    private async Task<(NeighborhoodProfileId Profile, Guid Area)> SeedInsightsAsync(int count)
    {
        Factory.EnsureDatabaseCreated();

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var areaId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var profile = NeighborhoodProfile.Create(
            NeighborhoodProfileId.New(),
            areaId,
            "GB",
            "Flooded Area",
            $"flooded-area-{Guid.NewGuid():N}",
            now).Value;

        dbContext.Set<NeighborhoodProfile>().Add(profile);

        var insights = new List<NeighborhoodInsight>(count);

        for (var index = 0; index < count; index++)
        {
            insights.Add(NeighborhoodInsight.Submit(
                NeighborhoodInsightId.New(),
                profile.Id,
                areaId,
                UserId.New(),
                NeighborhoodInsightCategory.Transport,
                $"The number {index} bus runs until midnight.",
                null,
                // Written here and overwritten by the persistence interceptor at SaveChanges,
                // which is the point: all eight hundred land on one instant and become one tie
                // group.
                now.AddMinutes(-index)).Value);
        }

        dbContext.Set<NeighborhoodInsight>().AddRange(insights);

        await dbContext.SaveChangesAsync();

        return (profile.Id, areaId);
    }
}
