using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.Contracts.Community.V1.Events;
using Diaspora.Contracts.Housing.V1.Events;
using Diaspora.Contracts.Jobs.V1.Events;
using Diaspora.Domain.Abstractions;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Analytics.Application.Projection;
using Diaspora.Modules.Community.Application.Services;
using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Platform.Messaging;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// B19's Analytics Platform, driven over real HTTP through the same host every other module suite
/// uses, against real persisted rows.
/// <para>
/// <b>Every figure asserted here is produced by an action rather than written by the test.</b> A
/// guide is published through B8's own routes; an engagement is published as B6's own contract and
/// delivered through the real outbox; a view arrives through the telemetry endpoint a device would
/// call. Nothing inserts an analytics row directly, because a suite that did would be asserting that
/// its own fixture reads back — which is the shape of a green test that proves nothing, and is
/// exactly what Wave 4's severest finding was.
/// </para>
/// <para>
/// <b>Nothing here asserts against an empty table alone.</b> Every predicate is exercised from both
/// sides: a period that includes an event and one that excludes it, a creator who owns content and
/// one who does not, a duplicate that is refused and an original that is not.
/// </para>
/// <para>
/// The wire shapes are re-declared by hand rather than referenced from the module, for the reason
/// the B12 suite records: binding a contract test to the module's own records makes it pass whenever
/// the module compiles.
/// </para>
/// </summary>
public sealed class AnalyticsPlatformTests : IdentityTestBase
{
    private const string Analytics = "/api/v1/analytics";
    private const string Telemetry = "/api/v1/telemetry";
    private const string GuidesRoot = "/api/v1/guides";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public AnalyticsPlatformTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // FRONTEND-PHASE15-GAP-029 — event ingestion. The register calls it the blocker.
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>The whole pipeline, end to end, in one test.</b> A guide is published through B8, the
    /// outbox delivers the contract, a device reports a view through the telemetry endpoint, the
    /// projection runs, and the creator's own overview reports one view against a real period. Every
    /// arrow in the phase brief's diagram is exercised by this single assertion.
    /// </summary>
    [Fact]
    public async Task A_reported_view_reaches_the_creator_overview_as_a_real_period_figure()
    {
        var author = await NewMemberAsync();

        var guideId = await PublishGuideAsync("Opening a bank account");
        await Factory.DrainOutboxAsync();

        await NewMemberAsync();

        var accepted = await ReadDataAsync<TelemetryResultDto>(
            await HttpClient.PostAsJsonAsync(
                $"{Telemetry}/events",
                new
                {
                    events = new[]
                    {
                        new
                        {
                            @event = "content.viewed",
                            occurredAt = DateTimeOffset.UtcNow,
                            properties = new { contentId = guideId.ToString(), contentType = "guide" }
                        }
                    }
                },
                JsonOptions));

        accepted.Accepted.Should().Be(1);
        accepted.Results[0].Status.Should().Be("accepted");

        await ProjectAsync();

        Api.Authenticate(author.Token);

        var summary = await ReadDataAsync<CreatorSummaryDto>(
            await HttpClient.GetAsync($"{Analytics}/creators/me/summary?period=30d"));

        Views(summary).Should().Be(1, "one device reported opening one guide");
        summary.Range.Timezone.Should().NotBeNullOrWhiteSpace("§58: the calendar is always stated");
        summary.DataAsOf.Should().NotBeNull("the projection has run and the response says when");
        summary.DefinitionVersion.Should().NotBeNullOrWhiteSpace();
    }

    /// <summary>
    /// <b>The same event twice is one figure.</b> Delivery is at-least-once, a client retries, and a
    /// member reloads a page; none of the three may move a creator's numbers twice. This asserts the
    /// published definition rather than an implementation detail: "one person counted once per item
    /// per day".
    /// </summary>
    [Fact]
    public async Task The_same_view_reported_twice_counts_once()
    {
        var author = await NewMemberAsync();
        var guideId = await PublishGuideAsync("Registering with a GP");
        await Factory.DrainOutboxAsync();

        await NewMemberAsync();

        var occurredAt = DateTimeOffset.UtcNow;

        var first = await PostViewAsync(guideId, occurredAt);
        var second = await PostViewAsync(guideId, occurredAt.AddMinutes(5));

        first.Accepted.Should().Be(1);
        second.Duplicate.Should().Be(1, "the same person, the same item, the same day");
        second.Results[0].Status.Should().Be("duplicate");

        await ProjectAsync();

        Api.Authenticate(author.Token);

        var summary = await ReadDataAsync<CreatorSummaryDto>(
            await HttpClient.GetAsync($"{Analytics}/creators/me/summary?period=30d"));

        Views(summary).Should().Be(1);
    }

    /// <summary>
    /// <b>Two different people opening the same item are two views.</b> The counterpart to the test
    /// above, and the reason it is here: a deduplication key that ignored the actor would collapse
    /// an entire readership into one, and the first test would still pass.
    /// </summary>
    [Fact]
    public async Task Two_readers_opening_one_guide_are_two_views()
    {
        var author = await NewMemberAsync();
        var guideId = await PublishGuideAsync("Council tax explained");
        await Factory.DrainOutboxAsync();

        await AuthenticateAsync();
        await PostViewAsync(guideId, DateTimeOffset.UtcNow);

        await AuthenticateAsync();
        await PostViewAsync(guideId, DateTimeOffset.UtcNow);

        await ProjectAsync();

        var dataset = await ReadContentAsync(guideId, author.Token);

        MetricValue(dataset, "content.views").Should().Be(2);
    }

    /// <summary>
    /// <b>The batch is one transaction, and a duplicate inside it does not fail the rest.</b> A
    /// client that retried mid-flush legitimately sends the same event twice in one payload; a batch
    /// that staged both would be refused whole by the unique index, and the client would lose forty
    /// nine good events because of one repeat.
    /// </summary>
    [Fact]
    public async Task A_duplicate_inside_one_batch_does_not_fail_the_batch()
    {
        var author = await NewMemberAsync();
        var first = await PublishGuideAsync("Winter bills");
        var second = await PublishGuideAsync("Summer bills");
        await Factory.DrainOutboxAsync();

        await AuthenticateAsync();

        var occurredAt = DateTimeOffset.UtcNow;

        var result = await ReadDataAsync<TelemetryResultDto>(
            await HttpClient.PostAsJsonAsync(
                $"{Telemetry}/events",
                new
                {
                    events = new[]
                    {
                        ViewPayload(first, occurredAt),
                        ViewPayload(first, occurredAt),
                        ViewPayload(second, occurredAt)
                    }
                },
                JsonOptions));

        result.Accepted.Should().Be(2);
        result.Duplicate.Should().Be(1);
        result.Rejected.Should().Be(0);

        await ProjectAsync();

        MetricValue(await ReadContentAsync(first, author.Token), "content.views").Should().Be(1);
        MetricValue(await ReadContentAsync(second, author.Token), "content.views").Should().Be(1);
    }

    /// <summary>
    /// <b>Two callers racing one event produce one row.</b> The pre-read cannot see a write another
    /// connection has not committed; the unique index is what makes the guarantee hold under the
    /// horizontal scaling §29 requires. Run against the concurrent host, because the ordinary suite
    /// shares one SQLite connection and cannot express the race at all.
    /// </summary>
    [Fact]
    public async Task Two_concurrent_reports_of_one_view_produce_one_row()
    {
        await using var factory = new ConcurrentWebApplicationFactory();

        using var client = factory.CreateClient();
        factory.EnsureDatabaseCreated();

        var api = new IdentityApiClient(client);
        var suite = new AnalyticsConcurrencyHarness(factory, client, api);

        api.Authenticate(await suite.AuthenticateAsync());

        var guideId = Guid.NewGuid();
        var occurredAt = DateTimeOffset.UtcNow;

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 4).Select(_ => suite.PostViewAsync(guideId, occurredAt)));

        var bodies = await Task.WhenAll(responses.Select(response => response.Content.ReadAsStringAsync()));

        var accepted = bodies.Count(body => body.Contains("\"accepted\":1", StringComparison.Ordinal));

        accepted.Should().Be(
            1, "exactly one of four racing reports may be stored; the rest are duplicates");
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Server-attested engagement — the half of CREATOR-GAP-007 that needs no client release.
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>Saves, shares, comments and reactions are real on the day this ships.</b> Each arrives as
    /// B6's own published contract, through the real outbox, and lands on the creator's overview
    /// with the engagement rate the served definition describes.
    /// </summary>
    [Fact]
    public async Task Server_published_engagement_reaches_the_creator_overview()
    {
        var author = await NewMemberAsync();
        var guideId = await PublishGuideAsync("Finding a dentist");
        await Factory.DrainOutboxAsync();

        await PublishEngagementAsync(guideId, author.UserId);
        await Factory.DrainOutboxAsync();

        // One view, so the engagement rate has a denominator.
        await AuthenticateAsync();
        await PostViewAsync(guideId, DateTimeOffset.UtcNow);

        await ProjectAsync();

        Api.Authenticate(author.Token);

        var summary = await ReadDataAsync<CreatorSummaryDto>(
            await HttpClient.GetAsync($"{Analytics}/creators/me/summary?period=30d"));

        Metric(summary, "saves").Value.Should().Be(1);
        Metric(summary, "shares").Value.Should().Be(1);
        Metric(summary, "comments").Value.Should().Be(1);
        Metric(summary, "helpful_votes").Should().NotBeNull();
        Metric(summary, "content_published").Value.Should().Be(1);

        var rate = Metric(summary, "engagement_rate");

        rate.IsAvailable.Should().BeTrue("one view is a denominator");
        rate.Value.Should().Be(400, "four engagements over one view is 400%");
        rate.Definition.Should().Contain("divided by views");
    }

    /// <summary>
    /// <b>A creator's own actions on their own work are not an audience.</b> The creator refreshing
    /// their guide is the single commonest way a view figure would be wrong, and a creator answering
    /// every question on their own guide would otherwise watch their engagement climb.
    /// </summary>
    [Fact]
    public async Task A_creators_own_view_of_their_own_content_is_not_counted()
    {
        var authorToken = await AuthenticateAsync();
        var guideId = await PublishGuideAsync("Self view");
        await Factory.DrainOutboxAsync();

        Api.Authenticate(authorToken);

        var result = await PostViewAsync(guideId, DateTimeOffset.UtcNow);

        result.Accepted.Should().Be(0, "the owner is not part of their own audience");
        result.Results[0].Reason.Should().Be(
            "analytics.ingest.self-excluded",
            "its own code, because 'duplicate' would say the event was already stored");

        await ProjectAsync();

        var summary = await ReadDataAsync<CreatorSummaryDto>(
            await HttpClient.GetAsync($"{Analytics}/creators/me/summary?period=30d"));

        Views(summary).Should().Be(0);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Time
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>An event outside the window is outside the figure.</b> The register's whole objection to
    /// deriving period analytics from lifetime counters, asserted from both sides: the same guide,
    /// two events, one inside the period and one before it.
    /// </summary>
    [Fact]
    public async Task An_event_before_the_window_is_not_in_the_period()
    {
        var author = await NewMemberAsync();
        var guideId = await PublishGuideAsync("Period boundary");
        await Factory.DrainOutboxAsync();

        await AuthenticateAsync();
        await PostViewAsync(guideId, DateTimeOffset.UtcNow);

        // A second reader, twenty-six hours ago. Twenty-six rather than twenty because a window of
        // less than a day is the same calendar day whenever the suite happens to run before that hour
        // — a test that passes in the evening and fails in the morning is worse than no test.
        await AuthenticateAsync();
        await PostViewAsync(guideId, DateTimeOffset.UtcNow.AddHours(-26));

        await ProjectAsync();

        var today = MetricValue(
            await ReadContentAsync(guideId, author.Token, "today"), "content.views");

        var month = MetricValue(
            await ReadContentAsync(guideId, author.Token, "30d"), "content.views");

        month.Should().Be(2, "both views are inside thirty days");
        today.Should().Be(1, "only one of the two views happened today");
    }

    /// <summary>
    /// <b>An inverted custom range is refused with the code the client already handles.</b> Quoted
    /// from the Phase 7 register rather than invented here: the period selector switches on exactly
    /// this string.
    /// </summary>
    [Fact]
    public async Task An_inverted_custom_range_is_refused_with_the_clients_own_code()
    {
        await AuthenticateAsync();

        var response = await HttpClient.GetAsync(
            $"{Analytics}/creators/me/summary?period=custom&from=2026-08-20&to=2026-08-01");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadAsStringAsync();

        body.Should().Contain("creator.analytics.range_inverted");
    }

    [Fact]
    public async Task A_custom_range_with_one_bound_is_refused_with_the_clients_own_code()
    {
        await AuthenticateAsync();

        var response = await HttpClient.GetAsync(
            $"{Analytics}/creators/me/summary?period=custom&from=2026-08-01");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync())
            .Should().Contain("creator.analytics.range_incomplete");
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Isolation and authorization
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>One creator never sees another's figures.</b> Two creators, two guides, activity on both;
    /// each overview reports only its own. Asserted from both sides, because an isolation test that
    /// only proves a creator sees nothing would pass against an endpoint that returns nothing.
    /// </summary>
    [Fact]
    public async Task A_creator_sees_only_their_own_figures()
    {
        var firstToken = await AuthenticateAsync();
        var firstGuide = await PublishGuideAsync("First creator's guide");
        await Factory.DrainOutboxAsync();

        var secondToken = await AuthenticateAsync();
        var secondGuide = await PublishGuideAsync("Second creator's guide");
        await Factory.DrainOutboxAsync();

        // A reader opens the first creator's guide twice as two people, and the second's once.
        await AuthenticateAsync();
        await PostViewAsync(firstGuide, DateTimeOffset.UtcNow);

        await AuthenticateAsync();
        await PostViewAsync(firstGuide, DateTimeOffset.UtcNow);
        await PostViewAsync(secondGuide, DateTimeOffset.UtcNow);

        await ProjectAsync();

        Api.Authenticate(firstToken);
        var first = await ReadDataAsync<CreatorSummaryDto>(
            await HttpClient.GetAsync($"{Analytics}/creators/me/summary?period=30d"));

        Api.Authenticate(secondToken);
        var second = await ReadDataAsync<CreatorSummaryDto>(
            await HttpClient.GetAsync($"{Analytics}/creators/me/summary?period=30d"));

        Views(first).Should().Be(2);
        Views(second).Should().Be(1);
    }

    /// <summary>
    /// <b>Another creator's content performance is a 404, never a 403.</b> A 403 confirms the item
    /// exists and belongs to somebody, which is exactly what an enumeration attack is looking for.
    /// The register calls this route "the single most likely place for an IDOR in the phase".
    /// </summary>
    [Fact]
    public async Task Another_creators_content_is_not_readable_and_does_not_confirm_it_exists()
    {
        await AuthenticateAsync();
        var guideId = await PublishGuideAsync("Somebody else's guide");
        await Factory.DrainOutboxAsync();

        await AuthenticateAsync();

        var mine = await HttpClient.GetAsync($"{Analytics}/content/guide/{guideId}");

        mine.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var invented = await HttpClient.GetAsync($"{Analytics}/content/guide/{Guid.NewGuid()}");

        invented.StatusCode.Should().Be(
            HttpStatusCode.NotFound, "an existing item and an invented one answer identically");
    }

    /// <summary>An anonymous caller is refused every analytics route.</summary>
    [Fact]
    public async Task An_anonymous_caller_is_refused()
    {
        Api.ClearAuthentication();

        foreach (var route in new[]
        {
            $"{Analytics}/creators/me",
            $"{Analytics}/creators/me/summary",
            $"{Analytics}/creators/me/content",
            $"{Analytics}/definitions",
            $"{Analytics}/access"
        })
        {
            (await HttpClient.GetAsync(route)).StatusCode.Should().Be(
                HttpStatusCode.Unauthorized, "{0} is not public", route);
        }

        var ingestion = await HttpClient.PostAsJsonAsync(
            $"{Telemetry}/events", new { events = Array.Empty<object>() }, JsonOptions);

        ingestion.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// <b>A member without the operations permission cannot rebuild the platform's figures.</b>
    /// §30: a rebuild-all exposed to normal users is the dangerous shape the brief names.
    /// </summary>
    [Fact]
    public async Task A_member_cannot_start_a_backfill()
    {
        await AuthenticateAsync();

        var response = await HttpClient.PostAsJsonAsync(
            $"{Analytics}/operations/backfill",
            new { from = "2026-08-01", to = "2026-08-02" },
            JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Anti-manipulation — §40 and §41
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>A client cannot post a business metric.</b> Saves, shares, comments and conversions are
    /// facts a module commits and publishes; a device claiming one would be incrementing somebody's
    /// creator performance from a phone.
    /// </summary>
    [Theory]
    [InlineData("content.saved")]
    [InlineData("content.shared")]
    [InlineData("content.commented")]
    [InlineData("content.converted")]
    [InlineData("content.published")]
    public async Task A_client_cannot_submit_a_server_produced_event(string eventType)
    {
        await AuthenticateAsync();

        var result = await ReadDataAsync<TelemetryResultDto>(
            await HttpClient.PostAsJsonAsync(
                $"{Telemetry}/events",
                new
                {
                    events = new[]
                    {
                        new
                        {
                            @event = eventType,
                            occurredAt = DateTimeOffset.UtcNow,
                            properties = new
                            {
                                contentId = Guid.NewGuid().ToString(),
                                contentType = "guide"
                            }
                        }
                    }
                },
                JsonOptions));

        result.Rejected.Should().Be(1);
        result.Results[0].Reason.Should().Be("analytics.ingest.event-not-submittable");
    }

    /// <summary>
    /// <b>An unregistered event is refused visibly rather than dropped quietly.</b> The register's
    /// own requirement, and the failure it prevents is the worst one available: the client believes
    /// it is instrumented, the dashboard reads zero, and the two agree for months.
    /// </summary>
    [Fact]
    public async Task An_unregistered_event_is_refused_with_a_reason()
    {
        await AuthenticateAsync();

        var result = await ReadDataAsync<TelemetryResultDto>(
            await HttpClient.PostAsJsonAsync(
                $"{Telemetry}/events",
                new
                {
                    events = new[]
                    {
                        new { @event = "something.invented", occurredAt = DateTimeOffset.UtcNow }
                    }
                },
                JsonOptions));

        result.Rejected.Should().Be(1);
        result.Results[0].Reason.Should().Be("analytics.ingest.event-unknown");
    }

    /// <summary>An event whose version the registry does not carry is refused, and says which.</summary>
    [Fact]
    public async Task An_unknown_event_version_is_refused_distinctly()
    {
        await AuthenticateAsync();

        var result = await ReadDataAsync<TelemetryResultDto>(
            await HttpClient.PostAsJsonAsync(
                $"{Telemetry}/events",
                new
                {
                    events = new[]
                    {
                        new
                        {
                            @event = "content.viewed",
                            version = 99,
                            occurredAt = DateTimeOffset.UtcNow,
                            properties = new
                            {
                                contentId = Guid.NewGuid().ToString(),
                                contentType = "guide"
                            }
                        }
                    }
                },
                JsonOptions));

        result.Results[0].Reason.Should().Be("analytics.ingest.event-version-unknown");
    }

    /// <summary>
    /// <b>A property the allow-list does not carry is refused, not silently stripped.</b> The
    /// client's sanitiser is four phases old and this is what stops it becoming advisory.
    /// </summary>
    [Fact]
    public async Task A_disallowed_property_is_refused()
    {
        await AuthenticateAsync();

        var result = await ReadDataAsync<TelemetryResultDto>(
            await HttpClient.PostAsJsonAsync(
                $"{Telemetry}/events",
                new
                {
                    events = new[]
                    {
                        new
                        {
                            @event = "content.viewed",
                            occurredAt = DateTimeOffset.UtcNow,
                            properties = new
                            {
                                contentId = Guid.NewGuid().ToString(),
                                contentType = "guide",
                                userId = Guid.NewGuid().ToString()
                            }
                        }
                    }
                },
                JsonOptions));

        result.Rejected.Should().Be(1);
        result.Results[0].Reason.Should().Be("analytics.ingest.property-not-allowed");
    }

    /// <summary>
    /// <b>A timestamp far in the past is refused; one slightly in the future is clamped.</b> The
    /// asymmetry is deliberate: a device with a fast clock is ordinary, and a timestamp days old
    /// writes into a period somebody has already read.
    /// </summary>
    [Fact]
    public async Task An_out_of_window_timestamp_is_refused_and_a_small_skew_is_clamped()
    {
        await AuthenticateAsync();
        var guideId = await PublishGuideAsync("Clock skew");
        await Factory.DrainOutboxAsync();

        await AuthenticateAsync();

        var old = await PostViewAsync(guideId, DateTimeOffset.UtcNow.AddDays(-30));

        old.Rejected.Should().Be(1);
        old.Results[0].Reason.Should().Be("analytics.ingest.occurred-at-out-of-range");

        var skewed = await PostViewAsync(guideId, DateTimeOffset.UtcNow.AddMinutes(5));

        skewed.Accepted.Should().Be(1, "five minutes of clock skew is an ordinary device");
    }

    /// <summary>An empty or oversized batch is refused whole, with a named code.</summary>
    [Fact]
    public async Task An_empty_batch_is_refused()
    {
        await AuthenticateAsync();

        var response = await HttpClient.PostAsJsonAsync(
            $"{Telemetry}/events", new { events = Array.Empty<object>() }, JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("analytics.ingest.batch-invalid");
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Contracts — FRONTEND-PHASE15-GAP-025, -030, -009 and -008
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>The capability response is filled in by the server and never derived on the device.</b>
    /// A caller with no access gets an all-false set and a 200 rather than a 403, so the existence
    /// of a dashboard is not confirmed by a status code.
    /// </summary>
    [Fact]
    public async Task The_access_contract_answers_rather_than_refusing()
    {
        await AuthenticateAsync();

        var mine = await ReadDataAsync<AccessDto>(
            await HttpClient.GetAsync($"{Analytics}/access?subjectType=creator"));

        mine.CanView.Should().BeTrue();
        mine.CanViewAdministrative.Should().BeFalse();
        mine.CanViewFinancial.Should().BeFalse("money is B15's and CREATOR-GAP-008's");
        mine.SupportedRanges.Should().Contain(["today", "last7d", "last30d", "last90d", "custom"]);
        mine.ReportingTimezone.Should().NotBeNullOrWhiteSpace();

        var stranger = await ReadDataAsync<AccessDto>(
            await HttpClient.GetAsync(
                $"{Analytics}/access?subjectType=guide&subjectId={Guid.NewGuid()}"));

        stranger.CanView.Should().BeFalse();
        stranger.RestrictedMetrics.Should().NotBeEmpty("a withheld metric is named rather than nulled");
    }

    /// <summary>
    /// <b>Every metric the platform serves carries the platform's own definition.</b> §14 of the
    /// Phase 7 register: the app ships no glossary, so the sentence has to come from here.
    /// </summary>
    [Fact]
    public async Task The_definitions_contract_serves_a_sentence_for_every_metric()
    {
        await AuthenticateAsync();

        var catalogue = await ReadDataAsync<DefinitionsDto>(
            await HttpClient.GetAsync($"{Analytics}/definitions"));

        catalogue.Version.Should().NotBeNullOrWhiteSpace();
        catalogue.Definitions.Should().NotBeEmpty();

        foreach (var definition in catalogue.Definitions)
        {
            definition.Definition.Length.Should().BeGreaterThan(20);
            definition.Owner.Should().NotBeNullOrWhiteSpace();
            definition.Status.Should().BeOneOf("CONNECTED", "AWAITING_SOURCE", "NOT_MEASURED");
        }

        catalogue.Definitions.Should().Contain(definition =>
            definition.Key == "reach" && definition.Status == "NOT_MEASURED");
    }

    /// <summary>
    /// <b>An unmeasured metric is named rather than served as a zero.</b> Reach is the one the
    /// register and Wave 6 both single out: a zero would tell a creator nobody saw their work, which
    /// is a claim this platform cannot make.
    /// </summary>
    [Fact]
    public async Task An_unmeasured_metric_is_reported_as_unavailable_rather_than_zero()
    {
        await AuthenticateAsync();

        var summary = await ReadDataAsync<CreatorSummaryDto>(
            await HttpClient.GetAsync($"{Analytics}/creators/me/summary?period=30d"));

        summary.UnavailableMetrics.Should().Contain("reach");
        summary.UnavailableMetrics.Should().Contain("revenue");

        Metric(summary, "reach").IsAvailable.Should().BeFalse();
        Metric(summary, "reach").Definition.Should().ContainEquivalentOf("not measured");

        var dataset = await ReadDataAsync<DatasetDto>(
            await HttpClient.GetAsync($"{Analytics}/creators/me"));

        dataset.Status.Should().Be("PARTIAL", "some metrics could not be answered");
        dataset.UnavailableMetrics.Should().Contain("content.reach");
        dataset.Metrics.Should().NotContain(metric => metric.Key == "content.reach");
        dataset.Freshness.Limitations.Should().NotBeNull();
    }

    /// <summary>
    /// <b>The dataset and the summary are two renderings of one set of figures.</b> If they could
    /// disagree, a member moving between the creator dashboard and the analytics hub would see two
    /// different numbers for the same week.
    /// </summary>
    [Fact]
    public async Task The_dataset_and_the_summary_report_the_same_figures()
    {
        var authorToken = await AuthenticateAsync();
        var guideId = await PublishGuideAsync("Two renderings");
        await Factory.DrainOutboxAsync();

        await AuthenticateAsync();
        await PostViewAsync(guideId, DateTimeOffset.UtcNow);

        await ProjectAsync();

        Api.Authenticate(authorToken);

        var summary = await ReadDataAsync<CreatorSummaryDto>(
            await HttpClient.GetAsync($"{Analytics}/creators/me/summary?period=30d"));

        var dataset = await ReadDataAsync<DatasetDto>(
            await HttpClient.GetAsync($"{Analytics}/creators/me?period=30d&includeSeries=true"));

        MetricValue(dataset, "content.views").Should().Be(Views(summary));
        dataset.Leaderboards.Should().ContainSingle(board => board.Key == "creator-content");
        dataset.Leaderboards[0].Rows[0].Entity.EntityId.Should().Be(guideId.ToString());
        dataset.Series.Should().NotBeEmpty("includeSeries was asked for");
        dataset.Range.IsDeviceTimezone.Should().BeFalse("the platform publishes its own zone");
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Comparison history — "Not enough history yet", and when it is true
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>A quiet day after earlier activity is compared with zero, not called new.</b> Found on a
    /// seller dashboard on 13 Sep 2026: every tile for "Today" read "Not enough history yet" for a
    /// member with a fortnight of history, because the day before had been quiet and a quiet day has
    /// no rows. Asserted on all four routes that share the rule, so a figure cannot mean one thing on
    /// the overview and another on its own row.
    /// </summary>
    [Fact]
    public async Task A_quiet_day_after_earlier_activity_is_compared_with_zero_rather_than_called_new()
    {
        var author = await NewMemberAsync();
        var guideId = await PublishGuideAsync("Ten days of history");
        await Factory.DrainOutboxAsync();

        // A save ten days ago, published as B6's own contract carrying the time it happened. A server
        // event is not held to the client's 48-hour window, which is what lets the suite put history
        // before yesterday without writing an analytics row by hand.
        await PublishBookmarkAsync(guideId, author.UserId, DateTimeOffset.UtcNow.AddDays(-10));
        await Factory.DrainOutboxAsync();

        await AuthenticateAsync();
        await PostViewAsync(guideId, DateTimeOffset.UtcNow);

        await ProjectAsync();

        var reasons = await ReadTodayReasonsAsync(author.Token, guideId);

        // Summary, creator dataset, content page row, item dataset — in that order, so a failure
        // names the route.
        reasons.Should().Equal(
            ["zeroBaseline", "zeroBaseline", "zeroBaseline", "zeroBaseline"],
            "the author was recorded ten days ago and nothing happened yesterday — a real zero");
    }

    /// <summary>
    /// <b>The other side: a member whose first activity is today has no history to compare.</b>
    /// Without it, the test above could pass by never saying "insufficient history" at all.
    /// </summary>
    [Fact]
    public async Task A_member_first_recorded_today_has_no_history_to_compare()
    {
        var author = await NewMemberAsync();
        var guideId = await PublishGuideAsync("First day");
        await Factory.DrainOutboxAsync();

        await AuthenticateAsync();
        await PostViewAsync(guideId, DateTimeOffset.UtcNow);

        await ProjectAsync();

        var reasons = await ReadTodayReasonsAsync(author.Token, guideId);

        reasons.Should().Equal(
            ["insufficientHistory", "insufficientHistory", "insufficientHistory", "insufficientHistory"],
            "nothing was recorded for this author, or this guide, before today");
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // FRONTEND-PHASE15-GAP-013 — a landlord's own dataset
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>A landlord's views and viewings reach their own dataset, under the member and never the
    /// landlord profile.</b> Housing names the profile on every event; the home's publication also
    /// carries the member, and a viewing — which names only the profile — must be attributed through
    /// that publication. The seller dashboard read nothing for every seller until 13 Sep 2026
    /// because rows were keyed by the profile, and no fixture had two different identifiers to
    /// notice with. This one does.
    /// </summary>
    [Fact]
    public async Task A_landlords_views_and_viewings_reach_their_dataset_under_the_member_not_the_profile()
    {
        var landlord = await NewMemberAsync();

        var landlordProfileId = Guid.NewGuid();
        var homeId = Guid.NewGuid();

        landlordProfileId.Should().NotBe(landlord.UserId, "the whole point of the test");

        await PublishHomeAsync(homeId, landlordProfileId, landlord.UserId);
        await Factory.DrainOutboxAsync();

        var seeker = await NewMemberAsync();
        (await PostViewAsync(homeId, DateTimeOffset.UtcNow, "housing")).Accepted.Should().Be(1);

        await PublishViewingAsync(homeId, landlordProfileId, seeker.UserId);
        await Factory.DrainOutboxAsync();

        await ProjectAsync();

        Api.Authenticate(landlord.Token);

        var dataset = await ReadDataAsync<DatasetDto>(
            await HttpClient.GetAsync($"{Analytics}/landlords/me?period=30d&includeSeries=true"));

        MetricValue(dataset, "content.views").Should().Be(1, "one member opened the home");
        MetricValue(dataset, "conversion.count").Should().Be(
            1, "one viewing was booked, and it names only the landlord profile");
        MetricValue(dataset, "conversion.rate").Should().Be(100, "one viewing for one view");
        MetricValue(dataset, "content.published").Should().Be(1);

        dataset.Metrics.Single(metric => metric.Key == "conversion.count").Label.Should().Be("Viewings");
        dataset.Metrics.Single(metric => metric.Key == "conversion.rate").Label.Should().Be("Viewing rate");

        dataset.Metrics.Should().NotContain(metric => metric.Key == "revenue.gross");

        var board = dataset.Leaderboards.Should().ContainSingle(entry => entry.Key == "landlord-listings").Subject;
        board.Rows.Should().ContainSingle().Which.Entity.EntityId.Should().Be(homeId.ToString());
        board.Rows[0].Entity.Title.Should().Be("Two-bed flat near the station");

        // The same viewing on the member's overview. The dataset above counts a home's row, whose
        // owner comes from the home's publication — so a viewing attributed to the profile would
        // still reach it. The overview keys by each event's own owner, and is where a viewing sent
        // with the profile id would go missing.
        var overview = await ReadDataAsync<CreatorSummaryDto>(
            await HttpClient.GetAsync($"{Analytics}/creators/me/summary?period=30d"));

        Metric(overview, "conversion").Value.Should().Be(
            1, "the viewing belongs to the member, not to their landlord profile");

        // The same action by the same name on the home's own page and on its row in the content table.
        var homePage = await ReadDataAsync<DatasetDto>(
            await HttpClient.GetAsync($"{Analytics}/content/housing/{homeId}?period=30d"));

        homePage.Metrics.Single(metric => metric.Key == "conversion.count").Label.Should().Be("Viewings");

        var homeRows = await ReadDataAsync<ContentPageDto>(
            await HttpClient.GetAsync($"{Analytics}/creators/me/content?period=30d"));

        homeRows.Items.Single(item => item.ContentId == homeId.ToString())
            .Metrics.Single(metric => metric.Key == "conversion").Label.Should().Be("Viewings");

        // The other sides. The same member's seller dashboard does not count a home…
        var asSeller = await ReadDataAsync<DatasetDto>(
            await HttpClient.GetAsync($"{Analytics}/sellers/me?period=30d"));

        MetricValue(asSeller, "content.views").Should().Be(0, "a home is not a marketplace listing");

        // …and nobody else's landlord dashboard carries any of it, including the seeker's.
        Api.Authenticate(seeker.Token);

        var stranger = await ReadDataAsync<DatasetDto>(
            await HttpClient.GetAsync($"{Analytics}/landlords/me?period=30d"));

        MetricValue(stranger, "content.views").Should().Be(0);
        MetricValue(stranger, "conversion.count").Should().Be(0);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // FRONTEND-PHASE15-GAP-012 — an employer's own dataset
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>An employer's views and applications reach their own dataset, under the member and never
    /// the employer profile.</b> The application is published with no <c>EmployerUserId</c> at all —
    /// the shape the historical half-fix <c>EmployerUserId ?? EmployerId</c> got wrong — so it must
    /// be attributed through the job's publication, on the dataset and on the member's overview.
    /// </summary>
    [Fact]
    public async Task An_employers_views_and_applications_reach_their_dataset_under_the_member_not_the_profile()
    {
        var employer = await NewMemberAsync();

        var employerProfileId = Guid.NewGuid();
        var jobId = Guid.NewGuid();

        employerProfileId.Should().NotBe(employer.UserId, "the whole point of the test");

        await PublishJobAsync(jobId, employerProfileId, employer.UserId);
        await Factory.DrainOutboxAsync();

        var applicant = await NewMemberAsync();
        (await PostViewAsync(jobId, DateTimeOffset.UtcNow, "job")).Accepted.Should().Be(1);

        await PublishApplicationAsync(jobId, employerProfileId, applicant.UserId);
        await Factory.DrainOutboxAsync();

        await ProjectAsync();

        Api.Authenticate(employer.Token);

        var dataset = await ReadDataAsync<DatasetDto>(
            await HttpClient.GetAsync($"{Analytics}/employers/me?period=30d&includeSeries=true"));

        MetricValue(dataset, "content.views").Should().Be(1, "one member opened the job");
        MetricValue(dataset, "conversion.count").Should().Be(
            1, "one application, which named only the employer profile");
        MetricValue(dataset, "conversion.rate").Should().Be(100, "one application for one view");
        MetricValue(dataset, "content.published").Should().Be(1);

        dataset.Metrics.Single(metric => metric.Key == "conversion.count").Label.Should().Be("Applications");
        dataset.Metrics.Single(metric => metric.Key == "conversion.rate").Label.Should().Be("Application rate");

        dataset.Metrics.Should().NotContain(metric => metric.Key == "revenue.gross");

        var board = dataset.Leaderboards.Should().ContainSingle(entry => entry.Key == "employer-jobs").Subject;
        board.Rows.Should().ContainSingle().Which.Entity.EntityId.Should().Be(jobId.ToString());
        board.Rows[0].Entity.Title.Should().Be("Community nurse, night shifts");

        // The overview keys by each event's own owner: where an application attributed to the
        // profile would go missing while the dataset above still counted it.
        var overview = await ReadDataAsync<CreatorSummaryDto>(
            await HttpClient.GetAsync($"{Analytics}/creators/me/summary?period=30d"));

        Metric(overview, "conversion").Value.Should().Be(
            1, "the application belongs to the member, not to their employer profile");

        var jobPage = await ReadDataAsync<DatasetDto>(
            await HttpClient.GetAsync($"{Analytics}/content/job/{jobId}?period=30d"));

        jobPage.Metrics.Single(metric => metric.Key == "conversion.count").Label.Should().Be("Applications");

        var jobRows = await ReadDataAsync<ContentPageDto>(
            await HttpClient.GetAsync($"{Analytics}/creators/me/content?period=30d"));

        jobRows.Items.Single(item => item.ContentId == jobId.ToString())
            .Metrics.Single(metric => metric.Key == "conversion").Label.Should().Be("Applications");

        // The other sides: a job is not a home, and the applicant's own dashboard carries none of it.
        var asLandlord = await ReadDataAsync<DatasetDto>(
            await HttpClient.GetAsync($"{Analytics}/landlords/me?period=30d"));

        MetricValue(asLandlord, "content.views").Should().Be(0, "a job is not a housing listing");

        Api.Authenticate(applicant.Token);

        var stranger = await ReadDataAsync<DatasetDto>(
            await HttpClient.GetAsync($"{Analytics}/employers/me?period=30d"));

        MetricValue(stranger, "content.views").Should().Be(0);
        MetricValue(stranger, "conversion.count").Should().Be(0);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Content performance and paging
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>Everything published appears, including what nobody read.</b> A list built from the
    /// rollups alone would silently omit exactly the content that is not working, which is the
    /// content a creator most needs to see.
    /// </summary>
    [Fact]
    public async Task Content_performance_lists_every_item_including_the_unread_ones()
    {
        var author = await NewMemberAsync();

        var read = await PublishGuideAsync("Read guide");
        var unread = await PublishGuideAsync("Unread guide");
        await Factory.DrainOutboxAsync();

        await AuthenticateAsync();
        await PostViewAsync(read, DateTimeOffset.UtcNow);

        await ProjectAsync();

        Api.Authenticate(author.Token);

        var page = await ReadDataAsync<ContentPageDto>(
            await HttpClient.GetAsync($"{Analytics}/creators/me/content?period=30d"));

        page.Items.Should().HaveCount(2, "an item nobody read is a row reading zero, not an absence");

        var readRow = page.Items.Single(item => item.ContentId == read.ToString());
        var unreadRow = page.Items.Single(item => item.ContentId == unread.ToString());

        readRow.Metrics.Single(metric => metric.Key == "content_views").Value.Should().Be(1);
        unreadRow.Metrics.Single(metric => metric.Key == "content_views").Value.Should().Be(0);

        readRow.ContentType.Should().Be("Guide", "the client's own vocabulary");
        readRow.Title.Should().Be("Read guide");

        // A guide asks for nothing, so its row keeps the catalogue's general name for the action.
        readRow.Metrics.Single(metric => metric.Key == "conversion").Label.Should().Be("Actions taken");
        readRow.Status.Should().NotBeNullOrWhiteSpace();

        page.Items[0].ContentId.Should().Be(
            read.ToString(), "the most-viewed item is ranked first, by the server");
    }

    /// <summary>
    /// <b>The list is cursor-paged from the first request, and the pages do not overlap.</b> Six
    /// items, three at a time: every identifier appears exactly once across the pages.
    /// </summary>
    [Fact]
    public async Task Content_performance_pages_without_repeating_or_skipping()
    {
        var authorToken = await AuthenticateAsync();

        for (var index = 0; index < 6; index++)
        {
            await PublishGuideAsync($"Paged guide {index}");
        }

        await Factory.DrainOutboxAsync();
        await ProjectAsync();

        Api.Authenticate(authorToken);

        var seen = new List<string>();
        string? cursor = null;

        for (var page = 0; page < 5; page++)
        {
            var query = cursor is null
                ? $"{Analytics}/creators/me/content?period=30d&take=3"
                : $"{Analytics}/creators/me/content?period=30d&take=3&cursor={Uri.EscapeDataString(cursor)}";

            var result = await ReadDataAsync<ContentPageDto>(await HttpClient.GetAsync(query));

            seen.AddRange(result.Items.Select(item => item.ContentId));

            if (!result.HasMore)
            {
                result.NextCursor.Should().BeNull("the final page carries no cursor");
                break;
            }

            result.NextCursor.Should().NotBeNullOrWhiteSpace();
            cursor = result.NextCursor;
        }

        seen.Should().HaveCount(6);
        seen.Should().OnlyHaveUniqueItems("no item may be served on two pages");
    }

    /// <summary>A malformed cursor answers the first page rather than a 400.</summary>
    [Fact]
    public async Task A_malformed_cursor_answers_the_first_page()
    {
        await AuthenticateAsync();
        await PublishGuideAsync("Cursor guide");
        await Factory.DrainOutboxAsync();
        await ProjectAsync();

        var result = await ReadDataAsync<ContentPageDto>(
            await HttpClient.GetAsync($"{Analytics}/creators/me/content?cursor=not-a-cursor"));

        result.Items.Should().NotBeEmpty();
    }

    /// <summary>A creator with nothing published gets an empty page rather than an error.</summary>
    [Fact]
    public async Task A_creator_with_no_content_gets_an_empty_page()
    {
        await AuthenticateAsync();

        var result = await ReadDataAsync<ContentPageDto>(
            await HttpClient.GetAsync($"{Analytics}/creators/me/content?period=30d"));

        result.Items.Should().BeEmpty();
        result.HasMore.Should().BeFalse();
        result.NextCursor.Should().BeNull();
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Volume — §51's real-database requirement
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>Over a thousand real analytics rows, across several creators, several content types and
    /// twenty days, then the figures are read back and checked against what was written.</b> §51
    /// asks for exactly this, and the reason is Wave 4's: a suite that only ever sees one row passes
    /// against a query that is wrong the moment it has data.
    /// </summary>
    [Fact]
    public async Task A_thousand_events_across_many_creators_aggregate_correctly()
    {
        // 1,080 published engagements: three creators, six items each, sixty days. Sixty rather
        // than thirty so the period filter has something to exclude at this volume — a window that
        // covered everything written would prove the sum and not the cut.
        const int Creators = 3;
        const int GuidesPerCreator = 6;
        const int Days = 60;
        const int WindowDays = 30;

        var creators = new List<(string Token, Guid UserId, List<Guid> Guides)>();

        for (var index = 0; index < Creators; index++)
        {
            var member = await NewMemberAsync();
            var guides = new List<Guid>();

            for (var guide = 0; guide < GuidesPerCreator; guide++)
            {
                guides.Add(await PublishGuideAsync($"Volume {index}-{guide}"));
            }

            await Factory.DrainOutboxAsync();
            creators.Add((member.Token, member.UserId, guides));
        }

        var expected = creators.ToDictionary(creator => creator.UserId, _ => 0);
        var written = 0;

        using (var scope = Factory.Services.CreateScope())
        {
            var publisher = scope.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            for (var day = 0; day < Days; day++)
            {
                foreach (var creator in creators)
                {
                    foreach (var guide in creator.Guides)
                    {
                        // Server-attested engagement, published as B6's own contract, dated across
                        // the window so the period arithmetic has something to cut.
                        await publisher.PublishAsync(new CommunityBookmarkAddedIntegrationEvent
                        {
                            OccurredAt = DateTimeOffset.UtcNow.AddDays(-day),
                            ContentType = CommunityWireNames.ToWire(CommunityContentType.Guide),
                            ContentId = guide,
                            UserId = Guid.NewGuid(),
                            ContentAuthorId = creator.UserId
                        });

                        if (day < WindowDays)
                        {
                            expected[creator.UserId]++;
                        }

                        written++;
                    }
                }
            }

            await unitOfWork.SaveChangesAsync();
        }

        written.Should().BeGreaterThan(1000, "§51 asks for a realistic volume");

        await Factory.DrainOutboxAsync(maxPasses: 200);
        await ProjectAsync(passes: 40);

        foreach (var creator in creators)
        {
            Api.Authenticate(creator.Token);

            var summary = await ReadDataAsync<CreatorSummaryDto>(
                await HttpClient.GetAsync($"{Analytics}/creators/me/summary?period=30d"));

            Metric(summary, "saves").Value.Should().Be(
                expected[creator.UserId],
                "every save inside the window for this creator, none from outside it, and none of "
                    + "anybody else's");

            expected[creator.UserId].Should().Be(
                GuidesPerCreator * WindowDays, "the window covers thirty of the sixty days written");
        }
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Late events, backfill and erasure — §8, §30, §31 and §34
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>An event for a day the projection has already computed still lands in that day.</b> The
    /// hardest property in the phase brief and the one a naive projection gets wrong: a watermark on
    /// occurrence time would put a late arrival permanently behind the cursor, and nothing would ever
    /// report it. Here the day is computed, closed and then reopened by a fact that arrives after it.
    /// </summary>
    [Fact]
    public async Task An_event_arriving_after_its_day_was_computed_still_reaches_the_figure()
    {
        var author = await NewMemberAsync();
        var guideId = await PublishGuideAsync("Late arrival");
        await Factory.DrainOutboxAsync();

        // Today is computed and closed.
        await AuthenticateAsync();
        await PostViewAsync(guideId, DateTimeOffset.UtcNow);
        await ProjectAsync();

        var beforeLateArrival = MetricValue(
            await ReadContentAsync(guideId, author.Token, "30d"), "content.views");

        beforeLateArrival.Should().Be(1);

        // A second reader's view of *yesterday* arrives now, after yesterday was already projected.
        await AuthenticateAsync();
        var late = await PostViewAsync(guideId, DateTimeOffset.UtcNow.AddHours(-26));

        late.Accepted.Should().Be(1, "a day-old client event is inside the acceptance window");

        await ProjectAsync();

        var afterLateArrival = MetricValue(
            await ReadContentAsync(guideId, author.Token, "30d"), "content.views");

        afterLateArrival.Should().Be(2, "the late fact was folded into the day it happened on");

        var todayOnly = MetricValue(
            await ReadContentAsync(guideId, author.Token, "today"), "content.views");

        todayOnly.Should().Be(1, "and it was not folded into today, which is where it arrived");
    }

    /// <summary>
    /// <b>A backfill rebuilds a window and reaches the same answer.</b> §31 asks for a documented
    /// recomputation strategy; this is it, exercised: the rollups are rewritten from the raw store
    /// and the figures do not move, which is what "idempotent" means when the projection is a
    /// recomputation rather than an increment.
    /// </summary>
    [Fact]
    public async Task A_backfill_rebuilds_a_window_and_the_figures_do_not_move()
    {
        var author = await NewMemberAsync();
        var guideId = await PublishGuideAsync("Backfilled");
        await Factory.DrainOutboxAsync();

        await AuthenticateAsync();
        await PostViewAsync(guideId, DateTimeOffset.UtcNow);
        await ProjectAsync();

        var before = MetricValue(
            await ReadContentAsync(guideId, author.Token, "30d"), "content.views");

        before.Should().Be(1);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var run = await RunPrivilegedAsync(async sender =>
            await sender.Send(
                new Diaspora.Modules.Analytics.Application.Operations.StartAnalyticsBackfillCommand
                {
                    Request = new Diaspora.Modules.Analytics.Contracts.V1.Requests
                        .AnalyticsBackfillRequest
                    {
                        From = today.AddDays(-3).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        To = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                    }
                }));

        run.Status.Should().Be("Queued");
        run.NextDay.Should().Be(today.AddDays(-3).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

        // A second request while one is in flight is a conflict rather than a second run racing the
        // first for the same cells.
        var second = await RunPrivilegedRawAsync(async sender =>
            await sender.Send(
                new Diaspora.Modules.Analytics.Application.Operations.StartAnalyticsBackfillCommand
                {
                    Request = new Diaspora.Modules.Analytics.Contracts.V1.Requests
                        .AnalyticsBackfillRequest
                    {
                        From = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        To = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                    }
                }));

        second.IsFailure.Should().BeTrue();
        second.Error.Code.Should().Be("analytics.backfill.in-flight");

        // Drive the worker to completion, one slice at a time, exactly as a deployment would.
        for (var pass = 0; pass < 5; pass++)
        {
            using var scope = Factory.Services.CreateScope();

            var worker = scope.ServiceProvider
                .GetServices<Microsoft.Extensions.Hosting.IHostedService>()
                .OfType<Diaspora.Modules.Analytics.Infrastructure.Hosting.AnalyticsBackfillWorker>()
                .Single();

            if (!await worker.RunOnceAsync(CancellationToken.None))
            {
                break;
            }
        }

        var after = MetricValue(
            await ReadContentAsync(guideId, author.Token, "30d"), "content.views");

        after.Should().Be(before, "a rebuild recomputes the same cells to the same values");
    }

    /// <summary>
    /// <b>A backfill wider than the cap is refused rather than truncated.</b> §30: the dangerous
    /// shape is a rebuild-all, and a request silently narrowed to fit is a rebuild the operator
    /// thinks happened and did not.
    /// </summary>
    [Fact]
    public async Task A_backfill_wider_than_the_cap_is_refused()
    {
        await AuthenticateAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var result = await RunPrivilegedRawAsync(async sender =>
            await sender.Send(
                new Diaspora.Modules.Analytics.Application.Operations.StartAnalyticsBackfillCommand
                {
                    Request = new Diaspora.Modules.Analytics.Contracts.V1.Requests
                        .AnalyticsBackfillRequest
                    {
                        From = today.AddYears(-2).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        To = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                    }
                }));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("analytics.backfill.window-too-wide");
    }

    /// <summary>
    /// <b>Erasing a member removes every analytics row about them, and the figures follow.</b> §34
    /// asks for a documented treatment of a deletion request; this is it, and the mechanism is what
    /// makes it possible: the actor key is a deterministic keyed hash, so erasure computes it once
    /// and deletes precisely rather than scanning.
    /// </summary>
    [Fact]
    public async Task Erasing_a_member_removes_their_analytics_rows_and_the_figures_follow()
    {
        var author = await NewMemberAsync();
        var guideId = await PublishGuideAsync("Erasure");
        await Factory.DrainOutboxAsync();

        var reader = await NewMemberAsync();
        await PostViewAsync(guideId, DateTimeOffset.UtcNow);

        var other = await NewMemberAsync();
        await PostViewAsync(guideId, DateTimeOffset.UtcNow);
        _ = other;

        await ProjectAsync();

        MetricValue(await ReadContentAsync(guideId, author.Token, "30d"), "content.views")
            .Should().Be(2);

        int deleted;

        using (var scope = Factory.Services.CreateScope())
        {
            var hasher = scope.ServiceProvider
                .GetRequiredService<Diaspora.Modules.Analytics.Application.Services.AnalyticsActorHasher>();

            var events = scope.ServiceProvider
                .GetRequiredService<Diaspora.Modules.Analytics.Domain.Repositories.IAnalyticsEventRepository>();

            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            deleted = await events.DeleteForActorAsync(hasher.Hash(reader.UserId));

            await unitOfWork.SaveChangesAsync();
        }

        deleted.Should().Be(1, "one row named that member and no other row did");

        var todayCell = DateOnly.FromDateTime(DateTime.UtcNow);

        using (var scope = Factory.Services.CreateScope())
        {
            var projection = scope.ServiceProvider.GetRequiredService<AnalyticsProjectionService>();

            await projection.RebuildRangeAsync(todayCell.AddDays(-1), todayCell);
        }

        MetricValue(await ReadContentAsync(guideId, author.Token, "30d"), "content.views")
            .Should().Be(1, "the erased member's view is gone from the figure, and only theirs");
    }

    /// <summary>
    /// <b>The projection idles when there is nothing new, and this is a regression test.</b> The
    /// first implementation used an inclusive cursor: harmless to every figure, because the
    /// projection recomputes rather than increments — and it meant the batch was never empty, so on
    /// a platform with no traffic the loop re-read one row and rebuilt its cells every thirty
    /// seconds, forever. Nothing failed. It was found by reading the loop.
    /// </summary>
    [Fact]
    public async Task The_projection_reports_no_work_once_it_has_caught_up()
    {
        await NewMemberAsync();
        var guideId = await PublishGuideAsync("Idle");
        await Factory.DrainOutboxAsync();

        await AuthenticateAsync();
        await PostViewAsync(guideId, DateTimeOffset.UtcNow);

        var processedOnFirstPass = await ProjectOnceAsync();

        processedOnFirstPass.Should().BeGreaterThan(0, "there was something to fold in");

        await ProjectAsync();

        var processedWhenCaughtUp = await ProjectOnceAsync();

        processedWhenCaughtUp.Should().Be(
            0, "a caught-up projection must do nothing rather than re-read its own boundary");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A fresh member, authenticated, with their identifier.
    /// <para>
    /// The identifier comes from the authentication response rather than a second request, so a
    /// test never has to guess which caller the client is currently carrying.
    /// </para>
    /// </summary>
    private async Task<Member> NewMemberAsync()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();

        Api.Authenticate(auth.Tokens.AccessToken);

        return new Member(auth.Tokens.AccessToken, auth.User.Id);
    }

    private async Task<string> AuthenticateAsync() => (await NewMemberAsync()).Token;

    private static object ViewPayload(Guid contentId, DateTimeOffset occurredAt, string contentType = "guide") => new
    {
        @event = "content.viewed",
        occurredAt,
        properties = new { contentId = contentId.ToString(), contentType }
    };

    private async Task<TelemetryResultDto> PostViewAsync(
        Guid contentId, DateTimeOffset occurredAt, string contentType = "guide")
    {
        var response = await HttpClient.PostAsJsonAsync(
            $"{Telemetry}/events",
            new { events = new[] { ViewPayload(contentId, occurredAt, contentType) } },
            JsonOptions);

        return await ReadDataAsync<TelemetryResultDto>(response);
    }

    /// <summary>
    /// A home going on the market, published as B11's own contract.
    /// <para>
    /// <paramref name="landlordProfileId"/> and <paramref name="landlordUserId"/> are different
    /// identifiers on purpose, because they are in Housing: every fixture that used one Guid for both
    /// passed while every owner dashboard stayed empty.
    /// </para>
    /// </summary>
    private async Task PublishHomeAsync(Guid homeId, Guid landlordProfileId, Guid landlordUserId)
    {
        using var scope = Factory.Services.CreateScope();

        var publisher = scope.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        await publisher.PublishAsync(new HousingPublishedIntegrationEvent
        {
            ListingId = homeId,
            LandlordId = landlordProfileId,
            LandlordUserId = landlordUserId,
            LandlordName = "Harbour Lettings",
            LandlordKind = "Individual",
            IsLandlordVerified = true,
            Title = "Two-bed flat near the station",
            Slug = $"two-bed-flat-{homeId:N}",
            AccommodationTypeKey = "apartment",
            IsRentDisclosed = false,
            Bedrooms = 2,
            Bathrooms = 1,
            FurnishedStatus = "Furnished",
            TotalUnits = 1,
            AcceptsShortStay = false,
            AcceptsLongStay = true,
            IsStudentFriendly = false,
            IsFamilyFriendly = true,
            IsSharedAccommodation = false,
            CountryCode = "GB",
            IsFirstPublication = true,
            PublishedAt = DateTimeOffset.UtcNow
        });

        await unitOfWork.SaveChangesAsync();
    }

    /// <summary>
    /// A job going live, published as B9's own contract, with an employer profile id that differs
    /// from the member's user id — as it does in Jobs.
    /// </summary>
    private async Task PublishJobAsync(Guid jobId, Guid employerProfileId, Guid employerUserId)
    {
        using var scope = Factory.Services.CreateScope();

        var publisher = scope.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        await publisher.PublishAsync(new JobPublishedIntegrationEvent
        {
            JobId = jobId,
            EmployerId = employerProfileId,
            EmployerUserId = employerUserId,
            EmployerName = "Harbour Care",
            IsEmployerVerified = true,
            Title = "Community nurse, night shifts",
            Slug = $"community-nurse-{jobId:N}",
            CategoryKey = "healthcare",
            Tags = [],
            SkillKeys = [],
            CountryCode = "GB",
            WorkplaceType = "OnSite",
            JobType = "FullTime",
            ExperienceLevel = "Mid",
            OffersVisaSponsorship = true,
            OffersRelocationAssistance = false,
            IsSalaryDisclosed = false,
            VacancyCount = 1,
            IsFirstPublication = true,
            PublishedAt = DateTimeOffset.UtcNow
        });

        await unitOfWork.SaveChangesAsync();
    }

    /// <summary>
    /// An application to a job, published as B9's own contract with <b>no</b> employer user id — so
    /// ingestion has only the job's publication to attribute it by.
    /// </summary>
    private async Task PublishApplicationAsync(Guid jobId, Guid employerProfileId, Guid applicantUserId)
    {
        using var scope = Factory.Services.CreateScope();

        var publisher = scope.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        await publisher.PublishAsync(new ApplicationSubmittedIntegrationEvent
        {
            ApplicationId = Guid.NewGuid(),
            JobId = jobId,
            EmployerId = employerProfileId,
            CandidateId = Guid.NewGuid(),
            ApplicantUserId = applicantUserId,
            EmployerUserId = null,
            CountryCode = "GB",
            SubmittedAt = DateTimeOffset.UtcNow
        });

        await unitOfWork.SaveChangesAsync();
    }

    /// <summary>
    /// A viewing booked on a home, published as B11's own contract — which names the landlord
    /// <em>profile</em> and no member at all.
    /// </summary>
    private async Task PublishViewingAsync(Guid homeId, Guid landlordProfileId, Guid seekerUserId)
    {
        using var scope = Factory.Services.CreateScope();

        var publisher = scope.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var now = DateTimeOffset.UtcNow;

        await publisher.PublishAsync(new ViewingScheduledIntegrationEvent
        {
            ViewingId = Guid.NewGuid(),
            ListingId = homeId,
            LandlordId = landlordProfileId,
            SeekerUserId = seekerUserId,
            ScheduledFor = now.AddDays(3),
            DurationMinutes = 30,
            IsVirtual = false,
            ScheduledAt = now
        });

        await unitOfWork.SaveChangesAsync();
    }

    /// <summary>
    /// Publishes one of each engagement B6 broadcasts, against one item.
    /// <para>
    /// Published as B6's own contracts through the real publisher, so the delivery path, the
    /// deduplication and the ownership join are the production ones.
    /// </para>
    /// </summary>
    private async Task PublishEngagementAsync(Guid contentId, Guid authorId)
    {
        using var scope = Factory.Services.CreateScope();

        var publisher = scope.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var wire = CommunityWireNames.ToWire(CommunityContentType.Guide);

        await publisher.PublishAsync(new CommunityBookmarkAddedIntegrationEvent
        {
            ContentType = wire,
            ContentId = contentId,
            UserId = Guid.NewGuid(),
            ContentAuthorId = authorId
        });

        await publisher.PublishAsync(new CommunityShareRecordedIntegrationEvent
        {
            ShareId = Guid.NewGuid(),
            ContentType = wire,
            ContentId = contentId,
            SharedBy = Guid.NewGuid(),
            Kind = "link",
            ContentAuthorId = authorId
        });

        await publisher.PublishAsync(new CommunityCommentAddedIntegrationEvent
        {
            CommentId = Guid.NewGuid(),
            ContentType = wire,
            ContentId = contentId,
            AuthorId = Guid.NewGuid(),
            ContentAuthorId = authorId,
            Depth = 0
        });

        await publisher.PublishAsync(new CommunityReactionAddedIntegrationEvent
        {
            ContentType = wire,
            ContentId = contentId,
            UserId = Guid.NewGuid(),
            ReactionKey = "helpful",
            Sentiment = "positive",
            ContentAuthorId = authorId
        });

        await unitOfWork.SaveChangesAsync();
    }

    /// <summary>One save of one item, published as B6's contract with the time it happened.</summary>
    private async Task PublishBookmarkAsync(Guid contentId, Guid authorId, DateTimeOffset occurredAt)
    {
        using var scope = Factory.Services.CreateScope();

        var publisher = scope.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        await publisher.PublishAsync(new CommunityBookmarkAddedIntegrationEvent
        {
            ContentType = CommunityWireNames.ToWire(CommunityContentType.Guide),
            ContentId = contentId,
            UserId = Guid.NewGuid(),
            ContentAuthorId = authorId,
            OccurredAt = occurredAt
        });

        await unitOfWork.SaveChangesAsync();
    }

    /// <summary>
    /// The views comparison reason for "Today", from every route that serves one: the creator
    /// summary, the creator dataset, the item's dataset and the item's row on the content page.
    /// </summary>
    private async Task<IReadOnlyList<string?>> ReadTodayReasonsAsync(string authorToken, Guid guideId)
    {
        Api.Authenticate(authorToken);

        var summary = await ReadDataAsync<CreatorSummaryDto>(
            await HttpClient.GetAsync($"{Analytics}/creators/me/summary?period=today"));

        var dataset = await ReadDataAsync<DatasetDto>(
            await HttpClient.GetAsync($"{Analytics}/creators/me?period=today"));

        var page = await ReadDataAsync<ContentPageDto>(
            await HttpClient.GetAsync($"{Analytics}/creators/me/content?period=today"));

        var item = await ReadContentAsync(guideId, authorToken, "today");

        return
        [
            Metric(summary, "content_views").ComparisonUnavailableReason,
            ComparisonReason(dataset, "content.views"),
            page.Items.Single(row => row.ContentId == guideId.ToString())
                .Metrics.Single(metric => metric.Key == "content_views").ComparisonUnavailableReason,
            ComparisonReason(item, "content.views")
        ];
    }

    private static string? ComparisonReason(DatasetDto dataset, string key) =>
        dataset.Metrics.Single(metric => metric.Key == key).Comparison?.UnavailableReason;

    /// <summary>One projection pass, reporting how many raw rows it folded in.</summary>
    private async Task<int> ProjectOnceAsync()
    {
        using var scope = Factory.Services.CreateScope();

        return await scope.ServiceProvider
            .GetRequiredService<AnalyticsProjectionService>()
            .RunOnceAsync();
    }

    /// <summary>Runs the projection to exhaustion, so a read sees everything ingested.</summary>
    private async Task ProjectAsync(int passes = 10)
    {
        for (var pass = 0; pass < passes; pass++)
        {
            using var scope = Factory.Services.CreateScope();

            var projection = scope.ServiceProvider.GetRequiredService<AnalyticsProjectionService>();

            if (await projection.RunOnceAsync() == 0)
            {
                return;
            }
        }
    }

    /// <summary>One item's dataset, read by its owner over the certified route.</summary>
    private async Task<DatasetDto> ReadContentAsync(
        Guid contentId, string ownerToken, string period = "30d")
    {
        Api.Authenticate(ownerToken);

        return await ReadDataAsync<DatasetDto>(
            await HttpClient.GetAsync($"{Analytics}/content/guide/{contentId}?period={period}"));
    }

    /// <summary>
    /// Runs a command as an administrator, and unwraps it.
    /// <para>
    /// Dispatched in a scope rather than over HTTP, and DEBT-031 is why: this suite has no staff
    /// role to grant, so an administrator's token cannot be obtained the way a member's is. The
    /// command still runs through the whole application pipeline — validation, authorisation, the
    /// transaction, the audit behaviour — against the same claims the HTTP layer would have put in
    /// front of it, so what is skipped is the granting rather than the checking.
    /// </para>
    /// </summary>
    private async Task<T> RunPrivilegedAsync<T>(
        Func<MediatR.ISender, Task<Diaspora.SharedKernel.Results.Result<T>>> dispatch)
    {
        var result = await RunPrivilegedRawAsync(dispatch);

        result.IsSuccess.Should().BeTrue($"the operation should have succeeded: {result.Error.Code}");

        return result.Value;
    }

    private async Task<Diaspora.SharedKernel.Results.Result<T>> RunPrivilegedRawAsync<T>(
        Func<MediatR.ISender, Task<Diaspora.SharedKernel.Results.Result<T>>> dispatch)
    {
        using var scope = Factory.Services.CreateScope();

        ScopedCurrentUser.Apply(
            scope.ServiceProvider, CurrentAccessToken, "analytics:operations:run");

        return await dispatch(scope.ServiceProvider.GetRequiredService<MediatR.ISender>());
    }

    private string CurrentAccessToken =>
        HttpClient.DefaultRequestHeaders.Authorization?.Parameter
            ?? throw new InvalidOperationException("The suite has no authenticated caller.");

    private sealed record Member(string Token, Guid UserId);

    private static double Views(CreatorSummaryDto summary) => Metric(summary, "content_views").Value;

    private static MetricDto Metric(CreatorSummaryDto summary, string key) =>
        summary.Metrics.SingleOrDefault(metric => metric.Key == key)
            ?? throw new InvalidOperationException($"The summary did not carry '{key}'.");

    private static double MetricValue(DatasetDto dataset, string key) =>
        dataset.Metrics.SingleOrDefault(metric => metric.Key == key)?.Value.Value ?? 0;

    private async Task<Guid> PublishGuideAsync(string title)
    {
        var draft = await ReadDataAsync<GuideDto>(
            await HttpClient.PostAsJsonAsync(
                GuidesRoot,
                new { title, categoryKey = "getting-started", language = "en", countryCode = "GB" },
                JsonOptions));

        await HttpClient.PutAsJsonAsync(
            $"{GuidesRoot}/{draft.GuideId}",
            new
            {
                title,
                countryCode = "GB",
                sections = new[]
                {
                    new
                    {
                        sectionId = Guid.NewGuid(),
                        title = "What to do",
                        order = 0,
                        blocks = new[]
                        {
                            new
                            {
                                blockId = Guid.NewGuid(),
                                kind = "Markdown",
                                order = 0,
                                content = "Take the form, the fee and one hour of patience."
                            }
                        }
                    }
                }
            },
            JsonOptions);

        var published = await ReadDataAsync<GuideDto>(
            await HttpClient.PostAsJsonAsync(
                $"{GuidesRoot}/{draft.GuideId}/publish",
                new { revisionNotes = "First edition." },
                JsonOptions));

        return published.GuideId;
    }

    private static async Task<T> ReadDataAsync<T>(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue(
            $"the request should have succeeded, but answered {(int)response.StatusCode}: {body}");

        using var document = JsonDocument.Parse(body);

        return document.RootElement.GetProperty("data").Deserialize<T>(JsonOptions)!;
    }

    // ── Wire shapes, re-declared by hand ────────────────────────────────────────────────────

    private sealed record GuideDto(Guid GuideId);

    private sealed record TelemetryResultDto(
        int Accepted, int Duplicate, int Rejected, IReadOnlyList<TelemetryEntryDto> Results);

    private sealed record TelemetryEntryDto(int Index, string Event, string Status, string? Reason);

    private sealed record RangeDto(string Period, string From, string To, string Timezone);

    private sealed record MetricDto(
        string Key,
        string Label,
        double Value,
        string Format,
        double? ChangePercentage,
        double? PreviousValue,
        string? ComparisonUnavailableReason,
        bool HigherIsBetter,
        string Definition,
        bool IsAvailable);

    private sealed record CreatorSummaryDto(
        RangeDto Range,
        RangeDto ComparisonRange,
        IReadOnlyList<MetricDto> Metrics,
        IReadOnlyList<SeriesDto> Series,
        DateTimeOffset GeneratedAt,
        DateTimeOffset? DataAsOf,
        string Freshness,
        string FreshnessNote,
        string DefinitionVersion,
        IReadOnlyList<string> UnavailableMetrics);

    private sealed record SeriesDto(string Key, string Label, string Granularity, IReadOnlyList<PointDto> Points);

    private sealed record PointDto(string At, double Value, bool IsPartialBucket);

    private sealed record ContentPageDto(
        IReadOnlyList<ContentItemDto> Items, string? NextCursor, bool HasMore);

    private sealed record ContentItemDto(
        string ContentId,
        string ContentType,
        string Title,
        DateTimeOffset? PublishedAt,
        string Status,
        IReadOnlyList<MetricDto> Metrics,
        bool IsSponsored);

    private sealed record AccessDto(
        bool CanView,
        bool CanViewFinancial,
        bool CanViewAdministrative,
        bool CanCompare,
        bool CanExport,
        bool CanBuildReports,
        IReadOnlyList<string> SupportedRanges,
        IReadOnlyList<string> SupportedComparisons,
        IReadOnlyList<string> RestrictedMetrics,
        string ReportingTimezone);

    private sealed record DefinitionsDto(string Version, IReadOnlyList<DefinitionDto> Definitions);

    private sealed record DefinitionDto(
        string Key,
        string CatalogueKey,
        string Label,
        string Definition,
        string? Formula,
        string Format,
        bool HigherIsBetter,
        string PrivacyClass,
        string Owner,
        string FreshnessPolicy,
        string TrustClass,
        string Status);

    private sealed record SourcedDto(double Value, string Source);

    private sealed record DatasetMetricDto(
        string Key, string Label, SourcedDto Value, string Format, DatasetComparisonDto? Comparison);

    private sealed record DatasetComparisonDto(
        string Basis, double? BaselineValue, double? ChangePercent, string? UnavailableReason);

    private sealed record DatasetRangeDto(
        string Preset, string From, string To, string Timezone, bool IsDeviceTimezone);

    private sealed record FreshnessDto(
        DateTimeOffset ComputedAt,
        string SourceName,
        string? Methodology,
        IReadOnlyList<string>? Limitations);

    private sealed record EntityRefDto(string EntityType, string EntityId, string Title, string? Route);

    private sealed record LeaderboardRowDto(int Position, EntityRefDto Entity, double Value);

    private sealed record LeaderboardDto(
        string Key, string Label, string MetricKey, IReadOnlyList<LeaderboardRowDto> Rows);

    private sealed record DatasetSeriesDto(string Key, string Granularity, IReadOnlyList<PointDto> Points);

    private sealed record DatasetDto(
        DatasetRangeDto Range,
        string Comparison,
        string Status,
        IReadOnlyList<DatasetMetricDto> Metrics,
        IReadOnlyList<DatasetSeriesDto> Series,
        IReadOnlyList<LeaderboardDto> Leaderboards,
        FreshnessDto Freshness,
        IReadOnlyList<string> UnavailableMetrics);
}

/// <summary>
/// The concurrency test's own host and helpers.
/// <para>
/// A separate harness because the concurrent factory is created per test rather than shared: the
/// suite's ordinary host holds one SQLite connection, and two requests arriving together against it
/// fail inside the authentication middleware before either reaches this module — a 500 that looks
/// exactly like a product defect and is not one.
/// </para>
/// <para>
/// <b>It publishes no content, deliberately.</b> The race under test is the unique index on the
/// deduplication key, which does not depend on analytics knowing whose the item is: a view of
/// unknown content is stored, deduplicated and attributed later. Publishing a guide here would add
/// the whole trust-verification flow to a test about a database constraint.
/// </para>
/// </summary>
internal sealed class AnalyticsConcurrencyHarness
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ConcurrentWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly IdentityApiClient _api;
    private int _phone;

    public AnalyticsConcurrencyHarness(
        ConcurrentWebApplicationFactory factory, HttpClient client, IdentityApiClient api)
    {
        _factory = factory;
        _client = client;
        _api = api;
    }

    public async Task<string> AuthenticateAsync()
    {
        var phone = $"+23490{Interlocked.Increment(ref _phone):D8}";

        await _api.RegisterAsync(new Diaspora.Contracts.Identity.V1.Requests.RegisterRequest
        {
            FullName = "Concurrent Reader",
            PhoneNumber = phone,
            Email = $"member{phone.TrimStart('+')}@diaspora.test",
            Password = "Correct-Horse-Battery-7",
            Nationality = "NG",
            CountryOfResidence = "CA",
            PreferredLanguage = "en",
            PreferredTimeZone = "UTC"
        });

        var code = _factory.OtpCodes.LatestFor(
            phone, Diaspora.Domain.Identity.Verification.OtpPurpose.PhoneVerification);

        var verified = await _api.VerifyPhoneAsync(
            new Diaspora.Contracts.Identity.V1.Requests.VerifyPhoneRequest
            {
                PhoneNumber = phone,
                Code = code,
                Platform = "Android",
                AppVersion = "1.0.0"
            });

        var auth = await IdentityApiClient
            .ReadDataAsync<Diaspora.Contracts.Identity.V1.Responses.AuthenticationResponse>(verified);

        _api.Authenticate(auth.Tokens.AccessToken);

        return auth.Tokens.AccessToken;
    }

    public Task<HttpResponseMessage> PostViewAsync(Guid contentId, DateTimeOffset occurredAt) =>
        _client.PostAsJsonAsync(
            "/api/v1/telemetry/events",
            new
            {
                events = new[]
                {
                    new
                    {
                        @event = "content.viewed",
                        occurredAt,
                        properties = new
                        {
                            contentId = contentId.ToString(),
                            contentType = "guide"
                        }
                    }
                }
            },
            JsonOptions);
}
