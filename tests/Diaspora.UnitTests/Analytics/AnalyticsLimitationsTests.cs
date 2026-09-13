using Diaspora.Modules.Analytics.Application.Services;
using Diaspora.Modules.Analytics.Contracts.V1.Responses;
using Diaspora.Modules.Analytics.Domain.Enums;
using Diaspora.Modules.Analytics.Domain.Periods;
using Diaspora.Modules.Analytics.Domain.Policies;
using Diaspora.Modules.Analytics.Domain.Rollups;
using Diaspora.Modules.Analytics.Options;
using Diaspora.Platform.Services.Abstractions;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Diaspora.UnitTests.Analytics;

/// <summary>
/// What a dataset says it could not answer — 13 Sep 2026.
/// <para>
/// <b>Found on the device.</b> Aisha's seller dashboard for "Today", a day with no views, read:
/// "Not measured on this deployment: Conversion, Revenue." Revenue is right — Diaspora does not
/// measure it. Conversion is not: the platform measures conversion rate; that day there was nothing
/// to divide by. A rate with no denominator and a metric the deployment cannot produce both reached
/// the builder as <c>IsAvailable = false</c>, and one sentence was written for both.
/// </para>
/// <para>
/// The same held for engagement rate on every creator's dashboard with no views, before a seller
/// dashboard existed. These tests go through <see cref="AnalyticsDatasetBuilder.Build"/>, the path
/// the live routes take, and assert the sentences themselves — the wording is the defect.
/// </para>
/// </summary>
public sealed class AnalyticsLimitationsTests
{
    [Fact]
    public void A_rate_with_nothing_to_divide_by_is_not_called_unmeasured()
    {
        var limitations = LimitationsFor(AnalyticsMetricCatalog.SellerHeadline, views: 0, conversions: 0);

        var notMeasured = limitations.Single(sentence => sentence.StartsWith("Not measured", StringComparison.Ordinal));

        notMeasured.Should().Be("Not measured on this deployment: Revenue.");
        notMeasured.Should().NotContain("Conversion", "the platform measures conversion rate");
    }

    [Fact]
    public void It_says_instead_that_there_was_nothing_to_work_the_rate_out_from()
    {
        var limitations = LimitationsFor(AnalyticsMetricCatalog.SellerHeadline, views: 0, conversions: 0);

        limitations.Should().Contain(
            "No figure yet for Conversion: nothing was viewed in this period, so there is nothing to work it out from.");
    }

    [Fact]
    public void Once_something_was_viewed_the_rate_has_a_figure_and_no_caveat()
    {
        var limitations = LimitationsFor(AnalyticsMetricCatalog.SellerHeadline, views: 12, conversions: 1);

        limitations.Should().ContainSingle().Which.Should().Be("Not measured on this deployment: Revenue.");
    }

    [Fact]
    public void A_creator_with_no_views_is_not_told_engagement_is_unmeasured_either()
    {
        // The same defect, older than the seller dashboard, on every creator overview with a quiet week.
        var limitations = LimitationsFor(AnalyticsMetricCatalog.CreatorHeadline, views: 0, conversions: 0);

        limitations.Single(sentence => sentence.StartsWith("Not measured", StringComparison.Ordinal))
            .Should().NotContain("Engagement");

        limitations.Should().Contain(sentence =>
            sentence.StartsWith("No figure yet for", StringComparison.Ordinal)
            && sentence.Contains("Engagement", StringComparison.Ordinal));
    }

    [Fact]
    public void Metrics_the_deployment_genuinely_does_not_measure_keep_saying_so()
    {
        // The half of the old sentence that was true. It must survive the split.
        var limitations = LimitationsFor(AnalyticsMetricCatalog.CreatorHeadline, views: 40, conversions: 2);

        var notMeasured = limitations.Single(sentence => sentence.StartsWith("Not measured", StringComparison.Ordinal));

        notMeasured.Should().Contain("Revenue").And.Contain("Reach");
    }

    [Fact]
    public void A_dataset_with_nothing_withheld_carries_no_limitation()
    {
        var response = Build([AnalyticsMetricCatalog.ContentViews], views: 5, conversions: 0);

        response.Freshness.Limitations.Should().BeNull();
    }

    [Fact]
    public void Neither_sentence_offers_a_zero_percent_in_place_of_the_rate()
    {
        // A 0% conversion rate for a listing nobody opened would tell a seller it does not sell.
        var response = Build(AnalyticsMetricCatalog.SellerHeadline, views: 0, conversions: 0);

        response.Metrics.Should().NotContain(metric => metric.Key == "conversion.rate");
        response.UnavailableMetrics.Should().Contain("conversion.rate");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static IReadOnlyList<string> LimitationsFor(IReadOnlyList<string> keys, long views, long conversions) =>
        Build(keys, views, conversions).Freshness.Limitations
        ?? throw new InvalidOperationException("expected limitations");

    private static AnalyticsDatasetResponse Build(IReadOnlyList<string> keys, long views, long conversions)
    {
        var options = Microsoft.Extensions.Options.Options.Create(new AnalyticsOptions());
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(new DateTimeOffset(2026, 9, 13, 9, 0, 0, TimeSpan.Zero));

        var builder = new AnalyticsDatasetBuilder(new AnalyticsReportingCalendar(clock, options), options);

        var period = AnalyticsPeriod.Resolve(
            AnalyticsPeriodPreset.Custom,
            new DateOnly(2026, 9, 13),
            new DateOnly(2026, 9, 13),
            TimeZoneInfo.Utc,
            new DateTimeOffset(2026, 9, 13, 9, 0, 0, TimeSpan.Zero),
            366).Period!;

        var counts = new AnalyticsCounts(views, 0, 0, 0, 0, 0, conversions, 0, 0, 0);
        var totals = new AnalyticsWindowTotals(counts, views > 0 ? 1 : 0, 0, HasData: true, HasHistory: true);

        var metrics = AnalyticsMetricAssembler.BuildSet(keys, totals, totals);

        return builder.Build(
            new AnalyticsSubjectResponse { Type = "seller", Id = null, Label = null },
            period,
            period.Previous(),
            metrics,
            [],
            null,
            []);
    }
}
