using Diaspora.Modules.Analytics.Application.Services;
using Diaspora.Modules.Analytics.Contracts.V1.Responses;
using Diaspora.Modules.Analytics.Domain.Enums;
using Diaspora.Modules.Analytics.Domain.Identifiers;
using Diaspora.Modules.Analytics.Domain.Periods;
using Diaspora.Modules.Analytics.Domain.Policies;
using Diaspora.Modules.Analytics.Domain.Repositories;
using Diaspora.Modules.Analytics.Domain.Rollups;
using Diaspora.Modules.Analytics.Options;
using Diaspora.Platform.Services.Abstractions;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Diaspora.UnitTests.Analytics;

/// <summary>
/// "Not enough history yet", and when it is true — 13 Sep 2026.
/// <para>
/// <b>Found on the device.</b> Aisha's seller dashboard for "Today" read "Not enough history yet" on
/// every tile. She had published two listings a fortnight earlier and had been viewed since; the day
/// before was quiet. The projection writes a row only for a day on which something happened, so the
/// comparison period had no rows — and "no rows in the comparison period" was the whole of the test
/// for missing history.
/// </para>
/// <para>
/// The client already words a comparison period with nothing in it differently — "Nothing to compare
/// against" — and never received the reason that would show it. These tests sit where the reason is
/// decided: the read, which must ask what came before the window and nothing after it, and the
/// assembler, which must use that answer rather than the window's.
/// </para>
/// </summary>
public sealed class AnalyticsComparisonHistoryTests
{
    private static readonly Guid Owner = Guid.Parse("5d1c0a4e-2b7f-4e8a-9c31-6f0e2d8b7a14");
    private static readonly Guid Item = Guid.Parse("a7e3b9c2-41d8-4f6e-8b05-3c9d1e7f2a60");
    private static readonly Guid OtherItem = Guid.Parse("e2f8a1d4-9b3c-4c7e-a6d2-0f5b8e3c1d97");

    private static readonly DateOnly Today = new(2026, 9, 13);
    private static readonly DateOnly Yesterday = Today.AddDays(-1);
    private static readonly DateOnly AFortnightAgo = Today.AddDays(-14);

    // ── The reason ──────────────────────────────────────────────────────────

    [Fact]
    public void A_quiet_comparison_period_after_earlier_activity_is_a_zero_baseline()
    {
        var metrics = AnalyticsMetricAssembler.BuildSet(
            AnalyticsMetricCatalog.SellerHeadline,
            Totals(views: 3, hasData: true, hasHistory: true),
            Totals(views: 0, hasData: false, hasHistory: true));

        var views = metrics.Single(metric => metric.Key == AnalyticsMetricCatalog.ContentViews);

        views.ComparisonUnavailableReason.Should().Be("zeroBaseline");
        views.PreviousValue.Should().Be(0, "the comparison period is real, and nothing happened in it");
        views.ChangePercentage.Should().BeNull("a rise from nothing is not a percentage");
    }

    [Fact]
    public void No_tile_tells_a_subject_with_history_that_it_has_none()
    {
        // The device's exact case: nothing today, nothing yesterday, a fortnight of activity before.
        var metrics = AnalyticsMetricAssembler.BuildSet(
            AnalyticsMetricCatalog.SellerHeadline,
            Totals(views: 0, hasData: false, hasHistory: true),
            Totals(views: 0, hasData: false, hasHistory: true));

        metrics.Should().NotContain(metric => metric.ComparisonUnavailableReason == "insufficientHistory");

        metrics.Where(metric => metric.IsAvailable)
            .Should().NotBeEmpty()
            .And.OnlyContain(metric => metric.ComparisonUnavailableReason == "zeroBaseline");
    }

    [Fact]
    public void A_subject_with_nothing_recorded_by_the_end_of_the_comparison_period_still_lacks_history()
    {
        // The half of the old rule that was true. It must survive the fix.
        var metrics = AnalyticsMetricAssembler.BuildSet(
            AnalyticsMetricCatalog.SellerHeadline,
            Totals(views: 3, hasData: true, hasHistory: true),
            Totals(views: 0, hasData: false, hasHistory: false));

        var views = metrics.Single(metric => metric.Key == AnalyticsMetricCatalog.ContentViews);

        views.ComparisonUnavailableReason.Should().Be("insufficientHistory");
        views.PreviousValue.Should().BeNull("there was no comparison period for this subject to have a value in");
    }

    [Fact]
    public void A_window_with_rows_has_history_whatever_the_caller_passed()
    {
        new AnalyticsWindowTotals(AnalyticsCounts.Zero, 0, 0, HasData: true, HasHistory: false)
            .HasHistory.Should().BeTrue("a row in the window is history");
    }

    [Fact]
    public void The_reason_reaches_the_dataset_the_dashboards_render()
    {
        var response = Dataset(
            Totals(views: 0, hasData: false, hasHistory: true),
            Totals(views: 0, hasData: false, hasHistory: true));

        var views = response.Metrics.Single(metric => metric.Key == "content.views");

        views.Comparison.Should().NotBeNull();
        views.Comparison!.UnavailableReason.Should().Be("zeroBaseline");
        views.Comparison.BaselineValue.Should().Be(0);
    }

    // ── The owner read ──────────────────────────────────────────────────────

    [Fact]
    public async Task An_owner_active_a_fortnight_ago_has_history_on_a_quiet_day()
    {
        var reads = Reads(OwnerRowsOn(AFortnightAgo));

        var totals = await reads.ReadOwnerAsync(Owner, Window(Yesterday, Yesterday), CancellationToken.None);

        totals.HasData.Should().BeFalse("nothing happened yesterday");
        totals.HasHistory.Should().BeTrue("something happened a fortnight before it");
    }

    [Fact]
    public async Task Activity_after_the_comparison_period_is_not_history_for_it()
    {
        // A member whose first activity was today. Yesterday was not quiet for them; it was before
        // them. A check that asked "any row at all" would find today's and say otherwise.
        var reads = Reads(OwnerRowsOn(Today));

        var totals = await reads.ReadOwnerAsync(Owner, Window(Yesterday, Yesterday), CancellationToken.None);

        totals.HasHistory.Should().BeFalse();
    }

    [Fact]
    public async Task A_window_with_rows_does_not_ask_what_came_before_it()
    {
        var rollups = OwnerRowsOn(Yesterday);
        var reads = Reads(rollups);

        var totals = await reads.ReadOwnerAsync(Owner, Window(Yesterday, Yesterday), CancellationToken.None);

        totals.HasData.Should().BeTrue();
        totals.HasHistory.Should().BeTrue();

        await rollups.DidNotReceiveWithAnyArgs().AnyOwnerBeforeAsync(default, default, default);
    }

    // ── The narrowed read ───────────────────────────────────────────────────

    [Fact]
    public async Task A_guide_read_a_fortnight_ago_is_no_history_for_a_members_listings()
    {
        var reads = Reads(ContentRowsOn(AFortnightAgo, AnalyticsContentType.Guide, Item));

        var listings = await reads.ReadOwnerContentTypeAsync(
            Owner, AnalyticsContentType.Marketplace, Window(Yesterday, Yesterday), CancellationToken.None);

        listings.HasHistory.Should().BeFalse("the member's listings have never been recorded");
    }

    [Fact]
    public async Task The_same_guide_is_history_for_the_members_guides()
    {
        // The other side of the narrowing, so the test above cannot pass by answering false always.
        var reads = Reads(ContentRowsOn(AFortnightAgo, AnalyticsContentType.Guide, Item));

        var guides = await reads.ReadOwnerContentTypeAsync(
            Owner, AnalyticsContentType.Guide, Window(Yesterday, Yesterday), CancellationToken.None);

        guides.HasData.Should().BeFalse();
        guides.HasHistory.Should().BeTrue();
    }

    // ── The item read ───────────────────────────────────────────────────────

    [Fact]
    public async Task An_item_viewed_a_fortnight_ago_has_history_and_another_item_does_not()
    {
        var reads = Reads(ContentRowsOn(AFortnightAgo, AnalyticsContentType.Marketplace, Item));

        var viewed = await reads.ReadContentAsync(
            AnalyticsContentType.Marketplace, Item, Window(Yesterday, Yesterday), CancellationToken.None);

        var other = await reads.ReadContentAsync(
            AnalyticsContentType.Marketplace, OtherItem, Window(Yesterday, Yesterday), CancellationToken.None);

        viewed.HasHistory.Should().BeTrue();
        other.HasHistory.Should().BeFalse();
    }

    [Fact]
    public async Task The_per_item_rows_learn_which_items_were_recorded_before_the_window_in_one_read()
    {
        var reads = Reads(ContentRowsOn(AFortnightAgo, AnalyticsContentType.Marketplace, Item));

        var recorded = await reads.ReadOwnerContentRecordedBeforeAsync(Owner, Yesterday, CancellationToken.None);
        var beforeThem = await reads.ReadOwnerContentRecordedBeforeAsync(Owner, AFortnightAgo, CancellationToken.None);

        recorded.Should().Contain((AnalyticsContentType.Marketplace, Item));
        recorded.Should().NotContain((AnalyticsContentType.Marketplace, OtherItem));
        beforeThem.Should().BeEmpty("nothing was recorded before the fortnight began");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static AnalyticsWindowTotals Totals(long views, bool hasData, bool hasHistory) =>
        new(new AnalyticsCounts(views, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0), views > 0 ? 1 : 0, 0, hasData, hasHistory);

    private static AnalyticsReadService Reads(IAnalyticsRollupRepository rollups) =>
        new(rollups, Substitute.For<IAnalyticsEventRepository>());

    /// <summary>A rollup table holding one owner row, on one day, answering every read truthfully.</summary>
    private static IAnalyticsRollupRepository OwnerRowsOn(DateOnly day)
    {
        var rollups = Substitute.For<IAnalyticsRollupRepository>();

        IReadOnlyList<OwnerDailyRollup> one = [OwnerDailyRollup.Create(OwnerDailyRollupId.New(), day, Owner)];
        IReadOnlyList<OwnerDailyRollup> none = [];

        rollups.ListOwnerAsync(Owner, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(call => day >= call.ArgAt<DateOnly>(1) && day <= call.ArgAt<DateOnly>(2) ? one : none);

        rollups.AnyOwnerBeforeAsync(Owner, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(call => day < call.ArgAt<DateOnly>(1));

        return rollups;
    }

    /// <summary>A rollup table holding one content row, on one day, answering every read truthfully.</summary>
    private static IAnalyticsRollupRepository ContentRowsOn(
        DateOnly day, AnalyticsContentType type, Guid contentId)
    {
        var rollups = Substitute.For<IAnalyticsRollupRepository>();

        IReadOnlyList<ContentDailyRollup> one =
            [ContentDailyRollup.Create(ContentDailyRollupId.New(), day, type, contentId, Owner)];
        IReadOnlyList<ContentDailyRollup> none = [];

        IReadOnlyList<(AnalyticsContentType, Guid)> itsKey = [(type, contentId)];
        IReadOnlyList<(AnalyticsContentType, Guid)> noKeys = [];

        rollups.ListContentForOwnerAsync(Owner, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(call => day >= call.ArgAt<DateOnly>(1) && day <= call.ArgAt<DateOnly>(2) ? one : none);

        rollups.ListContentAsync(
                Arg.Any<AnalyticsContentType>(), Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
                call.ArgAt<AnalyticsContentType>(0) == type
                && call.ArgAt<Guid>(1) == contentId
                && day >= call.ArgAt<DateOnly>(2)
                && day <= call.ArgAt<DateOnly>(3)
                    ? one
                    : none);

        // The owner-wide answer includes this row whatever its type — which is exactly why the
        // narrowed read must not use it.
        rollups.AnyOwnerBeforeAsync(Owner, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(call => day < call.ArgAt<DateOnly>(1));

        rollups.AnyOwnerContentTypeBeforeAsync(
                Owner, Arg.Any<AnalyticsContentType>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<AnalyticsContentType>(1) == type && day < call.ArgAt<DateOnly>(2));

        rollups.AnyContentBeforeAsync(
                Arg.Any<AnalyticsContentType>(), Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(call =>
                call.ArgAt<AnalyticsContentType>(0) == type
                && call.ArgAt<Guid>(1) == contentId
                && day < call.ArgAt<DateOnly>(2));

        rollups.ListContentRecordedBeforeAsync(Owner, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(call => day < call.ArgAt<DateOnly>(1) ? itsKey : noKeys);

        return rollups;
    }

    private static AnalyticsPeriod Window(DateOnly from, DateOnly to) =>
        AnalyticsPeriod.Resolve(
            AnalyticsPeriodPreset.Custom,
            from,
            to,
            TimeZoneInfo.Utc,
            new DateTimeOffset(2026, 9, 13, 9, 0, 0, TimeSpan.Zero),
            366).Period!;

    private static AnalyticsDatasetResponse Dataset(AnalyticsWindowTotals current, AnalyticsWindowTotals baseline)
    {
        var options = Microsoft.Extensions.Options.Options.Create(new AnalyticsOptions());
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(new DateTimeOffset(2026, 9, 13, 9, 0, 0, TimeSpan.Zero));

        var builder = new AnalyticsDatasetBuilder(new AnalyticsReportingCalendar(clock, options), options);
        var period = Window(Today, Today);

        return builder.Build(
            new AnalyticsSubjectResponse { Type = "seller", Id = null, Label = null },
            period,
            period.Previous(),
            AnalyticsMetricAssembler.BuildSet(AnalyticsMetricCatalog.SellerHeadline, current, baseline),
            [],
            null,
            []);
    }
}
