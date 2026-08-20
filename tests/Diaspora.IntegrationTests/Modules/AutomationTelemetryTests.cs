using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.Contracts.Financial.V1.Events;
using Diaspora.Domain.Abstractions;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Analytics.Domain.Events;
using Diaspora.Persistence;
using Diaspora.Platform.Messaging;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// B24 — Platform &amp; Automation Telemetry, end to end.
/// <para>
/// <b>Every assertion here starts from a published fact and ends at a surface somebody reads.</b> No
/// run is inserted, no analytics row is written by a fixture, and no counter is nudged by the test:
/// a payment is published, the outbox delivers it, the engine runs, the Communication Platform
/// settles the notification, and the figures are read back over the routes a client calls or out of
/// the store a dataset would query. A suite that wrote its own telemetry would be asserting that its
/// fixture reads back.
/// </para>
/// <para>
/// <b>The gap this suite exists to close was reported closed.</b> Wave 11 recorded
/// FRONTEND-PHASE18-GAP-028 as already closed by B19 on the strength of B23 publishing
/// <c>AutomationRunCompleted</c>. Nothing consumed it, the automation vocabulary was unregistered,
/// and two of the three paths on which a run finishes announced nothing at all. Each of those is a
/// test below, and each would have passed vacuously before B24 — which is why every one of them
/// asserts a positive figure rather than the absence of an error.
/// </para>
/// </summary>
public sealed class AutomationTelemetryTests : IdentityTestBase
{
    private const string Automations = "/api/v1/automations";
    private const string Telemetry = "/api/v1/telemetry";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public AutomationTelemetryTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // B24-G05 · B24-G25 · B24-G67 · B24-G72 — the run that finishes on the settlement path.
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// A run whose last action is a notification is counted, timed and announced.
    /// <para>
    /// <b>This is the defect the whole wave turns on.</b> <c>notify.*</c> is the primary automation
    /// action, so the engine leaves the run <c>Running</c> and the Communication Platform's receipt
    /// finishes it minutes later on another thread. That path counted nothing, moved no counter and
    /// published nothing — so the platform's most common automation was the one whose completion was
    /// invisible, and a <c>MaxExecutions</c> ceiling on such a rule could never be reached.
    /// </para>
    /// <para>
    /// Three assertions, deliberately: the run settles, the <em>definition's</em> counters move, and
    /// an analytics row exists. The first passed before B24; the second and third did not.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_run_that_finishes_on_a_notification_receipt_is_counted_and_announced()
    {
        var member = await NewVerifiedMemberAsync();

        var automation = await CreateAutomationAsync(
            "Tell me about payments", trigger: "payment.completed", action: "notify.self");

        await PublishAsync(Payment(member));
        await Factory.DrainOutboxAsync();

        var runs = await ReadAsync<PageDto<RunDto>>(
            await HttpClient.GetAsync($"{Automations}/runs?automationId={automation.Id}"));

        runs.Items.Should().ContainSingle();

        runs.Items[0].Status.Should().NotBe(
            "RUNNING",
            "the settlement receipt finished this run, and a run left running for ever is a member "
            + "watching an automation that never completes");

        runs.Items[0].DurationMs.Should().NotBeNull("a settled run has a duration");

        // The definition's own counters, read over the route the Automation Center calls. These are
        // moved by the reporter now; before B24 they were moved only by the engine, which never saw
        // this run finish.
        var definition = await ReadAsync<AutomationDto>(
            await HttpClient.GetAsync($"{Automations}/{automation.Id}"));

        definition.ExecutionCount.Should().Be(
            1, "the definition's execution count moves wherever the run actually settles");

        definition.LastRunStatus.Should().Be(runs.Items[0].Status);

        // And the handoff to B19: one registered, deduplicated analytics row naming the trigger.
        var stored = await AnalyticsEventsAsync("automation.executed", member);

        stored.Should().ContainSingle("one settled run is one analytics fact");
        stored[0].Surface.Should().Be("payment.completed", "the trigger key travels as the surface");
        stored[0].TrustClass.Should().Be(
            Diaspora.Modules.Analytics.Domain.Enums.AnalyticsTrustClass.Server,
            "an execution is a fact this platform committed, never one a client reported");

        stored[0].OwnerUserId.Should().BeNull(
            "an automation is not content, and attributing a run to its owner would put it in that "
            + "member's creator rollups");
    }

    /// <summary>
    /// A redelivered settlement receipt does not produce a second execution anywhere.
    /// <para>
    /// §30's requirement, asserted at both ends. Delivery is at-least-once, so the receipt arrives
    /// twice; the run's persisted report claim is what stops the second delivery counting, and the
    /// analytics deduplication key composed from the producing contract's own identifier is what
    /// stops it counting again downstream.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_redelivered_settlement_does_not_double_count_an_execution()
    {
        var member = await NewVerifiedMemberAsync();

        var automation = await CreateAutomationAsync(
            "Tell me twice", trigger: "payment.completed", action: "notify.self");

        await PublishAsync(Payment(member));
        await Factory.DrainOutboxAsync();

        var before = await ReadAsync<AutomationDto>(
            await HttpClient.GetAsync($"{Automations}/{automation.Id}"));

        before.ExecutionCount.Should().Be(1, "the first delivery counted, so there is something to double");

        // The same receipt again, delivered below the outbox exactly as a re-claimed message is.
        var receipt = await LastSettlementAsync();

        await DeliverAsync(receipt);

        var after = await ReadAsync<AutomationDto>(
            await HttpClient.GetAsync($"{Automations}/{automation.Id}"));

        after.ExecutionCount.Should().Be(
            1, "a redelivered receipt is not a second execution");

        (await AnalyticsEventsAsync("automation.executed", member)).Should().ContainSingle(
            "and the analytics store deduplicates on the producing contract's own identifier");
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // B24-G01 · B24-G15 · B24-G19 — the lifecycle vocabulary.
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Creating and activating an automation produces the facts a denominator needs.
    /// <para>
    /// An execution count with no activation count cannot tell a template people have just discovered
    /// from a regression: both are a moving line. This is the fact that separates them, and before
    /// B24 the platform published nothing at all about a rule's lifecycle.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Creating_and_activating_an_automation_are_recorded_as_analytics_facts()
    {
        var member = await NewVerifiedMemberAsync();

        var automation = await CreateAutomationAsync(
            "Lifecycle", trigger: "payment.completed", action: "notify.self", activate: false);

        await Factory.DrainOutboxAsync();

        var created = await AnalyticsEventsAsync("automation.created", member);

        created.Should().ContainSingle();
        created[0].Surface.Should().Be("payment.completed");
        created[0].Qualifier.Should().Be("User", "whose scope, as a bounded label — never who");

        // Nothing has been switched on yet, and the absence is asserted rather than assumed: a
        // consumer that wrote both facts on create would make the denominator meaningless.
        (await AnalyticsEventsAsync("automation.activated", member)).Should().BeEmpty(
            "a draft has not been activated");

        var activated = await HttpClient.PatchAsJsonAsync(
            $"{Automations}/{automation.Id}/status", new { status = "ACTIVE" }, JsonOptions);

        activated.StatusCode.Should().Be(HttpStatusCode.OK);

        await Factory.DrainOutboxAsync();

        (await AnalyticsEventsAsync("automation.activated", member)).Should().ContainSingle(
            "switching one on is its own fact");

        // Pausing and disabling stay apart, because members mean different things by them.
        await HttpClient.PatchAsJsonAsync(
            $"{Automations}/{automation.Id}/status", new { status = "PAUSED" }, JsonOptions);

        await Factory.DrainOutboxAsync();

        (await AnalyticsEventsAsync("automation.paused", member)).Should().ContainSingle();
        (await AnalyticsEventsAsync("automation.disabled", member)).Should().BeEmpty(
            "a pause is not a disable, and a vocabulary that merged them would answer neither "
            + "question anybody asks of it");
    }

    /// <summary>
    /// The client's automation vocabulary is accepted where it may be, and refused where it may not.
    /// <para>
    /// FRONTEND-PHASE18-GAP-028, both halves. A member reporting that they opened a builder is a fact
    /// only their device holds; a member reporting that an automation executed would be a client
    /// incrementing a business metric, which §41 forbids outright and which the registry refuses by
    /// trust class rather than by a check somebody remembered to write.
    /// </para>
    /// </summary>
    [Fact]
    public async Task The_client_may_report_builder_interactions_and_may_not_report_executions()
    {
        var member = await NewVerifiedMemberAsync();

        var result = await ReadDataAsync<TelemetryResultDto>(
            await HttpClient.PostAsJsonAsync(
                $"{Telemetry}/events",
                new
                {
                    events = new object[]
                    {
                        new
                        {
                            @event = "automation.builder.started",
                            occurredAt = DateTimeOffset.UtcNow,
                            properties = new { templateId = "job-alerts", source = "template" }
                        },
                        new
                        {
                            @event = "automation.gap.encountered",
                            occurredAt = DateTimeOffset.UtcNow,
                            properties = new
                            {
                                gapId = "FRONTEND-PHASE18-GAP-023",
                                triggerKey = "payment.completed"
                            }
                        },
                        new
                        {
                            @event = "automation.executed",
                            occurredAt = DateTimeOffset.UtcNow,
                            properties = new { triggerKey = "payment.completed" }
                        },
                        new
                        {
                            @event = "automation.center.viewed",
                            occurredAt = DateTimeOffset.UtcNow
                        }
                    }
                },
                JsonOptions));

        result.Accepted.Should().Be(2, "the two the client alone can know");

        result.Results[0].Status.Should().Be("accepted");
        result.Results[1].Status.Should().Be("accepted");

        result.Results[2].Status.Should().Be("rejected");
        result.Results[2].Reason.Should().Be(
            "analytics.ingest.event-not-submittable",
            "an execution is a fact the platform commits; a client posting one would be "
            + "incrementing a business metric");

        result.Results[3].Status.Should().Be("rejected");
        result.Results[3].Reason.Should().Be(
            "analytics.ingest.event-unknown",
            "a surface view is not in the registry, and an unregistered name is refused visibly "
            + "rather than dropped quietly");

        // The accepted rows carry what the client sent, in the two low-cardinality slots the raw
        // store has — and the gap id is the whole point of the second one.
        var gaps = await AnalyticsEventsAsync("automation.gap.encountered", member);

        gaps.Should().ContainSingle();
        gaps[0].Qualifier.Should().Be("FRONTEND-PHASE18-GAP-023");
        gaps[0].Surface.Should().Be("payment.completed");
        gaps[0].TrustClass.Should().Be(
            Diaspora.Modules.Analytics.Domain.Enums.AnalyticsTrustClass.Client,
            "the row records where it came from, which is what that column is for");
    }

    /// <summary>
    /// A retried flush of the same buffered event is recognised rather than counted twice.
    /// <para>
    /// The client sets <c>occurredAt</c> at emit rather than at delivery, so a retried batch composes
    /// the same deduplication identity. That is §30 satisfied without the batch identifier the
    /// register asks for: the identity is the fact, not the delivery.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_retried_client_flush_is_deduplicated_rather_than_counted_twice()
    {
        var member = await NewVerifiedMemberAsync();

        var emitted = DateTimeOffset.UtcNow.AddMinutes(-3);

        var batch = new
        {
            events = new object[]
            {
                new
                {
                    @event = "automation.builder.started",
                    occurredAt = emitted,
                    properties = new { source = "builder" }
                }
            }
        };

        var first = await ReadDataAsync<TelemetryResultDto>(
            await HttpClient.PostAsJsonAsync($"{Telemetry}/events", batch, JsonOptions));

        first.Accepted.Should().Be(1);

        var retried = await ReadDataAsync<TelemetryResultDto>(
            await HttpClient.PostAsJsonAsync($"{Telemetry}/events", batch, JsonOptions));

        retried.Duplicate.Should().Be(1, "the same fact, delivered again, is not a second fact");
        retried.Accepted.Should().Be(0);

        (await AnalyticsEventsAsync("automation.builder.started", member)).Should().ContainSingle();
    }

    /// <summary>
    /// A property bag carrying a count and a flag is accepted, and a nested object is not.
    /// <para>
    /// <b>The contract defect this wave found last and would have shipped without.</b> The client's
    /// sanitiser accepts <c>string | number | boolean</c> and sends each as its natural JSON type, so
    /// <c>automation.tested</c> flushes <c>{ actionCount: 3, wasSimulated: true }</c>. The property
    /// bag was declared as a dictionary of strings, which <c>System.Text.Json</c> refuses to populate
    /// from a number or a boolean — and the refusal is a 400 on the whole batch rather than a
    /// per-event verdict.
    /// </para>
    /// <para>
    /// <b>It was latent rather than new.</b> The allow-list already had <c>position</c> and
    /// <c>isSimulated</c>; no registered event carried either, so nothing had ever sent one.
    /// Registering an event that does is what turned a mismatch nobody could reach into one every
    /// client flush would hit.
    /// </para>
    /// <para>
    /// The second half asserts the refusal that must survive: a nested object is the usual way a
    /// whole DTO with an email address in it reaches a telemetry payload, and accepting one would
    /// make the client's sanitiser advisory.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_numeric_or_boolean_property_is_accepted_and_a_nested_object_is_not()
    {
        var member = await NewVerifiedMemberAsync();

        var accepted = await ReadDataAsync<TelemetryResultDto>(
            await HttpClient.PostAsJsonAsync(
                $"{Telemetry}/events",
                new
                {
                    events = new object[]
                    {
                        new
                        {
                            @event = "automation.tested",
                            occurredAt = DateTimeOffset.UtcNow,
                            properties = new
                            {
                                triggerKey = "payment.completed",
                                actionCount = 3,
                                wasSimulated = true
                            }
                        }
                    }
                },
                JsonOptions));

        accepted.Accepted.Should().Be(
            1, "the client sends a count as a number and a flag as a boolean, and always has");

        var stored = await AnalyticsEventsAsync("automation.tested", member);

        stored.Should().ContainSingle();
        stored[0].Surface.Should().Be("payment.completed");

        // A nested object is refused, and the refusal is the request's shape being wrong rather than
        // one event's content — so it is a 400 rather than a per-event verdict.
        var nested = await HttpClient.PostAsJsonAsync(
            $"{Telemetry}/events",
            new
            {
                events = new object[]
                {
                    new
                    {
                        @event = "automation.tested",
                        occurredAt = DateTimeOffset.UtcNow,
                        properties = new { triggerKey = new { nested = "email@example.com" } }
                    }
                }
            },
            JsonOptions);

        nested.IsSuccessStatusCode.Should().BeFalse(
            "a nested object is the usual way a whole DTO with an email address in it reaches a "
            + "telemetry payload, and accepting one would make the client's sanitiser advisory");
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // B24-G51 — the metric-cardinality defect.
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// A caller cannot mint metric time series by inventing event names.
    /// <para>
    /// <b>The one security defect this wave found in shipped code.</b> Ingestion tagged its received
    /// and rejected counters with the caller's own event string, before anything had checked whether
    /// the platform had heard of it — so an authenticated caller posting a batch of random names
    /// minted two permanent series per name, for as long as they cared to keep posting. §41 names a
    /// metric-cardinality attack in its own test list and §12 forbids unbounded dimensions outright.
    /// </para>
    /// <para>
    /// Asserted by listening to the real meter rather than by reading the code: the tag values that
    /// reach the instrument are what matters, and the refusal itself must stay per-event and precise
    /// in the response body, which the second half checks.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Unregistered_event_names_never_reach_a_metric_dimension()
    {
        await NewVerifiedMemberAsync();

        var observed = new List<string>();

        using var listener = new MeterListener();

        listener.InstrumentPublished = (instrument, active) =>
        {
            if (instrument.Meter.Name == "Diaspora.Analytics")
            {
                active.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<long>((instrument, _, tags, _) =>
        {
            if (instrument.Name is not ("diaspora.analytics.events.received"
                or "diaspora.analytics.events.rejected"))
            {
                return;
            }

            foreach (var tag in tags)
            {
                if (tag.Key == "event" && tag.Value is string value)
                {
                    lock (observed)
                    {
                        observed.Add(value);
                    }
                }
            }
        });

        listener.Start();

        var invented = Enumerable.Range(0, 12)
            .Select(index => $"attack.{Guid.NewGuid():N}.{index}")
            .ToArray();

        var result = await ReadDataAsync<TelemetryResultDto>(
            await HttpClient.PostAsJsonAsync(
                $"{Telemetry}/events",
                new
                {
                    events = invented.Select(name => new
                    {
                        @event = name,
                        occurredAt = DateTimeOffset.UtcNow
                    })
                },
                JsonOptions));

        listener.Dispose();

        result.Rejected.Should().Be(invented.Length, "none of them is registered");

        // The caller still learns exactly which name was refused — an unbounded string costs one
        // response body, which is not the same thing as costing a permanent time series.
        result.Results.Select(entry => entry.Event).Should().BeEquivalentTo(invented);
        result.Results.Should().OnlyContain(
            entry => entry.Reason == "analytics.ingest.event-unknown");

        observed.Should().NotBeEmpty("the counters were exercised, so the assertion below means something");

        observed.Should().OnlyContain(
            value => value == "unregistered",
            "every unrecognised name collapses into one bucket; a caller must not be able to grow "
            + "the metric's dimension by choosing strings");

        observed.Should().NotIntersectWith(invented);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // B24-G57 · B24-G58 · B24-G59 — the diagnostics surface and its scope.
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// The diagnostics summary reports what actually happened, from the run store.
    /// <para>
    /// Both sides of every figure it reports: an automation that completes and one that skips,
    /// because a summary asserted only against a run that succeeded would pass against a handler that
    /// counted every run as a success.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Diagnostics_report_completions_and_skips_apart()
    {
        var member = await NewVerifiedMemberAsync();

        await CreateAutomationAsync(
            "Always", trigger: "payment.completed", action: "notify.self");

        // The same trigger with a condition that cannot match this payment, so the run is skipped
        // rather than failed — the commonest healthy outcome, and the one a success rate must not
        // punish.
        await CreateAutomationAsync(
            "Never",
            trigger: "payment.completed",
            action: "notify.self",
            conditions: [new { key = "country", @operator = "equals", value = "ZZ" }]);

        await PublishAsync(Payment(member));
        await Factory.DrainOutboxAsync();

        var diagnostics = await ReadAsync<DiagnosticsDto>(
            await HttpClient.GetAsync($"{Automations}/diagnostics?days=1"));

        diagnostics.Total.Should().Be(2, "one fact matched two automations");
        diagnostics.Days.Should().Be(1);

        diagnostics.ByStatus.Should().ContainKey("SKIPPED");
        diagnostics.ByStatus["SKIPPED"].Should().Be(1, "the condition said no");

        diagnostics.ByStatus.Should().ContainKey("COMPLETED");
        diagnostics.ByStatus["COMPLETED"].Should().Be(1);

        diagnostics.SuccessRate.Should().Be(
            1.0, "a skip is the feature working, not a failure — both runs succeeded");

        diagnostics.ByFailureKind.Should().BeEmpty(
            "nothing failed, and a map of zeroes is noise pretending to be data");

        diagnostics.DeadLettered.Should().Be(0);
        diagnostics.TopFailingActions.Should().BeEmpty();
        diagnostics.AverageDurationMs.Should().NotBeNull("both runs settled");
    }

    /// <summary>
    /// A member's diagnostics never include another member's runs.
    /// <para>
    /// §24 and §25. Asserted from both sides in one test: the stranger's automation fires on its own
    /// fact and the caller's figures still report only their own, and asking for the stranger's
    /// automation by identifier is not-found rather than answered emptily — a distinguishable refusal
    /// on a foreign identifier is an existence oracle.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Diagnostics_are_scoped_to_the_caller_and_a_foreign_automation_is_not_found()
    {
        var stranger = await NewVerifiedMemberAsync();
        var theirs = await CreateAutomationAsync(
            "Theirs", trigger: "payment.completed", action: "notify.self");

        await PublishAsync(Payment(stranger));
        await Factory.DrainOutboxAsync();

        // Their own figures are real, so the caller's emptiness below is a scope boundary rather
        // than an engine that ran nothing.
        var theirDiagnostics = await ReadAsync<DiagnosticsDto>(
            await HttpClient.GetAsync($"{Automations}/diagnostics?days=1"));

        theirDiagnostics.Total.Should().Be(1);

        var caller = await NewVerifiedMemberAsync();
        await CreateAutomationAsync("Mine", trigger: "payment.completed", action: "notify.self");

        var mine = await ReadAsync<DiagnosticsDto>(
            await HttpClient.GetAsync($"{Automations}/diagnostics?days=1"));

        mine.Total.Should().Be(0, "the stranger's run is not the caller's telemetry");

        var foreign = await HttpClient.GetAsync(
            $"{Automations}/diagnostics?automationId={theirs.Id}&days=1");

        foreign.StatusCode.Should().Be(
            HttpStatusCode.NotFound,
            "an automation that is not the caller's is not-found rather than answered with zeroes");

        // And the caller's own fact still reaches their own figures, so the isolation above is not
        // simply a route that answers nothing to anybody.
        await PublishAsync(Payment(caller));
        await Factory.DrainOutboxAsync();

        var afterMine = await ReadAsync<DiagnosticsDto>(
            await HttpClient.GetAsync($"{Automations}/diagnostics?days=1"));

        afterMine.Total.Should().Be(1);
    }

    /// <summary>
    /// The failure-kind and trigger filters narrow history, and an unknown value does not widen it.
    /// <para>
    /// §21's two new filters. The trigger filter reaches a string column and is therefore the one
    /// checked against the catalogue: a key the catalogue does not have cannot match a stored run, so
    /// refusing it costs nothing and keeps an arbitrary caller-supplied string out of a predicate.
    /// </para>
    /// </summary>
    [Fact]
    public async Task History_can_be_filtered_by_trigger_and_by_failure_kind()
    {
        var member = await NewVerifiedMemberAsync();

        await CreateAutomationAsync(
            "Payments", trigger: "payment.completed", action: "notify.self");

        await PublishAsync(Payment(member));
        await Factory.DrainOutboxAsync();

        var matching = await ReadAsync<PageDto<RunDto>>(
            await HttpClient.GetAsync($"{Automations}/runs?triggerKey=payment.completed"));

        matching.Items.Should().ContainSingle("the run's trigger is this one");

        var other = await ReadAsync<PageDto<RunDto>>(
            await HttpClient.GetAsync($"{Automations}/runs?triggerKey=guide.published"));

        other.Items.Should().BeEmpty("a different catalogue trigger matches nothing");

        var nonsense = await ReadAsync<PageDto<RunDto>>(
            await HttpClient.GetAsync($"{Automations}/runs?triggerKey=' OR 1=1 --"));

        nonsense.Items.Should().ContainSingle(
            "a key the catalogue does not have is not a filter, so the page is unnarrowed rather "
            + "than the string reaching a predicate");

        var timedOut = await ReadAsync<PageDto<RunDto>>(
            await HttpClient.GetAsync($"{Automations}/runs?failureKind=timeout"));

        timedOut.Items.Should().BeEmpty("nothing timed out");

        var none = await ReadAsync<PageDto<RunDto>>(
            await HttpClient.GetAsync($"{Automations}/runs?failureKind=none"));

        none.Items.Should().ContainSingle("the run that succeeded carries no failure kind");
    }

    /// <summary>
    /// Both diagnostics queries render on this deployment's provider.
    /// <para>
    /// <b>Kept because it caught the defect it exists for.</b> The first implementation of the
    /// failing-actions aggregate constructed the domain record inside the projection, which reads
    /// naturally and does not translate — and the failure mode is a 500 on a route rather than a
    /// compile error, so nothing but running it would have found it. Called with an owner that has no
    /// rows on purpose: an empty result still forces the whole expression through the provider's
    /// translator, which is what is being asserted.
    /// </para>
    /// </summary>
    [Fact]
    public async Task The_diagnostics_aggregates_translate_on_this_provider()
    {
        using var scope = Factory.Services.CreateScope();

        var summary = await scope.ServiceProvider
            .GetRequiredService<Diaspora.Modules.Automation.Domain.Repositories.IAutomationRunRepository>()
            .SummariseAsync(
                Guid.NewGuid(), null, null,
                DateTimeOffset.UtcNow.AddDays(-7), DateTimeOffset.UtcNow, topFailingActions: 5);

        summary.Total.Should().Be(0);
        summary.AverageDurationMs.Should().BeNull("an average over no runs is not zero");
        summary.TopFailingActions.Should().BeEmpty();

        var deadLettered = await scope.ServiceProvider
            .GetRequiredService<Diaspora.Modules.Automation.Domain.Repositories.IAutomationOccurrenceRepository>()
            .CountDeadLetteredForOwnerAsync(
                Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(-7), DateTimeOffset.UtcNow);

        deadLettered.Should().Be(0);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The analytics rows one member's activity produced.
    /// <para>
    /// <b>Narrowed to the caller's own hashed actor key, and that is not optional.</b> The suite
    /// shares one database across its tests, so a query for every <c>automation.executed</c> row
    /// would count the rows every other test in this class produced — which passes or fails
    /// depending on execution order, and is the shape of a test that has to be re-run to be believed.
    /// The hash is computed the same way ingestion computes it, so this is the production identity
    /// rather than a test-only join.
    /// </para>
    /// </summary>
    private async Task<IReadOnlyList<AnalyticsEvent>> AnalyticsEventsAsync(
        string eventType, Guid actor)
    {
        using var scope = Factory.Services.CreateScope();

        var actorKey = scope.ServiceProvider
            .GetRequiredService<Diaspora.Modules.Analytics.Application.Services.AnalyticsActorHasher>()
            .Hash(actor);

        return await scope.ServiceProvider
            .GetRequiredService<DiasporaDbContext>()
            .Set<AnalyticsEvent>()
            .AsNoTracking()
            .Where(row => row.EventType == eventType && row.ActorKey == actorKey)
            .ToListAsync();
    }

    /// <summary>
    /// The settlement receipt the Communication Platform published for the last run.
    /// <para>
    /// Rebuilt from the run rather than captured, because the receipt is produced inside B13's own
    /// consumer and never crosses this suite. What matters for the redelivery is that the identity —
    /// run, action, recipient — is the one the platform already settled once.
    /// </para>
    /// </summary>
    private async Task<Diaspora.Contracts.Automation.V1.Events.AutomationDeliverySettledIntegrationEvent>
        LastSettlementAsync()
    {
        using var scope = Factory.Services.CreateScope();

        var runs = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>()
            .Set<Diaspora.Modules.Automation.Domain.Runs.AutomationRun>()
            .AsNoTracking()
            .Include(row => row.ActionOutcomes);

        var run = await runs.OrderByDescending(row => row.StartedAt).FirstAsync();
        var outcome = run.ActionOutcomes[0];

        return new Diaspora.Contracts.Automation.V1.Events.AutomationDeliverySettledIntegrationEvent
        {
            AutomationId = run.AutomationId.Value,
            RunId = run.Id.Value,
            ActionId = outcome.ActionId.Value,
            RecipientUserId = run.OwnerId,
            Accepted = 1
        };
    }

    private async Task<Guid> NewVerifiedMemberAsync()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);
        return auth.User.Id;
    }

    private static PaymentCapturedIntegrationEvent Payment(Guid payer) => new()
    {
        PaymentIntentId = Guid.NewGuid(),
        PayerUserId = payer,
        AmountMinorUnits = 2_500L,
        Currency = "GBP",
        CapturedAt = DateTimeOffset.UtcNow
    };

    private async Task PublishAsync(Diaspora.Contracts.Events.IIntegrationEvent integrationEvent)
    {
        using var scope = Factory.Services.CreateScope();

        await scope.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>()
            .PublishAsync(integrationEvent);

        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
    }

    /// <summary>Delivers a contract to its consumers again, exactly as a re-claimed message does.</summary>
    private async Task DeliverAsync<TIntegrationEvent>(TIntegrationEvent integrationEvent)
        where TIntegrationEvent : Diaspora.Contracts.Events.IIntegrationEvent
    {
        using var scope = Factory.Services.CreateScope();

        await scope.ServiceProvider.GetRequiredService<MediatR.IPublisher>()
            .Publish(
                new Diaspora.Application.Abstractions.IntegrationEventNotification<TIntegrationEvent>(
                    integrationEvent));

        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
    }

    private async Task<AutomationDto> CreateAutomationAsync(
        string name,
        string trigger,
        string action,
        bool activate = true,
        object[]? conditions = null)
    {
        var response = await HttpClient.PostAsJsonAsync(
            Automations,
            new
            {
                name,
                trigger = new { key = trigger },
                conditions = conditions ?? [],
                actions = new[] { new { key = action } },
                status = activate ? "ACTIVE" : "DRAFT",
                idempotencyKey = Guid.NewGuid().ToString("N")
            },
            JsonOptions);

        return await ReadAsync<AutomationDto>(response);
    }

    /// <summary>
    /// Unwraps the envelope, and puts the body in the message when it is not one.
    /// <para>
    /// <c>EnsureSuccessStatusCode</c> alone reports "500" and throws the server's own explanation
    /// away, which turns every failure in this suite into a second run with a debugger attached.
    /// </para>
    /// </summary>
    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue(
            $"the request should have succeeded, and answered {(int)response.StatusCode}: {body}");

        return JsonSerializer.Deserialize<EnvelopeDto<T>>(body, JsonOptions)!.Data;
    }

    private static Task<T> ReadDataAsync<T>(HttpResponseMessage response) => ReadAsync<T>(response);

    private sealed record EnvelopeDto<T>(T Data);

    private sealed record PageDto<T>(IReadOnlyList<T> Items, string? NextCursor);

    private sealed record RunDto(string Id, string Status, long? DurationMs, bool WasDeduplicated);

    private sealed record AutomationDto(
        string Id, int ExecutionCount, string? LastRunStatus, string? LastRunAt);

    private sealed record DiagnosticsDto(
        string From,
        string To,
        int Days,
        int Total,
        IReadOnlyDictionary<string, int> ByStatus,
        IReadOnlyDictionary<string, int> ByFailureKind,
        double? SuccessRate,
        double? AverageDurationMs,
        long? SlowestDurationMs,
        int Deduplicated,
        int DeadLettered,
        IReadOnlyList<ActionFailureDto> TopFailingActions);

    private sealed record ActionFailureDto(string ActionKey, int Failures);

    private sealed record TelemetryResultDto(
        int Accepted, int Duplicate, int Rejected, IReadOnlyList<TelemetryEntryDto> Results);

    private sealed record TelemetryEntryDto(int Index, string Event, string Status, string? Reason);
}
