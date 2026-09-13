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
/// A service provider's own figures — FRONTEND-PHASE15-GAP-010, 13 Sep 2026.
/// <para>
/// <b>The other owner dashboards' traps, and one of its own.</b> The shared one: count a provider's
/// listings, homes and jobs as provider profiles. The provider's own is the second column. Every other
/// owner board puts actions taken beside views, and a provider sells nothing through their profile, so
/// that column would read zero on every row of every provider's board. Somebody writing to a provider
/// is what the profile is for, and it is what the board counts.
/// </para>
/// </summary>
public sealed class ProviderAnalyticsTests
{
    private static readonly Guid Member = Guid.Parse("6a1d4c9e-2f7b-4e38-b5a0-8c3f1d6e9b24");
    private static readonly Guid Profile = Guid.Parse("c4e8a2d6-7b1f-4a93-8e05-2d9b6f3c1a57");
    private static readonly Guid SecondProfile = Guid.Parse("1f9b3e7a-5c2d-4b86-a0e4-7d6c8b2a9f13");
    private static readonly Guid Listing = Guid.Parse("47f0b2d9-1e6c-4a53-b8d7-3c5e9a1f2b06");
    private static readonly Guid Job = Guid.Parse("b5d17e2a-3c49-4f8b-9e06-1a7c4d2f5b83");

    // ── The set ─────────────────────────────────────────────────────────────

    [Fact]
    public void Every_provider_metric_resolves_against_the_catalogue()
    {
        foreach (var key in AnalyticsMetricCatalog.ProviderHeadline)
        {
            AnalyticsMetricCatalog.Find(key).Should().NotBeNull("{0} is served to providers", key);
        }
    }

    [Theory]
    [InlineData(AnalyticsMetricCatalog.Conversion)]
    [InlineData(AnalyticsMetricCatalog.ConversionRate)]
    [InlineData(AnalyticsMetricCatalog.Revenue)]
    public void A_provider_is_offered_no_action_no_rate_and_no_revenue(string key)
    {
        // A message is an enquiry, not the action a conversion counts, so an action count would be a
        // permanent zero and its rate a permanent absence; a provider takes no payment through their
        // profile.
        AnalyticsMetricCatalog.ProviderHeadline.Should().NotContain(key);
    }

    [Theory]
    [InlineData(AnalyticsMetricCatalog.Shares)]
    [InlineData(AnalyticsMetricCatalog.Comments)]
    [InlineData(AnalyticsMetricCatalog.HelpfulVotes)]
    public void A_measured_metric_nothing_records_for_a_provider_is_left_out(string key)
    {
        // The provider screen's share button opens the device's share sheet and records nothing, and a
        // provider cannot be commented on or voted on.
        AnalyticsMetricCatalog.ProviderHeadline.Should().NotContain(key);
    }

    [Theory]
    [InlineData(AnalyticsMetricCatalog.Enquiries)]
    [InlineData(AnalyticsMetricCatalog.Saves)]
    public void A_providers_enquiries_and_saves_are_on_their_dashboard_and_on_each_profiles_row(string key)
    {
        // Writing to a provider goes through Conversations and saving one through Community; both publish.
        AnalyticsMetricCatalog.ProviderHeadline.Should().Contain(key);
        AnalyticsMetricCatalog.ContentItemFor(AnalyticsContentType.Provider).Should().Contain(key);
    }

    [Fact]
    public void Every_owner_board_counts_one_of_its_own_tiles()
    {
        foreach (var profile in Profiles())
        {
            profile.Headline.Should().Contain(
                profile.BoardSecondary, "the {0} board's second column must be a figure on that dashboard", profile.SubjectType);

            profile.Invoking(entry => entry.BoardSecondaryOf(AnalyticsCounts.Zero)).Should().NotThrow();
        }
    }

    [Fact]
    public void Only_the_provider_board_counts_enquiries()
    {
        AnalyticsOwnerDatasetProfile.Provider.BoardSecondary.Should().Be(AnalyticsMetricCatalog.Enquiries);

        foreach (var profile in Profiles().Where(entry => entry != AnalyticsOwnerDatasetProfile.Provider))
        {
            profile.BoardSecondary.Should().Be(AnalyticsMetricCatalog.Conversion, "{0} sells, lets or hires", profile.SubjectType);
        }
    }

    // ── The dataset ─────────────────────────────────────────────────────────

    [Fact]
    public async Task The_provider_dataset_is_the_callers_profiles_by_their_member_id()
    {
        var (rollups, events) = Reads(
            Row(AnalyticsContentType.Provider, Profile, views: 70, saves: 6, enquiries: 4),
            Row(AnalyticsContentType.Provider, SecondProfile, views: 10, saves: 1, enquiries: 1),
            // The same member sells on the marketplace and hires. Neither is a provider profile.
            Row(AnalyticsContentType.Marketplace, Listing, views: 5000, conversions: 40, enquiries: 30),
            Row(AnalyticsContentType.Job, Job, views: 700, conversions: 12));

        events.CountDistinctViewersForOwnerContentTypeAsync(
                Member, AnalyticsContentType.Provider,
                Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(29L);

        var response = await Compose(rollups, events);

        response.Subject.Type.Should().Be("provider");
        response.Subject.Id.Should().BeNull("the dataset is the caller's, and names nobody");

        Value(response, "content.views").Should().Be(80, "the listing's and the job's views are not a provider's");
        Value(response, "content.uniqueViewers").Should().Be(29);
        Value(response, "content.enquiries").Should().Be(5, "the listing's enquiries are the seller's");
        Value(response, "content.saves").Should().Be(7);

        response.Metrics.Select(metric => metric.Key).Should().Equal(
            "content.views", "content.uniqueViewers", "content.enquiries", "content.saves", "content.published");

        await rollups.Received().ListContentForOwnerAsync(
            Member, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task The_provider_board_ranks_profiles_with_enquiries_beside_views()
    {
        var (rollups, events) = Reads(
            Row(AnalyticsContentType.Provider, Profile, views: 15, saves: 8, conversions: 9, enquiries: 3),
            Row(AnalyticsContentType.Provider, SecondProfile, views: 60, saves: 5, enquiries: 2),
            Row(AnalyticsContentType.Job, Job, views: 700, conversions: 12));

        var dimensions = Substitute.For<IAnalyticsContentDimensionRepository>();
        IReadOnlyList<AnalyticsContentDimension> titled =
        [
            AnalyticsContentDimension.Observe(
                AnalyticsContentDimensionId.New(), AnalyticsContentType.Provider, SecondProfile, Member,
                DateTimeOffset.UtcNow, title: "Harbour Plumbing")
        ];

        dimensions.ListAsync(
                Arg.Any<IReadOnlyCollection<(AnalyticsContentType, Guid)>>(), Arg.Any<CancellationToken>())
            .Returns(titled);

        var response = await Compose(rollups, events, dimensions);

        var board = response.Leaderboards.Should().ContainSingle().Subject;

        board.Key.Should().Be("provider-profiles");
        board.Label.Should().Be("Top profiles");
        board.Rows.Should().HaveCount(2, "the job is not on a provider's board");

        board.Rows[0].Entity.EntityId.Should().Be(SecondProfile.ToString(), "most viewed first");
        board.Rows[0].Entity.Title.Should().Be("Harbour Plumbing");
        board.Rows[0].Entity.EntityType.Should().Be("provider");
        board.Rows[0].Entity.Route.Should().Be($"/provider/{SecondProfile}");
        board.Rows[0].SecondaryValue.Should().Be(2);
        board.Rows[0].SecondaryLabel.Should().Be("Enquiries");

        board.Rows[1].Entity.Title.Should().Be("Provider profile", "a profile whose publication was never recorded");
        board.Rows[1].SecondaryValue.Should().Be(3, "enquiries — not saves, and not conversions a provider never records");
    }

    [Fact]
    public async Task A_profile_written_to_but_not_opened_in_the_period_is_still_on_the_board()
    {
        // A member can open a profile on the last day of one period and write on the first day of the
        // next. Dropping the row would hide the only thing that happened to that profile.
        var (rollups, events) = Reads(Row(AnalyticsContentType.Provider, Profile, views: 0, enquiries: 2));

        var response = await Compose(rollups, events);

        var row = response.Leaderboards.Should().ContainSingle().Subject.Rows.Should().ContainSingle().Subject;
        row.Value.Should().Be(0);
        row.SecondaryValue.Should().Be(2);
    }

    [Fact]
    public async Task The_provider_dataset_serves_and_withholds_no_revenue_and_no_action()
    {
        var (rollups, events) = Reads(Row(AnalyticsContentType.Provider, Profile, views: 4));

        var response = await Compose(rollups, events);

        foreach (var key in new[] { "revenue.gross", "conversion.count", "conversion.rate" })
        {
            response.Metrics.Should().NotContain(metric => metric.Key == key);
            response.UnavailableMetrics.Should().NotContain(key, "a withheld tile would still promise the figure");
        }
    }

    [Fact]
    public async Task The_employer_board_still_counts_applications_and_not_enquiries()
    {
        // The second column moved for providers only. A job written about nine times and applied for
        // once is an employer's "Applications 1".
        var (rollups, events) = Reads(Row(AnalyticsContentType.Job, Job, views: 20, conversions: 1, enquiries: 9));

        var response = await Compose(rollups, events, profile: AnalyticsOwnerDatasetProfile.Employer);

        var row = response.Leaderboards.Single().Rows.Single();
        row.SecondaryValue.Should().Be(1);
        row.SecondaryLabel.Should().Be("Applications");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static AnalyticsOwnerDatasetProfile[] Profiles() =>
    [
        AnalyticsOwnerDatasetProfile.Seller,
        AnalyticsOwnerDatasetProfile.Landlord,
        AnalyticsOwnerDatasetProfile.Employer,
        AnalyticsOwnerDatasetProfile.Provider
    ];

    private static ContentDailyRollup Row(
        AnalyticsContentType type,
        Guid contentId,
        long views,
        long saves = 0,
        long conversions = 0,
        long enquiries = 0)
    {
        var row = ContentDailyRollup.Create(
            ContentDailyRollupId.New(), new DateOnly(2026, 9, 10), type, contentId, Member);

        row.Replace(
            new AnalyticsCounts(views, saves, 0, 0, 0, 0, conversions, 0, 0, 0, enquiries),
            0,
            Member,
            "GB",
            new DateTimeOffset(2026, 9, 11, 2, 0, 0, TimeSpan.Zero));

        return row;
    }

    private static (IAnalyticsRollupRepository Rollups, IAnalyticsEventRepository Events)
        Reads(params ContentDailyRollup[] rows)
    {
        var rollups = Substitute.For<IAnalyticsRollupRepository>();
        var events = Substitute.For<IAnalyticsEventRepository>();

        IReadOnlyList<ContentDailyRollup> all = rows;
        IReadOnlyList<ContentDailyRollup> none = [];

        rollups.ListContentForOwnerAsync(Member, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(call =>
                new DateOnly(2026, 9, 10) >= call.ArgAt<DateOnly>(1)
                && new DateOnly(2026, 9, 10) <= call.ArgAt<DateOnly>(2)
                    ? all
                    : none);

        return (rollups, events);
    }

    private static async Task<AnalyticsDatasetResponse> Compose(
        IAnalyticsRollupRepository rollups,
        IAnalyticsEventRepository events,
        IAnalyticsContentDimensionRepository? dimensions = null,
        AnalyticsOwnerDatasetProfile? profile = null)
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
            profile ?? AnalyticsOwnerDatasetProfile.Provider,
            "custom",
            "2026-09-01",
            "2026-09-12",
            null,
            includeSeries: true,
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.Code : string.Empty);

        return result.Value;
    }

    private static double Value(AnalyticsDatasetResponse response, string catalogueKey) =>
        response.Metrics.Single(metric => metric.Key == catalogueKey).Value.Value;
}
