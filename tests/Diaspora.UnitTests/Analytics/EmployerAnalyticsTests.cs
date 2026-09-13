using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Jobs.V1.Events;
using Diaspora.Modules.Analytics;
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
/// An employer's own hiring figures — FRONTEND-PHASE15-GAP-012, 13 Sep 2026.
/// <para>
/// <b>The landlord dashboard's traps, and one of its own.</b> The shared ones: count an employer's
/// listings, homes and guides as jobs; key the figures by the employer <em>profile</em> so nobody's
/// dashboard answers. The employer's own is the candidate funnel. Jobs publishes shortlists,
/// interviews and offers, and a dashboard that counted them would put "1 shortlisted" in front of an
/// employer who had two applicants and knows who both are. §19 asks for counts that cannot name
/// anybody; an application count is one, a shortlist count is not.
/// </para>
/// </summary>
public sealed class EmployerAnalyticsTests
{
    private static readonly Guid Member = Guid.Parse("2c8e4a1f-7b3d-4e96-a5c0-9d1f6b2e8a37");
    private static readonly Guid Job = Guid.Parse("b5d17e2a-3c49-4f8b-9e06-1a7c4d2f5b83");
    private static readonly Guid SecondJob = Guid.Parse("e9a3c6f1-5d28-4b7e-8c14-6f2b9d0a3e75");
    private static readonly Guid Listing = Guid.Parse("47f0b2d9-1e6c-4a53-b8d7-3c5e9a1f2b06");
    private static readonly Guid Home = Guid.Parse("d1c6e8b3-9a2f-4d75-a064-8b3e7f1c5d29");

    // ── The set ─────────────────────────────────────────────────────────────

    [Fact]
    public void Every_employer_metric_resolves_against_the_catalogue()
    {
        foreach (var key in AnalyticsMetricCatalog.EmployerHeadline)
        {
            AnalyticsMetricCatalog.Find(key).Should().NotBeNull("{0} is served to employers", key);
        }
    }

    [Fact]
    public void An_employer_is_offered_no_revenue_tile()
    {
        // What an employer pays Diaspora is spend. "Revenue is not measured here" would read as
        // money the employer earned.
        AnalyticsMetricCatalog.EmployerHeadline.Should().NotContain(AnalyticsMetricCatalog.Revenue);
    }

    [Theory]
    [InlineData(AnalyticsMetricCatalog.Saves)]
    [InlineData(AnalyticsMetricCatalog.Shares)]
    [InlineData(AnalyticsMetricCatalog.Comments)]
    [InlineData(AnalyticsMetricCatalog.HelpfulVotes)]
    [InlineData(AnalyticsMetricCatalog.EngagementRate)]
    public void A_measured_metric_nothing_records_for_a_job_is_left_out(string key)
    {
        // Saving a job writes to the Bookmarks module, which publishes nothing analytics reads.
        AnalyticsMetricCatalog.EmployerHeadline.Should().NotContain(key);
    }

    [Fact]
    public void The_rate_says_what_it_divides_for_a_job()
    {
        AnalyticsMetricCatalog.Find(AnalyticsMetricCatalog.ConversionRate)!
            .Definition.Should().Contain("for a job, applications per view");

        AnalyticsMetricCatalog.Find(AnalyticsMetricCatalog.Conversion)!
            .Definition.Should().Contain("an application for a job");
    }

    [Fact]
    public void The_catalogue_version_moved_again_because_the_rate_definition_changed_again()
    {
        AnalyticsMetricCatalog.Version.Should().NotBe("1.2.0", "the rate's served definition now names jobs");
    }

    [Theory]
    [InlineData(typeof(CandidateShortlistedIntegrationEvent))]
    [InlineData(typeof(InterviewScheduledIntegrationEvent))]
    [InlineData(typeof(OfferExtendedIntegrationEvent))]
    [InlineData(typeof(OfferAcceptedIntegrationEvent))]
    [InlineData(typeof(ApplicationStatusChangedIntegrationEvent))]
    public void Analytics_counts_no_hiring_stage_after_the_application(Type stage)
    {
        // A consumer for any of these would make an employer's figures a candidate funnel, where a
        // count of one is a named person. Adding one is a privacy decision, not a metric.
        var handled = typeof(AnalyticsModule).Assembly
            .GetTypes()
            .Select(type => type.BaseType)
            .Where(baseType => baseType is { IsGenericType: true }
                && baseType.GetGenericTypeDefinition() == typeof(IntegrationEventHandler<>))
            .Select(baseType => baseType!.GetGenericArguments()[0])
            .ToList();

        handled.Should().Contain(typeof(ApplicationSubmittedIntegrationEvent), "the scan must see real consumers");
        handled.Should().NotContain(stage);
    }

    // ── The read, narrowed to jobs ──────────────────────────────────────────

    [Fact]
    public async Task An_employers_totals_leave_out_everything_that_is_not_a_job()
    {
        var (reads, _, _) = Reads(
            Row(AnalyticsContentType.Job, Job, views: 90, conversions: 6),
            Row(AnalyticsContentType.Job, SecondJob, views: 10, conversions: 1),
            // The same member sells on the marketplace and lets a flat. Neither is a job.
            Row(AnalyticsContentType.Marketplace, Listing, views: 5000, conversions: 40),
            Row(AnalyticsContentType.Housing, Home, views: 700, conversions: 12));

        var totals = await reads.ReadOwnerContentTypeAsync(
            Member, AnalyticsContentType.Job, Window(), CancellationToken.None);

        totals.Counts.Views.Should().Be(100);
        totals.Counts.Conversions.Should().Be(7, "applications to jobs, not sales or viewings");
    }

    // ── The dataset ─────────────────────────────────────────────────────────

    [Fact]
    public async Task The_employer_dataset_is_the_callers_jobs_by_their_member_id()
    {
        var (_, rollups, events) = Reads(
            Row(AnalyticsContentType.Job, Job, views: 80, conversions: 4),
            Row(AnalyticsContentType.Housing, Home, views: 700, conversions: 12));

        events.CountDistinctViewersForOwnerContentTypeAsync(
                Member, AnalyticsContentType.Job,
                Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(31L);

        var response = await Compose(AnalyticsOwnerDatasetProfile.Employer, rollups, events);

        response.Subject.Type.Should().Be("employer");
        Value(response, "content.views").Should().Be(80, "the home's views are not a job's");
        Value(response, "content.uniqueViewers").Should().Be(31);
        Value(response, "conversion.count").Should().Be(4);
        Value(response, "conversion.rate").Should().Be(5, "4 applications ÷ 80 views × 100");

        await rollups.Received().ListContentForOwnerAsync(
            Member, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task The_employer_board_ranks_jobs_with_applications_beside_views()
    {
        var (_, rollups, events) = Reads(
            Row(AnalyticsContentType.Job, Job, views: 15, conversions: 3),
            Row(AnalyticsContentType.Job, SecondJob, views: 60, conversions: 2),
            Row(AnalyticsContentType.Housing, Home, views: 700, conversions: 12));

        var dimensions = Substitute.For<IAnalyticsContentDimensionRepository>();
        IReadOnlyList<AnalyticsContentDimension> titled =
        [
            AnalyticsContentDimension.Observe(
                AnalyticsContentDimensionId.New(), AnalyticsContentType.Job, SecondJob, Member,
                DateTimeOffset.UtcNow, title: "Community nurse, night shifts")
        ];

        dimensions.ListAsync(
                Arg.Any<IReadOnlyCollection<(AnalyticsContentType, Guid)>>(), Arg.Any<CancellationToken>())
            .Returns(titled);

        var response = await Compose(AnalyticsOwnerDatasetProfile.Employer, rollups, events, dimensions);

        var board = response.Leaderboards.Should().ContainSingle().Subject;

        board.Key.Should().Be("employer-jobs");
        board.Label.Should().Be("Top jobs");
        board.Rows.Should().HaveCount(2, "the home is not on an employer's board");

        board.Rows[0].Entity.EntityId.Should().Be(SecondJob.ToString(), "most viewed first");
        board.Rows[0].Entity.Title.Should().Be("Community nurse, night shifts");
        board.Rows[0].Entity.EntityType.Should().Be("job");
        board.Rows[0].Entity.Route.Should().Be($"/job/{SecondJob}");

        board.Rows[1].Entity.Title.Should().Be("Job", "a job whose publication was never recorded");
        board.Rows[1].SecondaryValue.Should().Be(3);
        board.Rows[1].SecondaryLabel.Should().Be("Applications");
    }

    [Fact]
    public async Task The_employer_dataset_serves_and_withholds_no_revenue()
    {
        var (_, rollups, events) = Reads(Row(AnalyticsContentType.Job, Job, views: 4, conversions: 0));

        var response = await Compose(AnalyticsOwnerDatasetProfile.Employer, rollups, events);

        response.Metrics.Should().NotContain(metric => metric.Key == "revenue.gross");
        response.UnavailableMetrics.Should().NotContain("revenue.gross");
    }

    // ── What the dashboard calls them ───────────────────────────────────────

    [Fact]
    public async Task The_employer_dashboard_calls_them_applications()
    {
        var (_, rollups, events) = Reads(Row(AnalyticsContentType.Job, Job, views: 80, conversions: 4));

        var response = await Compose(AnalyticsOwnerDatasetProfile.Employer, rollups, events);

        Label(response, "conversion.count").Should().Be("Applications");
        Label(response, "conversion.rate").Should().Be("Application rate");
        response.Leaderboards.Single().Rows.Single().SecondaryLabel.Should().Be("Applications");
    }

    [Fact]
    public async Task A_withheld_application_rate_is_named_as_one()
    {
        var (_, rollups, events) = Reads(Row(AnalyticsContentType.Job, Job, views: 0, conversions: 0));

        var response = await Compose(AnalyticsOwnerDatasetProfile.Employer, rollups, events);

        response.Freshness.Limitations.Should().Contain(
            "No figure yet for Application rate: nothing was viewed in this period, so there is nothing to work it out from.");
    }

    [Fact]
    public async Task Only_the_label_changes()
    {
        // The key the client switches on, the served definition and every other tile's label stay
        // the catalogue's. Renaming the key would drop the tile on a client that does not know it.
        var (_, rollups, events) = Reads(Row(AnalyticsContentType.Job, Job, views: 80, conversions: 4));

        var response = await Compose(AnalyticsOwnerDatasetProfile.Employer, rollups, events);

        response.Metrics.Select(metric => metric.Key).Should().Equal(
            "content.views", "content.uniqueViewers", "conversion.count", "conversion.rate", "content.published");

        var published = response.Metrics.Single(metric => metric.Key == "content.published");
        published.Label.Should().Be(AnalyticsMetricCatalog.Find(AnalyticsMetricCatalog.ContentPublished)!.Label);
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
            new AnalyticsCounts(views, 0, 0, 0, 0, 0, conversions, 0, 0, 0, 0),
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
