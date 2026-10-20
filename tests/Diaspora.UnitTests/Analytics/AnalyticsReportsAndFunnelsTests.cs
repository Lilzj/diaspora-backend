using Diaspora.Modules.Analytics.Application.Exports;
using Diaspora.Modules.Analytics.Application.Services;
using Diaspora.Modules.Analytics.Contracts.V1.Requests;
using Diaspora.Modules.Analytics.Contracts.V1.Responses;
using Diaspora.Modules.Analytics.Domain.Enums;
using Diaspora.Modules.Analytics.Domain.Policies;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Analytics;

/// <summary>
/// The pure parts of saved reports, exports and funnels — FRONTEND-PHASE15-GAP-027, -026 and -016.
/// </summary>
public sealed class AnalyticsReportsAndFunnelsTests
{
    // ── Funnel catalogue ────────────────────────────────────────────────────────────────────

    [Fact]
    public void The_catalogue_serves_the_clients_five_journeys_and_no_sixth()
    {
        AnalyticsFunnelCatalog.All.Select(funnel => funnel.Key)
            .Should().BeEquivalentTo("job", "marketplace", "housing", "provider", "monetization");
    }

    [Fact]
    public void Every_counted_step_is_an_event_this_platform_records_and_every_requirement_is_an_earlier_step()
    {
        var recorded = new[]
        {
            AnalyticsEventTypes.ContentViewed,
            AnalyticsEventTypes.ContentSaved,
            AnalyticsEventTypes.ContentEnquired,
            AnalyticsEventTypes.ContentConverted
        };

        foreach (var funnel in AnalyticsFunnelCatalog.All)
        {
            for (var index = 0; index < funnel.Steps.Count; index++)
            {
                var step = funnel.Steps[index];

                recorded.Should().Contain(step.EventType, "{0}.{1} must be a recorded event", funnel.Key, step.Key);

                step.Requires.Should().OnlyContain(
                    key => funnel.Steps.Take(index).Any(earlier => earlier.Key == key),
                    "a requirement must name an earlier step of {0}", funnel.Key);
            }

            funnel.Steps.Select(step => step.Key)
                .Should().NotIntersectWith(funnel.Unavailable.Select(step => step.Key),
                    "a step is either counted or named as unavailable, never both");

            funnel.Unavailable.Should().OnlyContain(step => !string.IsNullOrWhiteSpace(step.Reason));
        }
    }

    [Fact]
    public void A_journey_with_no_recorded_step_is_about_no_content_and_names_every_step_unavailable()
    {
        var placements = AnalyticsFunnelCatalog.Find("monetization")!;

        placements.Steps.Should().BeEmpty();
        placements.ContentType.Should().Be(AnalyticsContentType.Unknown);
        placements.Unavailable.Should().NotBeEmpty();

        AnalyticsFunnelCatalog.Find("onboarding").Should().BeNull("an unknown journey is refused, never invented");
    }

    // ── Query normalisation ─────────────────────────────────────────────────────────────────

    [Fact]
    public void A_query_is_rebuilt_from_its_known_fields_and_bounded()
    {
        var (query, problem) = AnalyticsQueryPayloads.Normalise(new AnalyticsQueryPayload
        {
            Subject = new AnalyticsSubjectPayload { Type = "seller", Id = " 42 " },
            Metrics = ["content.views", "content.views", " conversion.count "],
            Range = new AnalyticsRangePayload { Preset = "last7d", From = "2026-10-01", To = "2026-10-07", Timezone = "Europe/London" }
        });

        problem.Should().BeNull();
        query!.Subject!.Id.Should().Be("42");
        query.Metrics.Should().Equal("content.views", "conversion.count");
        query.IncludeSeries.Should().BeFalse();
        query.Range!.IsDeviceTimezone.Should().BeFalse();
    }

    [Theory]
    [InlineData("galaxy", "2026-10-01", "2026-10-07", "Europe/London")]
    [InlineData("seller", "yesterday", "2026-10-07", "Europe/London")]
    [InlineData("seller", "2026-10-01", "2026-10-07", null)]
    public void A_query_without_a_known_subject_or_a_whole_period_is_refused(
        string type, string from, string to, string? timezone)
    {
        var (query, problem) = AnalyticsQueryPayloads.Normalise(new AnalyticsQueryPayload
        {
            Subject = new AnalyticsSubjectPayload { Type = type },
            Range = new AnalyticsRangePayload { From = from, To = to, Timezone = timezone }
        });

        query.Should().BeNull();
        problem.Should().NotBeNullOrWhiteSpace();
    }

    // ── CSV ─────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void The_csv_quotes_what_it_must_defuses_formulas_and_keeps_negative_numbers()
    {
        var csv = AnalyticsCsvWriter.Write(Dataset("=HYPERLINK(\"x\"), \"Lagos\"", -3), onlyMetrics: null);

        var lines = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        lines[0].Should().StartWith("kind,key,label,date,value,format");
        lines[1].Should().StartWith("metric,growth.members.net,");
        lines[1].Should().Contain(",,-3,count,", "a negative number is a number, not a formula");
        lines[1].Should().Contain("\"'=HYPERLINK(\"\"x\"\"), \"\"Lagos\"\"\"",
            "a formula-looking cell is prefixed and a quoted cell doubles its quotes");
        lines.Should().Contain(line => line.StartsWith("unavailable,growth.members.rate,", StringComparison.Ordinal));
    }

    [Fact]
    public void The_csv_carries_only_the_metrics_asked_for_when_some_were()
    {
        var csv = AnalyticsCsvWriter.Write(Dataset("Circle", 1), onlyMetrics: ["reach.members"]);

        csv.Should().NotContain("growth.members.net");
        csv.Should().NotContain("growth.members.rate");
    }

    private static AnalyticsDatasetResponse Dataset(string label, double net)
    {
        var freshness = new DataFreshnessResponse { ComputedAt = DateTimeOffset.UnixEpoch, SourceName = "test" };

        return new AnalyticsDatasetResponse
        {
            Subject = new AnalyticsSubjectResponse { Type = "community", Id = "c1", Label = label },
            Range = new AnalyticsTimeRangeResponse
            {
                Preset = "last7d", From = "2026-10-01", To = "2026-10-07", Timezone = "UTC", IsDeviceTimezone = false
            },
            Comparison = "previousPeriod",
            Status = "PARTIAL",
            Metrics =
            [
                new AnalyticsMetricResponse
                {
                    Key = "growth.members.net",
                    Label = label,
                    Value = new SourcedValueResponse { Value = net, Source = "backend" },
                    Format = "count",
                    Computation = "server-aggregate",
                    HigherIsBetter = true,
                    Freshness = freshness,
                    PrivacyClass = "privileged"
                }
            ],
            Series = [],
            Breakdowns = [],
            Funnels = [],
            Leaderboards = [],
            Insights = [],
            Freshness = freshness,
            UnavailableMetrics = ["growth.members.rate"]
        };
    }
}
