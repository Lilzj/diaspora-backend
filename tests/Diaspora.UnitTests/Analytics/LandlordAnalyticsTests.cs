using Diaspora.Modules.Analytics.Application.Services;
using Diaspora.Modules.Analytics.Contracts.V1.Responses;
using Diaspora.Modules.Analytics.Domain.Dimensions;
using Diaspora.Modules.Analytics.Domain.Enums;
using Diaspora.Modules.Analytics.Domain.Identifiers;
using Diaspora.Modules.Analytics.Domain.Periods;
using Diaspora.Modules.Analytics.Domain.Policies;
using Diaspora.Modules.Analytics.Domain.Repositories;
using Diaspora.Modules.Analytics.Domain.Rollups;
using Diaspora.Modules.Analytics.Options;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Diaspora.UnitTests.Analytics;

/// <summary>
/// A landlord's own housing figures — FRONTEND-PHASE15-GAP-013, 13 Sep 2026.
/// <para>
/// <b>Written at the traps the seller dashboard fell into.</b> Three ways to serve a confident wrong
/// number from "listing rollups summed to the landlord": count the landlord's marketplace listings
/// and guides as homes, key the figures by the landlord <em>profile</em> so nobody's dashboard ever
/// answers, and offer a revenue tile for money Diaspora never handles.
/// </para>
/// <para>
/// The composer is also the seller dashboard's now, so the seller's shape is pinned here too: sharing
/// an implementation is only safe if both callers are held to what they served before.
/// </para>
/// </summary>
public sealed class LandlordAnalyticsTests
{
    private static readonly Guid Member = Guid.Parse("6b0f2c7e-94d1-4a3b-8e25-1c7d9f0a3b58");
    private static readonly Guid Home = Guid.Parse("c3a9e1f7-2d64-4b8a-9f13-5e0b7d2c6a41");
    private static readonly Guid SecondHome = Guid.Parse("8d2b6f04-e1a7-4c95-b3d8-7a4e0c9f1b26");
    private static readonly Guid Listing = Guid.Parse("f41c8a2d-6b3e-4d07-a9c5-2e8b1d7f3a90");
    private static readonly Guid Guide = Guid.Parse("1e7d3b9a-5c2f-4a86-8b41-9d0c6e2a7f15");

    // ── The set ─────────────────────────────────────────────────────────────

    [Fact]
    public void Every_landlord_metric_resolves_against_the_catalogue()
    {
        foreach (var key in AnalyticsMetricCatalog.LandlordHeadline)
        {
            AnalyticsMetricCatalog.Find(key).Should().NotBeNull("{0} is served to landlords", key);
        }
    }

    [Fact]
    public void A_landlord_is_offered_no_revenue_tile()
    {
        // Rent is not taken through Diaspora. "Not measured here" would suggest it could be.
        AnalyticsMetricCatalog.LandlordHeadline.Should().NotContain(AnalyticsMetricCatalog.Revenue);
    }

    [Theory]
    [InlineData(AnalyticsMetricCatalog.Saves)]
    [InlineData(AnalyticsMetricCatalog.Shares)]
    [InlineData(AnalyticsMetricCatalog.Comments)]
    [InlineData(AnalyticsMetricCatalog.HelpfulVotes)]
    [InlineData(AnalyticsMetricCatalog.EngagementRate)]
    public void A_measured_metric_nothing_records_for_a_home_is_left_out(string key)
    {
        // Measured, so it would be served as available — and nothing writes it for housing content,
        // so every landlord would see a confident zero.
        AnalyticsMetricCatalog.LandlordHeadline.Should().NotContain(key);
    }

    [Fact]
    public void The_rate_says_what_it_divides_for_a_home()
    {
        AnalyticsMetricCatalog.Find(AnalyticsMetricCatalog.ConversionRate)!
            .Definition.Should().Contain("for a housing listing, viewings booked per view");

        AnalyticsMetricCatalog.Find(AnalyticsMetricCatalog.Conversion)!
            .Definition.Should().Contain("a viewing for a housing listing");
    }

    [Fact]
    public void The_catalogue_version_moved_because_a_definition_changed()
    {
        AnalyticsMetricCatalog.Version.Should().NotBe(
            "1.1.0", "the conversion rate's served definition changed wording to name housing");
    }

    // ── The read, narrowed to homes ─────────────────────────────────────────

    [Fact]
    public async Task A_landlords_totals_leave_out_everything_that_is_not_a_home()
    {
        var (reads, _, _) = Reads(
            Row(AnalyticsContentType.Housing, Home, views: 120, conversions: 3),
            Row(AnalyticsContentType.Housing, SecondHome, views: 30, conversions: 1),
            // The same member sells on the marketplace and writes guides. Neither is a home.
            Row(AnalyticsContentType.Marketplace, Listing, views: 5000, conversions: 40),
            Row(AnalyticsContentType.Guide, Guide, views: 900, conversions: 0));

        var totals = await reads.ReadOwnerContentTypeAsync(
            Member, AnalyticsContentType.Housing, Window(), CancellationToken.None);

        totals.Counts.Views.Should().Be(150);
        totals.Counts.Conversions.Should().Be(4, "viewings on homes, not sales of listings");
    }

    // ── The dataset ─────────────────────────────────────────────────────────

    [Fact]
    public async Task The_landlord_dataset_is_the_callers_homes_by_their_member_id()
    {
        var (_, rollups, events) = Reads(
            Row(AnalyticsContentType.Housing, Home, views: 40, conversions: 2),
            Row(AnalyticsContentType.Marketplace, Listing, views: 5000, conversions: 40));

        events.CountDistinctViewersForOwnerContentTypeAsync(
                Member, AnalyticsContentType.Housing,
                Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(9L);

        var response = await Compose(AnalyticsOwnerDatasetProfile.Landlord, rollups, events);

        response.Subject.Type.Should().Be("landlord");
        Value(response, "content.views").Should().Be(40, "the marketplace listing's views are not a home's");
        Value(response, "content.uniqueViewers").Should().Be(9);
        Value(response, "conversion.count").Should().Be(2);
        Value(response, "conversion.rate").Should().Be(5, "2 viewings ÷ 40 views × 100");

        // Read by the caller's user id — the only identity the composer has. Never a profile id.
        await rollups.Received().ListContentForOwnerAsync(
            Member, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task The_landlord_board_ranks_homes_with_viewings_beside_views()
    {
        var (_, rollups, events) = Reads(
            Row(AnalyticsContentType.Housing, Home, views: 12, conversions: 1),
            Row(AnalyticsContentType.Housing, SecondHome, views: 30, conversions: 0),
            Row(AnalyticsContentType.Marketplace, Listing, views: 5000, conversions: 40));

        var dimensions = Substitute.For<IAnalyticsContentDimensionRepository>();
        IReadOnlyList<AnalyticsContentDimension> titled =
        [
            AnalyticsContentDimension.Observe(
                AnalyticsContentDimensionId.New(), AnalyticsContentType.Housing, SecondHome, Member,
                DateTimeOffset.UtcNow, title: "Two-bed flat, Peckham")
        ];

        dimensions.ListAsync(
                Arg.Any<IReadOnlyCollection<(AnalyticsContentType, Guid)>>(), Arg.Any<CancellationToken>())
            .Returns(titled);

        var response = await Compose(AnalyticsOwnerDatasetProfile.Landlord, rollups, events, dimensions);

        var board = response.Leaderboards.Should().ContainSingle().Subject;

        board.Key.Should().Be("landlord-listings");
        board.Rows.Should().HaveCount(2, "the marketplace listing is not on a landlord's board");

        board.Rows[0].Entity.EntityId.Should().Be(SecondHome.ToString(), "most viewed first");
        board.Rows[0].Entity.Title.Should().Be("Two-bed flat, Peckham");
        board.Rows[0].Entity.EntityType.Should().Be("housing");
        board.Rows[0].Entity.Route.Should().Be($"/housing/{SecondHome}");

        board.Rows[1].Entity.Title.Should().Be("Home", "a home whose publication was never recorded");
        board.Rows[1].SecondaryValue.Should().Be(1);
        board.Rows[1].SecondaryLabel.Should().Be("Viewings");
    }

    [Fact]
    public async Task The_landlord_dataset_serves_and_withholds_no_revenue()
    {
        var (_, rollups, events) = Reads(Row(AnalyticsContentType.Housing, Home, views: 4, conversions: 0));

        var response = await Compose(AnalyticsOwnerDatasetProfile.Landlord, rollups, events);

        response.Metrics.Should().NotContain(metric => metric.Key == "revenue.gross");
        response.UnavailableMetrics.Should().NotContain("revenue.gross");
    }

    [Fact]
    public async Task The_seller_dataset_is_what_it_was_before_the_composer_was_shared()
    {
        var (_, rollups, events) = Reads(
            Row(AnalyticsContentType.Marketplace, Listing, views: 50, conversions: 5),
            Row(AnalyticsContentType.Housing, Home, views: 7000, conversions: 60));

        var response = await Compose(AnalyticsOwnerDatasetProfile.Seller, rollups, events);

        response.Subject.Type.Should().Be("seller");
        Value(response, "content.views").Should().Be(50, "a seller's homes are not their listings");
        Value(response, "conversion.rate").Should().Be(10);
        response.UnavailableMetrics.Should().Contain("revenue.gross", "a seller is told revenue is in the wallet");

        var board = response.Leaderboards.Should().ContainSingle().Subject;
        board.Key.Should().Be("seller-listings");
        board.Rows.Should().ContainSingle().Which.SecondaryLabel.Should().Be("Sales");
    }

    // ── What the dashboard calls them ───────────────────────────────────────

    [Fact]
    public async Task The_landlord_dashboard_calls_them_viewings()
    {
        // The catalogue's "Actions taken" serves every kind of content. Beside a landlord's homes
        // the action has a name.
        var (_, rollups, events) = Reads(Row(AnalyticsContentType.Housing, Home, views: 40, conversions: 2));

        var response = await Compose(AnalyticsOwnerDatasetProfile.Landlord, rollups, events);

        Label(response, "conversion.count").Should().Be("Viewings");
        Label(response, "conversion.rate").Should().Be("Viewing rate");

        // The same word on the tile and beside each home on the board.
        response.Leaderboards.Single().Rows.Single().SecondaryLabel.Should().Be("Viewings");
    }

    [Fact]
    public async Task A_withheld_viewing_rate_is_named_as_one()
    {
        var (_, rollups, events) = Reads(Row(AnalyticsContentType.Housing, Home, views: 0, conversions: 0));

        var response = await Compose(AnalyticsOwnerDatasetProfile.Landlord, rollups, events);

        response.Freshness.Limitations.Should().Contain(
            "No figure yet for Viewing rate: nothing was viewed in this period, so there is nothing to work it out from.");
        response.Freshness.Limitations.Should().NotContain(sentence => sentence.Contains("Conversion"));
    }

    [Fact]
    public async Task The_seller_dashboard_calls_them_sales()
    {
        var (_, rollups, events) = Reads(Row(AnalyticsContentType.Marketplace, Listing, views: 50, conversions: 5));

        var response = await Compose(AnalyticsOwnerDatasetProfile.Seller, rollups, events);

        Label(response, "conversion.count").Should().Be("Sales");
        Label(response, "conversion.rate").Should().Be("Conversion rate");
        response.Leaderboards.Single().Rows.Single().SecondaryLabel.Should().Be("Sales");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static string Label(AnalyticsDatasetResponse response, string catalogueKey) =>
        response.Metrics.Single(metric => metric.Key == catalogueKey).Label;

    private static ContentDailyRollup Row(
        AnalyticsContentType type, Guid contentId, long views, long conversions)
    {
        var row = ContentDailyRollup.Create(
            ContentDailyRollupId.New(), new DateOnly(2026, 9, 10), type, contentId, Member);

        row.Replace(
            new AnalyticsCounts(views, 0, 0, 0, 0, 0, conversions, 0, 0, 0),
            0,
            Member,
            "GB",
            new DateTimeOffset(2026, 9, 11, 2, 0, 0, TimeSpan.Zero));

        return row;
    }

    private static (AnalyticsReadService Reads, IAnalyticsRollupRepository Rollups, IAnalyticsEventRepository Events)
        Reads(params ContentDailyRollup[] rows)
    {
        var rollups = Substitute.For<IAnalyticsRollupRepository>();
        var events = Substitute.For<IAnalyticsEventRepository>();

        IReadOnlyList<ContentDailyRollup> all = rows;
        IReadOnlyList<ContentDailyRollup> none = [];

        // Every row sits on 10 Sep; a window that does not include that day sees none of them.
        rollups.ListContentForOwnerAsync(Member, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(call =>
                new DateOnly(2026, 9, 10) >= call.ArgAt<DateOnly>(1)
                && new DateOnly(2026, 9, 10) <= call.ArgAt<DateOnly>(2)
                    ? all
                    : none);

        return (new AnalyticsReadService(rollups, events), rollups, events);
    }

    private static AnalyticsPeriod Window() =>
        AnalyticsPeriod.Resolve(
            AnalyticsPeriodPreset.Custom,
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 9, 12),
            TimeZoneInfo.Utc,
            new DateTimeOffset(2026, 9, 13, 9, 0, 0, TimeSpan.Zero),
            366).Period!;

    private static async Task<AnalyticsDatasetResponse> Compose(
        AnalyticsOwnerDatasetProfile profile,
        IAnalyticsRollupRepository rollups,
        IAnalyticsEventRepository events,
        IAnalyticsContentDimensionRepository? dimensions = null)
    {
        dimensions ??= Substitute.For<IAnalyticsContentDimensionRepository>();

        var options = Microsoft.Extensions.Options.Options.Create(new AnalyticsOptions());
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(new DateTimeOffset(2026, 9, 13, 9, 0, 0, TimeSpan.Zero));

        var calendar = new AnalyticsReportingCalendar(clock, options);

        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.IsAuthenticated.Returns(true);
        currentUser.UserId.Returns(new UserId(Member));

        var composer = new AnalyticsOwnerDatasetComposer(
            new AnalyticsQueryContext(currentUser, dimensions, calendar, options),
            new AnalyticsReadService(rollups, events),
            new AnalyticsDatasetBuilder(calendar, options),
            dimensions);

        var result = await composer.ComposeAsync(
            profile, "custom", "2026-09-01", "2026-09-12", null, includeSeries: true, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.Code : string.Empty);

        return result.Value;
    }

    private static double Value(AnalyticsDatasetResponse response, string catalogueKey) =>
        response.Metrics.Single(metric => metric.Key == catalogueKey).Value.Value;
}
