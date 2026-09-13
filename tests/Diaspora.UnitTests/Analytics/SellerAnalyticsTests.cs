using Diaspora.Modules.Analytics.Application.Services;
using Diaspora.Modules.Analytics.Domain.Enums;
using Diaspora.Modules.Analytics.Domain.Identifiers;
using Diaspora.Modules.Analytics.Domain.Periods;
using Diaspora.Modules.Analytics.Domain.Policies;
using Diaspora.Modules.Analytics.Domain.Repositories;
using Diaspora.Modules.Analytics.Domain.Rollups;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Diaspora.UnitTests.Analytics;

/// <summary>
/// A seller's own marketplace figures — FRONTEND-PHASE15-GAP-011, 13 Sep 2026.
/// <para>
/// <b>Written at the traps rather than the happy path.</b> The figure is "listing rollups summed to
/// the seller", and there are two ways to serve a confident wrong number from that sentence: count
/// the seller's guide readers as people who looked at their listings, and report a 0% conversion
/// rate for a listing nobody opened. Both would render as figures. Neither is one.
/// </para>
/// </summary>
public sealed class SellerAnalyticsTests
{
    private static readonly Guid Seller = Guid.Parse("0dc73f0c-7efa-4b60-8706-7a60eacace65");
    private static readonly Guid Listing = Guid.Parse("3f2504e0-4f89-41d3-9a0c-0305e82c3301");
    private static readonly Guid SecondListing = Guid.Parse("9c858901-8a57-4791-81fe-4c455b099bc9");
    private static readonly Guid Guide = Guid.Parse("4b2e1f9a-8c3d-4a71-b0e5-1f6d9c8a7b32");

    // ── Conversion rate ─────────────────────────────────────────────────────

    [Fact]
    public void A_listing_nobody_viewed_has_no_conversion_rate_rather_than_zero_percent()
    {
        // Zero percent would tell a seller their listing fails to sell. It has not been seen.
        AnalyticsMetricAssembler.ConversionRate(Counts(views: 0, conversions: 0)).Should().BeNull();
    }

    [Fact]
    public void Conversion_rate_is_sales_per_view_as_a_percentage()
    {
        AnalyticsMetricAssembler.ConversionRate(Counts(views: 200, conversions: 5))
            .Should().Be(2.5);
    }

    [Fact]
    public void Conversion_rate_rounds_to_two_places_away_from_zero()
    {
        // 1 ÷ 3 × 100 = 33.333…
        AnalyticsMetricAssembler.ConversionRate(Counts(views: 3, conversions: 1))
            .Should().Be(33.33);
    }

    [Fact]
    public void The_served_rate_is_marked_unmeasured_when_there_were_no_views()
    {
        // `ValueOf` gives an unavailable rate a 0 to carry. The flag is what stops it being shown,
        // and it is the half that matters.
        var metrics = AnalyticsMetricAssembler.BuildSet(
            [AnalyticsMetricCatalog.ConversionRate],
            new AnalyticsWindowTotals(Counts(views: 0, conversions: 0), 0, 0, HasData: true, HasHistory: true),
            new AnalyticsWindowTotals(Counts(views: 10, conversions: 1), 0, 0, HasData: true, HasHistory: true));

        metrics.Single().IsAvailable.Should().BeFalse();
    }

    [Fact]
    public void The_served_rate_is_measured_once_there_are_views()
    {
        var metrics = AnalyticsMetricAssembler.BuildSet(
            [AnalyticsMetricCatalog.ConversionRate],
            new AnalyticsWindowTotals(Counts(views: 40, conversions: 2), 0, 0, HasData: true, HasHistory: true),
            new AnalyticsWindowTotals(Counts(views: 20, conversions: 1), 0, 0, HasData: true, HasHistory: true));

        var metric = metrics.Single();
        metric.IsAvailable.Should().BeTrue();
        metric.Value.Should().Be(5);
    }

    // ── The headline set ────────────────────────────────────────────────────

    [Fact]
    public void Every_seller_metric_resolves_against_the_catalogue()
    {
        foreach (var key in AnalyticsMetricCatalog.SellerHeadline)
        {
            AnalyticsMetricCatalog.Find(key).Should().NotBeNull("{0} is served to sellers", key);
        }
    }

    [Fact]
    public void Revenue_is_in_the_seller_set_and_served_as_not_measured()
    {
        // A seller told "not measured here" knows to open the wallet. One whose dashboard simply
        // has no revenue tile does not.
        AnalyticsMetricCatalog.SellerHeadline.Should().Contain(AnalyticsMetricCatalog.Revenue);

        var metric = AnalyticsMetricAssembler.Build(
            AnalyticsMetricCatalog.Revenue, 0, 0, baselineHasHistory: true);

        metric.IsAvailable.Should().BeFalse();
    }

    [Theory]
    [InlineData(AnalyticsMetricCatalog.Saves)]
    [InlineData(AnalyticsMetricCatalog.Shares)]
    [InlineData(AnalyticsMetricCatalog.Comments)]
    [InlineData(AnalyticsMetricCatalog.HelpfulVotes)]
    public void A_measured_metric_nothing_records_for_a_listing_is_left_out(string key)
    {
        // These are *measured* metrics, so they would be served as available — and nothing writes
        // them for marketplace content, so every seller would see a confident zero. Absent is not
        // none, and a tile that looks like none is worse than no tile.
        AnalyticsMetricCatalog.SellerHeadline.Should().NotContain(key);
    }

    [Fact]
    public void The_catalogue_version_moved_because_a_definition_was_added()
    {
        // Served beside every figure so a change in what a word means shows up as a version rather
        // than as a cliff in a chart.
        AnalyticsMetricCatalog.Version.Should().NotBe("1.0.0");
    }

    // ── Narrowing to marketplace ────────────────────────────────────────────

    [Fact]
    public async Task A_sellers_totals_leave_out_everything_that_is_not_a_listing()
    {
        var rollups = Substitute.For<IAnalyticsRollupRepository>();
        var events = Substitute.For<IAnalyticsEventRepository>();

        rollups.ListContentForOwnerAsync(Seller, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(
            [
                Row(AnalyticsContentType.Marketplace, Listing, views: 120, conversions: 3),
                Row(AnalyticsContentType.Marketplace, SecondListing, views: 30, conversions: 1),
                // A guide the same member wrote. Popular, and nothing to do with their listings.
                Row(AnalyticsContentType.Guide, Guide, views: 5000, conversions: 0),
            ]);

        var reads = new AnalyticsReadService(rollups, events);

        var totals = await reads.ReadOwnerContentTypeAsync(
            Seller, AnalyticsContentType.Marketplace, Period(), CancellationToken.None);

        totals.Counts.Views.Should().Be(150, "the guide's 5,000 views are not views of a listing");
        totals.Counts.Conversions.Should().Be(4);
    }

    [Fact]
    public async Task Unique_viewers_are_counted_across_listings_only_never_across_everything_owned()
    {
        var rollups = Substitute.For<IAnalyticsRollupRepository>();
        var events = Substitute.For<IAnalyticsEventRepository>();

        rollups.ListContentForOwnerAsync(Seller, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns([Row(AnalyticsContentType.Marketplace, Listing, views: 40, conversions: 1)]);

        events.CountDistinctViewersForOwnerContentTypeAsync(
                Seller, AnalyticsContentType.Marketplace,
                Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(7L);

        // The wide count, which includes the guide's readers. Wrong in the flattering direction.
        events.CountDistinctViewersForOwnerAsync(
                Seller, Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(900L);

        var reads = new AnalyticsReadService(rollups, events);

        var totals = await reads.ReadOwnerContentTypeAsync(
            Seller, AnalyticsContentType.Marketplace, Period(), CancellationToken.None);

        totals.UniqueViewers.Should().Be(7);

        await events.DidNotReceiveWithAnyArgs().CountDistinctViewersForOwnerAsync(
            default, default, default, default);
    }

    [Fact]
    public async Task A_seller_with_no_listing_activity_reports_no_data_rather_than_zeroes_with_history()
    {
        var rollups = Substitute.For<IAnalyticsRollupRepository>();
        var events = Substitute.For<IAnalyticsEventRepository>();

        rollups.ListContentForOwnerAsync(Seller, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns([Row(AnalyticsContentType.Guide, Guide, views: 80, conversions: 0)]);

        var reads = new AnalyticsReadService(rollups, events);

        var totals = await reads.ReadOwnerContentTypeAsync(
            Seller, AnalyticsContentType.Marketplace, Period(), CancellationToken.None);

        // `HasHistory` drives "insufficient history" on the comparison. A member who owns a guide and
        // no listings must not be compared as if their listings had a baseline of zero.
        totals.HasData.Should().BeFalse();
        totals.HasHistory.Should().BeFalse();
        totals.Counts.Views.Should().Be(0);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static AnalyticsCounts Counts(long views, long conversions) =>
        new(views, 0, 0, 0, 0, 0, conversions, 0, 0, 0, 0);

    private static ContentDailyRollup Row(
        AnalyticsContentType type, Guid contentId, long views, long conversions)
    {
        var row = ContentDailyRollup.Create(
            ContentDailyRollupId.New(), new DateOnly(2026, 9, 10), type, contentId, Seller);

        row.Replace(Counts(views, conversions), 0, Seller, "MU", new DateTimeOffset(2026, 9, 11, 2, 0, 0, TimeSpan.Zero));

        return row;
    }

    private static AnalyticsPeriod Period() =>
        AnalyticsPeriod.Resolve(
            AnalyticsPeriodPreset.Custom,
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 9, 12),
            TimeZoneInfo.Utc,
            new DateTimeOffset(2026, 9, 13, 0, 0, 0, TimeSpan.Zero),
            366).Period!;
}
