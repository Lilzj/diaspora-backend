using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.Contracts.CommunityLife.V1.Events;
using Diaspora.Contracts.Financial.V1.Events;
using Diaspora.Contracts.Jobs.V1.Events;
using Diaspora.IntegrationTests.Common;
using Diaspora.Platform.Messaging;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// B23's Automation Platform, driven over real HTTP through the same host every other module suite
/// uses, against real persisted rows.
/// <para>
/// <b>Every run asserted here is produced by a fact rather than written by the test.</b> A published
/// contract goes through the real outbox, the engine consumes it, and the run appears in the member's
/// own execution history through the route the client calls. Nothing inserts a run directly, because
/// a suite that did would be asserting that its own fixture reads back — which is the shape of a
/// green test that proves nothing, and is exactly what Wave 4's severest finding was.
/// </para>
/// <para>
/// <b>Nothing here asserts against an empty result alone.</b> Every predicate is exercised from both
/// sides: a condition that matches and one that does not, a member who owns an automation and one who
/// does not, a redelivery that is recognised and an original that is not.
/// </para>
/// <para>
/// The wire shapes are re-declared by hand rather than referenced from the module, for the reason the
/// B12 suite records: binding a contract test to the module's own records makes it pass whenever the
/// module compiles.
/// </para>
/// </summary>
public sealed class AutomationPlatformTests : IdentityTestBase
{
    private const string Automations = "/api/v1/automations";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public AutomationPlatformTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // FRONTEND-PHASE18-GAP-005 — the capability set, and what it never grants.
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// A verified member may automate their own affairs and may not broadcast.
    /// <para>
    /// Both halves matter. The first is the feature; the second is §66, and it is asserted as a
    /// property of the answer rather than of a refusal, because a capability that is never granted
    /// cannot be exercised by any request.
    /// </para>
    /// </summary>
    [Fact]
    public async Task The_capability_set_grants_personal_automation_and_never_platform_wide()
    {
        await NewVerifiedMemberAsync();

        var access = await ReadAsync<AccessDto>(
            await HttpClient.GetAsync($"{Automations}/capabilities"));

        access.CanViewAutomations.Should().BeTrue();
        access.CanCreatePersonal.Should().BeTrue();
        access.Provenance.Should().Be("server");
        access.MaxActiveAutomations.Should().BePositive("a ceiling is rendered before it is hit");

        access.CanCreatePlatformWide.Should().BeFalse(
            "§66: a normal member must not be able to create platform-wide automation");

        access.CanUseLocationTriggers.Should().BeFalse(
            "the app requests only foreground location and there is no geofence backend");

        access.CanNotifyOtherMembers.Should().BeFalse(
            "no scope authority in this deployment grants a broadcast capability");

        // The positive side of the same dividing line: a member holds the capabilities that gate their
        // own affairs, and none of the three that gate reaching another person.
        access.GrantedCapabilities.Should().Contain("automations.financial");
        access.CanUseFinancialTriggers.Should().BeTrue("a member may automate their own money");

        access.GrantedCapabilities.Should().NotContain(
            ["automations.community", "automations.organization", "automations.creator"]);
    }

    /// <summary>
    /// The catalogue tells the truth about what this deployment can do, in both directions.
    /// <para>
    /// A trigger the platform can observe reports available; one whose contract nobody publishes
    /// reports unavailable <i>with a reason</i>. Asserting only the first would let the whole
    /// catalogue drift to "available" and nothing would notice.
    /// </para>
    /// </summary>
    [Fact]
    public async Task The_catalogue_reports_availability_honestly_in_both_directions()
    {
        await NewVerifiedMemberAsync();

        var catalog = await ReadAsync<CatalogDto>(await HttpClient.GetAsync($"{Automations}/catalog"));

        var available = catalog.Triggers.Single(trigger => trigger.Key == "event.updated");
        available.IsAvailable.Should().BeTrue("B12.1 publishes EventUpdated");
        available.UnavailableReason.Should().BeNull();

        // Wave 12 made `provider.leadReceived` available — a lead is a conversation, and B13.2
        // publishes it — so the unavailable half is now asserted against one that genuinely still is.
        // `community.memberJoined` names a scope with no aggregate anywhere in this backend: the
        // frontend's Phase 13 Community Directory has no backend, and Wave 12 deferred it rather than
        // inventing a community to satisfy an automation trigger.
        var unavailable = catalog.Triggers.Single(trigger => trigger.Key == "community.memberJoined");
        unavailable.IsAvailable.Should().BeFalse(
            "no module owns community membership, so nobody publishes the fact");

        unavailable.UnavailableReason.Should().NotBeNullOrWhiteSpace();

        // And the two Wave 12 closed, so a later change that quietly re-broke either fails here.
        catalog.Triggers.Single(trigger => trigger.Key == "provider.leadReceived")
            .IsAvailable.Should().BeTrue("a lead is a provider enquiry, which B13.2 publishes");

        catalog.Triggers.Single(trigger => trigger.Key == "housing.applicationStatusChanged")
            .IsAvailable.Should().BeTrue("Housing publishes every reservation transition since Wave 12");

        catalog.Triggers.Should().NotContain(
            trigger => trigger.Key.StartsWith("government.", StringComparison.Ordinal),
            "Government Data is paused and the vocabulary does not contain the words");
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // FRONTEND-PHASE18-GAP-001 — the registry, its idempotency and its concurrency.
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Creating twice with one key produces one automation.
    /// <para>
    /// The register calls the alternative "silent and compounding": a retried creation gives the
    /// member two identical job alerts and every notification twice, with no obvious cause.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Creating_twice_with_one_idempotency_key_produces_one_automation()
    {
        await NewVerifiedMemberAsync();

        var key = Guid.NewGuid().ToString("N");
        var first = await CreateAutomationAsync("Tell me about jobs", key: key);
        var second = await CreateAutomationAsync("Tell me about jobs", key: key);

        second.Id.Should().Be(first.Id);

        var page = await ReadAsync<PageDto<AutomationDto>>(await HttpClient.GetAsync(Automations));

        page.Items.Count(item => item.Id == first.Id)
            .Should().Be(1, "one key means one automation, however many times it is sent");
    }

    /// <summary>
    /// A different key produces a different automation.
    /// <para>
    /// The other side of the same rule, and the one that catches an idempotency store keyed on
    /// nothing: a member with two genuinely different alerts must get two.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_different_idempotency_key_produces_a_second_automation()
    {
        await NewVerifiedMemberAsync();

        var first = await CreateAutomationAsync("Alert one");
        var second = await CreateAutomationAsync("Alert two");

        second.Id.Should().NotBe(first.Id);
    }

    /// <summary>
    /// Creating without an idempotency key is refused.
    /// <para>
    /// Mandatory rather than optional, because a signature that allowed it to be omitted would let a
    /// caller omit it.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Creating_without_an_idempotency_key_is_refused()
    {
        await NewVerifiedMemberAsync();

        var response = await HttpClient.PostAsJsonAsync(
            Automations, DraftBody("No key", null), JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// An edit under a stale precondition answers 409 rather than overwriting.
    /// <para>
    /// §40. The client shows the difference; a blind retry would discard whatever the other device
    /// wrote, which is the failure the precondition exists to make loud.
    /// </para>
    /// </summary>
    [Fact]
    public async Task An_edit_under_a_stale_precondition_answers_conflict()
    {
        await NewVerifiedMemberAsync();

        var automation = await CreateAutomationAsync("Original");
        var stale = automation.UpdatedAt;

        // The tablet writes first.
        var accepted = await HttpClient.PatchAsJsonAsync(
            $"{Automations}/{automation.Id}",
            new { name = "Changed elsewhere", expectedUpdatedAt = stale },
            JsonOptions);

        accepted.StatusCode.Should().Be(HttpStatusCode.OK);

        // The phone writes second, still holding the value it read before.
        var refused = await HttpClient.PatchAsJsonAsync(
            $"{Automations}/{automation.Id}",
            new { name = "Changed on the phone", expectedUpdatedAt = stale },
            JsonOptions);

        refused.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var current = await ReadAsync<AutomationDto>(
            await HttpClient.GetAsync($"{Automations}/{automation.Id}"));

        current.Name.Should().Be("Changed elsewhere", "the first writer's change survived");
    }

    /// <summary>
    /// Deleting twice answers 200 both times.
    /// <para>
    /// §40 requires the UI never be left showing an automation that is gone, and a 404 on the retry is
    /// exactly how it gets left there.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Deleting_twice_answers_the_same_thing_twice()
    {
        await NewVerifiedMemberAsync();

        var automation = await CreateAutomationAsync("Delete me");

        var first = await HttpClient.DeleteAsync($"{Automations}/{automation.Id}");
        var second = await HttpClient.DeleteAsync($"{Automations}/{automation.Id}");

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.OK, "a retry after a timeout must not 404");
    }

    /// <summary>
    /// Every lifecycle write leaves an audit entry naming the actor.
    /// <para>
    /// §51: "changed by a former moderator" must still be answerable, so the actor's display name is
    /// captured at the time rather than looked up later against a role that may be gone.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Lifecycle_writes_leave_an_audit_trail()
    {
        await NewVerifiedMemberAsync();

        var automation = await CreateAutomationAsync("Audited", activate: true);

        await HttpClient.PatchAsJsonAsync(
            $"{Automations}/{automation.Id}/status", new { status = "PAUSED" }, JsonOptions);

        var audit = await ReadAsync<IReadOnlyList<AuditDto>>(
            await HttpClient.GetAsync($"{Automations}/{automation.Id}/audit"));

        audit.Select(entry => entry.Action).Should().Contain(["created", "activated", "paused"]);
        audit.Should().OnlyContain(entry => entry.Provenance == "server");
        audit.Should().OnlyContain(entry => !string.IsNullOrWhiteSpace(entry.ActorLabel));
        audit.Should().NotContain(entry => entry.ActorLabel.Contains('@', StringComparison.Ordinal));
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Security — isolation, IDOR and privilege escalation.
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// One member's automation is unreachable by another under every verb.
    /// <para>
    /// Not-found rather than forbidden on all of them, because a 403 on somebody else's id confirms
    /// the id exists — which is how an enumeration tool is built out of an authorization check.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Another_members_automation_is_unreachable_under_every_verb()
    {
        await NewVerifiedMemberAsync();
        var mine = await CreateAutomationAsync("Mine");

        // A second, unrelated member.
        await NewVerifiedMemberAsync();

        (await HttpClient.GetAsync($"{Automations}/{mine.Id}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await HttpClient.GetAsync($"{Automations}/{mine.Id}/audit"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await HttpClient.GetAsync($"{Automations}/{mine.Id}/schedule"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await HttpClient.PatchAsJsonAsync(
                $"{Automations}/{mine.Id}", new { name = "Hijacked" }, JsonOptions))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await HttpClient.PatchAsJsonAsync(
                $"{Automations}/{mine.Id}/status", new { status = "DISABLED" }, JsonOptions))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await HttpClient.PostAsJsonAsync(
                $"{Automations}/{mine.Id}/run", new { reason = "manual" }, JsonOptions))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// A list never returns another member's automations, whatever owner the caller names.
    /// <para>
    /// Both sides: the owner sees theirs, and the stranger sees none of them — an assertion against an
    /// empty list alone would pass on a route that returned nothing to anybody.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_list_never_leaks_another_members_automations()
    {
        var owner = await NewVerifiedMemberAsync();
        var mine = await CreateAutomationAsync("Only mine");

        var visible = await ReadAsync<PageDto<AutomationDto>>(
            await HttpClient.GetAsync(Automations));

        visible.Items.Should().Contain(item => item.Id == mine.Id, "the owner sees their own");

        await NewVerifiedMemberAsync();

        var strangerSees = await ReadAsync<PageDto<AutomationDto>>(
            await HttpClient.GetAsync(Automations));

        strangerSees.Items.Should().NotContain(item => item.Id == mine.Id);

        // And naming the owner explicitly does not help.
        var named = await HttpClient.GetAsync($"{Automations}?ownerKind=user&ownerId={owner}");

        named.StatusCode.Should().Be(
            HttpStatusCode.Forbidden,
            "a caller naming a scope they have no role on gets 403, not an empty list");
    }

    /// <summary>
    /// A client cannot create a platform-wide automation by editing the request.
    /// <para>
    /// The client's hidden controls are a courtesy, and a courtesy is not a control.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_client_cannot_create_a_platform_wide_automation()
    {
        await NewVerifiedMemberAsync();

        var response = await HttpClient.PostAsJsonAsync(
            Automations,
            new
            {
                name = "Platform announcement",
                scope = new { ownerKind = "platform", ownerId = Guid.NewGuid() },
                trigger = new { key = "manual.run" },
                actions = new[] { new { key = "notify.self" } },
                status = "DRAFT",
                idempotencyKey = Guid.NewGuid().ToString("N")
            },
            JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// A client cannot create a community automation for a community it has no role on.
    /// </summary>
    [Fact]
    public async Task A_client_cannot_create_a_community_automation_without_a_role()
    {
        await NewVerifiedMemberAsync();

        var response = await HttpClient.PostAsJsonAsync(
            Automations,
            new
            {
                name = "Broadcast",
                scope = new { ownerKind = "community", ownerId = Guid.NewGuid() },
                trigger = new { key = "community.announcementPublished" },
                actions = new[] { new { key = "notify.community" } },
                status = "ACTIVE",
                idempotencyKey = Guid.NewGuid().ToString("N")
            },
            JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// A rule naming a key the catalogue does not have is refused.
    /// <para>
    /// The whole of the "no arbitrary code" guarantee, exercised: a client that invents a trigger, an
    /// action or an operator receives 400, because a permissive registry is how an unauthorized action
    /// key becomes an authorized one later.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("not.a.trigger", "notify.self", "equals")]
    [InlineData("manual.run", "wallet.transfer", "equals")]
    [InlineData("manual.run", "notify.self", "sqlInject")]
    public async Task An_invented_key_is_refused(string trigger, string action, string @operator)
    {
        await NewVerifiedMemberAsync();

        var response = await HttpClient.PostAsJsonAsync(
            Automations,
            new
            {
                name = "Invented",
                trigger = new { key = trigger },
                conditions = new[]
                {
                    new { key = "country", @operator, value = "NG" }
                },
                actions = new[] { new { key = action } },
                status = "DRAFT",
                idempotencyKey = Guid.NewGuid().ToString("N")
            },
            JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// A nested parameter structure is refused.
    /// <para>
    /// A nested structure is where a rules language starts, and the only reliable time to refuse one
    /// is before it is stored.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_nested_parameter_structure_is_refused()
    {
        await NewVerifiedMemberAsync();

        var response = await HttpClient.PostAsJsonAsync(
            Automations,
            new
            {
                name = "Nested",
                trigger = new { key = "manual.run", parameters = """{"a":{"b":"c"}}""" },
                actions = new[] { new { key = "notify.self" } },
                status = "DRAFT",
                idempotencyKey = Guid.NewGuid().ToString("N")
            },
            JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// An automation cannot be switched on against a trigger the platform cannot observe — and can
    /// still be saved as a draft.
    /// <para>
    /// The fail-closed reading, from both sides. A member may keep a rule for a capability that has
    /// not shipped; switching it on is refused with a reason, because a rule that is on and will never
    /// fire is how a member concludes the platform is broken.
    /// </para>
    /// </summary>
    [Fact]
    public async Task An_unobservable_trigger_may_be_drafted_and_may_not_be_activated()
    {
        await NewVerifiedMemberAsync();

        // Wave 12 made `housing.applicationStatusChanged` observable, so the rule is asserted against
        // one that is still not — and against one a *member* may own, since this test creates a user
        // automation and a community trigger would be refused for the owner rather than for the gap.
        // `location.countryChanged` is that trigger: the device observes it in the foreground and the
        // server never claims to, which is the frontend's own position too.
        var draft = await HttpClient.PostAsJsonAsync(
            Automations,
            new
            {
                name = "When I move country",
                trigger = new { key = "location.countryChanged" },
                actions = new[] { new { key = "notify.self" } },
                status = "DRAFT",
                idempotencyKey = Guid.NewGuid().ToString("N")
            },
            JsonOptions);

        draft.StatusCode.Should().Be(HttpStatusCode.Created, "a draft against a future capability is fine");

        var saved = await ReadAsync<AutomationDto>(draft);
        saved.Support.Should().Be("BACKEND_GAP");
        saved.GapIds.Should().Contain("location.countryChanged");

        var activate = await HttpClient.PatchAsJsonAsync(
            $"{Automations}/{saved.Id}/status", new { status = "ACTIVE" }, JsonOptions);

        activate.StatusCode.Should().Be(
            HttpStatusCode.BadRequest, "switching on a rule that can never fire is refused");
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // FRONTEND-PHASE18-GAP-002 · GAP-004 — the engine and execution history, end to end.
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>The whole pipeline in one test.</b> A member configures a rule, a module publishes a
    /// contract, the real outbox delivers it, the engine evaluates the conditions and dispatches the
    /// action, and the run appears in the member's own history through the route the client calls.
    /// Every arrow in the phase brief's diagram is exercised by this single assertion.
    /// </summary>
    [Fact]
    public async Task A_published_fact_runs_a_members_automation_and_appears_in_their_history()
    {
        var member = await NewVerifiedMemberAsync();

        var automation = await CreateAutomationAsync(
            "Tell me when my application moves",
            trigger: "job.applicationStatusChanged",
            action: "notify.self",
            activate: true);

        await PublishAsync(new ApplicationStatusChangedIntegrationEvent
        {
            ApplicationId = Guid.NewGuid(),
            JobId = Guid.NewGuid(),
            EmployerId = Guid.NewGuid(),
            CandidateId = Guid.NewGuid(),
            PreviousStatus = "Submitted",
            CurrentStatus = "Shortlisted",
            IsFirstEmployerResponse = true,
            ChangedAt = DateTimeOffset.UtcNow,
            CandidateUserId = member
        });

        await Factory.DrainOutboxAsync();

        var runs = await ReadAsync<PageDto<RunDto>>(
            await HttpClient.GetAsync($"{Automations}/runs?automationId={automation.Id}"));

        runs.Items.Should().ContainSingle("one published fact produces one run");

        var run = runs.Items[0];
        run.AutomationId.Should().Be(automation.Id);
        run.IsSimulated.Should().BeFalse();
        run.CorrelationId.Should().NotBeNullOrWhiteSpace("a member can read it out to support");
        run.ActionOutcomes.Should().ContainSingle();
        run.ActionOutcomes[0].ActionKey.Should().Be("notify.self");
    }

    /// <summary>
    /// A condition that does not match skips the run, and says so.
    /// <para>
    /// Skipped is the most common non-success in a healthy system — the trigger fired and a condition
    /// said no — and it must never render as a failure.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_condition_that_does_not_match_skips_the_run()
    {
        var member = await NewVerifiedMemberAsync();

        var automation = await CreateAutomationAsync(
            "Only when accepted",
            trigger: "job.applicationStatusChanged",
            action: "notify.self",
            activate: true,
            conditions: [new { key = "applicationStatus", @operator = "equals", value = "Accepted" }]);

        await PublishAsync(new ApplicationStatusChangedIntegrationEvent
        {
            ApplicationId = Guid.NewGuid(),
            JobId = Guid.NewGuid(),
            EmployerId = Guid.NewGuid(),
            CandidateId = Guid.NewGuid(),
            PreviousStatus = "Submitted",

            // Not Accepted.
            CurrentStatus = "Rejected",
            IsFirstEmployerResponse = true,
            ChangedAt = DateTimeOffset.UtcNow,
            CandidateUserId = member
        });

        await Factory.DrainOutboxAsync();

        var runs = await ReadAsync<PageDto<RunDto>>(
            await HttpClient.GetAsync($"{Automations}/runs?automationId={automation.Id}"));

        var run = runs.Items.Should().ContainSingle().Subject;

        run.Status.Should().Be("SKIPPED");
        run.FailureKind.Should().BeNull("a condition saying no is not a failure");
        run.ActionOutcomes.Should().BeEmpty("nothing was attempted");
        run.ConditionOutcomes.Should().ContainSingle();
        run.ConditionOutcomes[0].Matched.Should().BeFalse();
        run.ConditionOutcomes[0].EvaluatedBy.Should().Be("server");
    }

    /// <summary>
    /// A condition's explanation never quotes the member's value.
    /// <para>
    /// A run record is a durable log with a longer retention than the data it would be quoting.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_run_record_never_quotes_a_sensitive_comparison()
    {
        var member = await NewVerifiedMemberAsync();

        await CreateAutomationAsync(
            "Big payments only",
            trigger: "payment.completed",
            action: "notify.self",
            activate: true,
            conditions: [new { key = "amountAbove", @operator = "greaterThan", value = "9999.99" }]);

        await PublishAsync(new PaymentCapturedIntegrationEvent
        {
            PaymentIntentId = Guid.NewGuid(),
            PayerUserId = member,
            AmountMinorUnits = 4_126L,
            Currency = "GBP",
            CapturedAt = DateTimeOffset.UtcNow
        });

        await Factory.DrainOutboxAsync();

        var runs = await ReadAsync<PageDto<RunDto>>(await HttpClient.GetAsync($"{Automations}/runs"));
        var run = runs.Items.Should().ContainSingle().Subject;

        run.Status.Should().Be("SKIPPED");

        var explanation = run.ConditionOutcomes.Should().ContainSingle().Subject.Explanation;

        explanation.Should().NotContain("41.26").And.NotContain("4126");
        explanation.Should().Contain("amountAbove");
        run.TriggerSummary.Should().NotContain("41.26");
    }

    /// <summary>
    /// A redelivered fact produces the same run, marked as a redelivery.
    /// <para>
    /// Delivery is at-least-once. §39's "AlreadyProcessed renders as a completed state rather than an
    /// error" needs the flag to render, and a second run record would make history a
    /// duplicate-delivery log.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_redelivered_fact_finds_its_existing_run()
    {
        var member = await NewVerifiedMemberAsync();

        var automation = await CreateAutomationAsync(
            "Tell me",
            trigger: "payment.completed",
            action: "notify.self",
            activate: true);

        var published = new PaymentCapturedIntegrationEvent
        {
            PaymentIntentId = Guid.NewGuid(),
            PayerUserId = member,
            AmountMinorUnits = 1_000L,
            Currency = "GBP",
            CapturedAt = DateTimeOffset.UtcNow
        };

        await PublishAsync(published);
        await Factory.DrainOutboxAsync();

        // The same contract, same EventId, delivered to the consumer again — which is what an
        // at-least-once transport does after a handler times out and the message is re-claimed.
        //
        // Delivered rather than published a second time, because the outbox has its own uniqueness on
        // the message identity and refuses a duplicate row: the redelivery this asserts happens below
        // the outbox, at the consumer, and that is where the engine's idempotency has to hold.
        await DeliverAsync(published);

        var runs = await ReadAsync<PageDto<RunDto>>(
            await HttpClient.GetAsync($"{Automations}/runs?automationId={automation.Id}"));

        runs.Items.Should().ContainSingle("a redelivery must not produce a second run");
        runs.Items[0].WasDeduplicated.Should().BeTrue("and the client must be able to say so");
    }

    /// <summary>
    /// A paused automation does not run, and a resumed one does.
    /// <para>
    /// Both sides, because a test that only asserted the pause would pass against an engine that never
    /// ran anything.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_paused_automation_does_not_run_and_a_resumed_one_does()
    {
        var member = await NewVerifiedMemberAsync();

        var automation = await CreateAutomationAsync(
            "Pause me",
            trigger: "payment.completed",
            action: "notify.self",
            activate: true);

        await HttpClient.PatchAsJsonAsync(
            $"{Automations}/{automation.Id}/status", new { status = "PAUSED" }, JsonOptions);

        await PublishAsync(Payment(member));
        await Factory.DrainOutboxAsync();

        var whilePaused = await ReadAsync<PageDto<RunDto>>(
            await HttpClient.GetAsync($"{Automations}/runs?automationId={automation.Id}"));

        whilePaused.Items.Should().BeEmpty("a paused automation does not fire");

        await HttpClient.PatchAsJsonAsync(
            $"{Automations}/{automation.Id}/status", new { status = "ACTIVE" }, JsonOptions);

        await PublishAsync(Payment(member));
        await Factory.DrainOutboxAsync();

        var afterResume = await ReadAsync<PageDto<RunDto>>(
            await HttpClient.GetAsync($"{Automations}/runs?automationId={automation.Id}"));

        afterResume.Items.Should().ContainSingle("and a resumed one does");
    }

    /// <summary>
    /// A fact about one member never runs another member's automation.
    /// <para>
    /// The isolation property that matters most at execution: an owner-scoped trigger is narrowed on
    /// the owner set the consumer built, so a stranger watching the same trigger sees nothing.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_fact_about_one_member_never_runs_another_members_automation()
    {
        // The stranger is created first so the owner's token is the live one at the end. The suite
        // authenticates one member at a time, and asserting the owner's own view last is what makes
        // both halves observable in one test.
        await NewVerifiedMemberAsync();
        var theirs = await CreateAutomationAsync(
            "Theirs", trigger: "payment.completed", action: "notify.self", activate: true);

        var owner = await NewVerifiedMemberAsync();
        var mine = await CreateAutomationAsync(
            "Mine", trigger: "payment.completed", action: "notify.self", activate: true);

        // A payment concerning the owner only.
        await PublishAsync(Payment(owner));
        await Factory.DrainOutboxAsync();

        var ownerRuns = await ReadAsync<PageDto<RunDto>>(
            await HttpClient.GetAsync($"{Automations}/runs?automationId={mine.Id}"));

        ownerRuns.Items.Should().ContainSingle("the fact was about them");

        // Asking for somebody else's automation's history is refused rather than answered emptily.
        //
        // <b>Wave 12 strengthened this from an empty page to a 404.</b> Until then the route filtered
        // history on the caller's own identifier, which answered nothing for a foreign automation and
        // — the defect that made the change necessary — also answered nothing for an *organization's*
        // automation to the administrator who owns it, because a scope-owned run carries the scope's
        // identifier rather than any member's. The route now authorizes the named automation through
        // the scope authority, so a foreign one is not-found on the same terms as every other route
        // here, and a scope the caller genuinely holds is readable.
        var strangerRuns = await HttpClient.GetAsync($"{Automations}/runs?automationId={theirs.Id}");

        strangerRuns.StatusCode.Should().Be(
            HttpStatusCode.NotFound,
            "an automation that is not the caller's is not-found rather than empty — a 403 would "
            + "confirm the identifier exists");
    }

    /// <summary>
    /// An event-driven automation cannot be forced to run by hand.
    /// <para>
    /// "Run my community announcement now" is a broadcast with no event behind it, and an engine that
    /// accepted it would let a client manufacture a trigger.
    /// </para>
    /// </summary>
    [Fact]
    public async Task An_event_driven_automation_cannot_be_run_by_hand()
    {
        await NewVerifiedMemberAsync();

        var automation = await CreateAutomationAsync(
            "Event driven", trigger: "payment.completed", action: "notify.self", activate: true);

        var response = await HttpClient.PostAsJsonAsync(
            $"{Automations}/{automation.Id}/run", new { reason = "manual" }, JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>A manual automation can be run by hand, and produces a real run.</summary>
    [Fact]
    public async Task A_manual_automation_runs_when_the_member_asks()
    {
        await NewVerifiedMemberAsync();

        var automation = await CreateAutomationAsync(
            "Run me", trigger: "manual.run", action: "notify.self", activate: true);

        var run = await ReadAsync<RunDto>(
            await HttpClient.PostAsJsonAsync(
                $"{Automations}/{automation.Id}/run",
                new { reason = "manual", idempotencyKey = Guid.NewGuid().ToString("N") },
                JsonOptions));

        run.AutomationId.Should().Be(automation.Id);
        run.IsSimulated.Should().BeFalse();
        run.ActionOutcomes.Should().ContainSingle();
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // FRONTEND-PHASE18-GAP-006 — the dry run.
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// A simulation performs nothing and says so, and cannot be mistaken for a real run.
    /// <para>
    /// §57. The marker is on the record rather than on the caller's expectation, and the refusal to
    /// act is enforced in the dispatcher rather than by the caller passing a flag.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_simulation_performs_nothing_and_is_marked_as_a_simulation()
    {
        await NewVerifiedMemberAsync();

        var simulation = await ReadAsync<SimulationDto>(
            await HttpClient.PostAsJsonAsync(
                $"{Automations}/simulate",
                new
                {
                    name = "Test",
                    trigger = new { key = "manual.run" },
                    actions = new[] { new { key = "notify.self" } }
                },
                JsonOptions));

        simulation.Run.IsSimulated.Should().BeTrue();
        simulation.WouldHaveDone.Should().NotBeEmpty();

        simulation.Run.ActionOutcomes.Should().OnlyContain(
            outcome => outcome.Status == "skipped",
            "a dry run performs nothing, and the record says so rather than claiming success");

        // And no run was persisted.
        var runs = await ReadAsync<PageDto<RunDto>>(await HttpClient.GetAsync($"{Automations}/runs"));

        runs.Items.Should().BeEmpty("a simulation is not execution history");
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // FRONTEND-PHASE18-GAP-008 — reminders.
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// A reminder is scheduled in the subject's own zone, and asking twice produces one.
    /// <para>
    /// The register calls a duplicate here "two identical push notifications a minute apart, which is
    /// the most common way an automation feature loses a member's trust".
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_reminder_is_scheduled_once_however_many_times_it_is_asked_for()
    {
        await NewVerifiedMemberAsync();

        var subject = Guid.NewGuid();
        var moment = DateTimeOffset.UtcNow.AddDays(3);

        var first = await ReadAsync<ReminderDto>(await CreateReminderAsync(subject, moment));
        var second = await ReadAsync<ReminderDto>(await CreateReminderAsync(subject, moment));

        second.ReminderId.Should().Be(first.ReminderId);
        first.State.Should().Be("scheduled");
        first.TimeZoneId.Should().Be("Africa/Lagos", "the subject's own zone, never the device's");
        first.ScheduledFor.Should().NotBeNull();

        var listed = await ReadAsync<IReadOnlyList<ReminderDto>>(
            await HttpClient.GetAsync($"{Automations}/reminders?subjectKind=event&subjectId={subject}"));

        listed.Should().ContainSingle();
    }

    /// <summary>A reminder whose moment has passed is refused rather than delivered late.</summary>
    [Fact]
    public async Task A_reminder_in_the_past_is_refused()
    {
        await NewVerifiedMemberAsync();

        var response = await CreateReminderAsync(
            Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(-5));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// An event that moves rebases every reminder on it; one that is cancelled cancels them.
    /// <para>
    /// The register: "an event rescheduled from Friday to Saturday must cancel the Friday reminder".
    /// </para>
    /// </summary>
    [Fact]
    public async Task An_event_that_moves_rebases_its_reminders()
    {
        await NewVerifiedMemberAsync();

        var subject = Guid.NewGuid();
        var original = DateTimeOffset.UtcNow.AddDays(3);

        var reminder = await ReadAsync<ReminderDto>(await CreateReminderAsync(subject, original));
        var scheduledFor = reminder.ScheduledFor;

        await PublishAsync(new EventUpdatedIntegrationEvent
        {
            CommunityEventId = subject,
            Title = "Moved",
            Slug = "moved",
            StartsAt = original.AddDays(1),
            EndsAt = original.AddDays(1).AddHours(2),
            TimeZoneId = "Africa/Lagos",
            IsMaterialChange = true,
            AffectedAttendeeCount = 1,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        await Factory.DrainOutboxAsync();

        var after = await ReadAsync<IReadOnlyList<ReminderDto>>(
            await HttpClient.GetAsync($"{Automations}/reminders?subjectKind=event&subjectId={subject}"));

        var moved = after.Should().ContainSingle().Subject;

        moved.State.Should().Be("scheduled");
        moved.ScheduledFor.Should().NotBe(scheduledFor, "the event moved, so the reminder moved");
    }

    /// <summary>A cancelled event cancels its reminders rather than firing them.</summary>
    [Fact]
    public async Task A_cancelled_event_cancels_its_reminders()
    {
        await NewVerifiedMemberAsync();

        var subject = Guid.NewGuid();
        var moment = DateTimeOffset.UtcNow.AddDays(3);

        await CreateReminderAsync(subject, moment);

        await PublishAsync(new EventCancelledIntegrationEvent
        {
            CommunityEventId = subject,
            Title = "Cancelled",
            Slug = "cancelled",
            StartsAt = moment,
            AffectedAttendeeCount = 1,
            IsModerationAction = false,
            CancelledAt = DateTimeOffset.UtcNow
        });

        await Factory.DrainOutboxAsync();

        var after = await ReadAsync<IReadOnlyList<ReminderDto>>(
            await HttpClient.GetAsync($"{Automations}/reminders?subjectKind=event&subjectId={subject}"));

        after.Should().ContainSingle().Which.State.Should().Be("cancelled");
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // FRONTEND-PHASE18-GAP-009 — saved searches.
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Saving the same search twice returns the first, and a different one creates a second.
    /// <para>
    /// A duplicate here doubles every alert, so both sides are asserted: the fingerprint has to
    /// collapse genuinely identical criteria and separate genuinely different ones.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Saving_the_same_search_twice_returns_the_first()
    {
        await NewVerifiedMemberAsync();

        var first = await ReadAsync<SavedSearchDto>(
            await SaveSearchAsync("Lagos software", """{"country":"NG","category":"software"}"""));

        // The same filters in a different key order and a different case.
        var same = await ReadAsync<SavedSearchDto>(
            await SaveSearchAsync("Lagos software again", """{"category":"Software","country":"ng"}"""));

        same.Id.Should().Be(first.Id, "the fingerprint is order- and case-insensitive");

        var different = await ReadAsync<SavedSearchDto>(
            await SaveSearchAsync("Abuja software", """{"country":"NG","category":"housing"}"""));

        different.Id.Should().NotBe(first.Id);
    }

    /// <summary>
    /// A newly published job that matches a saved search is matched once, ever.
    /// <para>
    /// The single most visible correctness property of the feature: a member must be told about a job
    /// once even if it is republished. Both sides are exercised — a job that matches and a job that
    /// does not.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_published_job_matches_a_saved_search_once_and_a_mismatch_never()
    {
        await NewVerifiedMemberAsync();

        var search = await ReadAsync<SavedSearchDto>(
            await SaveSearchAsync("Nigerian software", """{"country":"NG","category":"software"}"""));

        var matching = JobPublished("NG", "software");

        await PublishAsync(matching);
        await Factory.DrainOutboxAsync();

        // The same job again — republished, edited, re-indexed, or simply redelivered. Delivered
        // rather than re-published because the outbox refuses a duplicate message identity, and the
        // barrier this asserts is the match row rather than the outbox's.
        await DeliverAsync(matching);

        // And one that does not match.
        await PublishAsync(JobPublished("GB", "housing"));
        await Factory.DrainOutboxAsync();

        var searches = await ReadAsync<IReadOnlyList<SavedSearchDto>>(
            await HttpClient.GetAsync($"{Automations}/saved-searches"));

        var updated = searches.Single(row => row.Id == search.Id);

        updated.NewMatchCount.Should().Be(
            1, "one matching job, counted once however often it is republished");

        updated.LastMatchedAt.Should().NotBeNull();
    }

    /// <summary>One member's saved search is unreachable by another.</summary>
    [Fact]
    public async Task Another_members_saved_search_is_unreachable()
    {
        await NewVerifiedMemberAsync();

        var mine = await ReadAsync<SavedSearchDto>(
            await SaveSearchAsync("Mine", """{"country":"NG"}"""));

        await NewVerifiedMemberAsync();

        var theirs = await ReadAsync<IReadOnlyList<SavedSearchDto>>(
            await HttpClient.GetAsync($"{Automations}/saved-searches"));

        theirs.Should().NotContain(row => row.Id == mine.Id);

        var patched = await HttpClient.PatchAsJsonAsync(
            $"{Automations}/saved-searches/{mine.Id}", new { name = "Hijacked" }, JsonOptions);

        patched.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

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

    private static JobPublishedIntegrationEvent JobPublished(string country, string category) => new()
    {
        JobId = Guid.NewGuid(),
        EmployerId = Guid.NewGuid(),
        EmployerName = "Acme",
        IsEmployerVerified = true,
        Title = $"{category} role in {country}",
        Slug = Guid.NewGuid().ToString("N"),
        CategoryKey = category,
        Tags = [],
        SkillKeys = [],
        CountryCode = country,
        WorkplaceType = "Remote",
        JobType = "FullTime",
        ExperienceLevel = "Mid",
        OffersVisaSponsorship = false,
        OffersRelocationAssistance = false,
        IsSalaryDisclosed = false,
        VacancyCount = 1,
        IsFirstPublication = true,
        PublishedAt = DateTimeOffset.UtcNow
    };

    /// <summary>
    /// Delivers a contract to its consumers again, exactly as a redelivery does.
    /// <para>
    /// The outbox is not involved: it has already delivered this message once and its own uniqueness
    /// on the message identity would refuse a second row. What is being exercised is the consumer's
    /// half of at-least-once — the same event arriving twice at the handler, which is what happens
    /// when a visibility timeout lapses and the message is re-claimed.
    /// </para>
    /// </summary>
    private async Task DeliverAsync<TIntegrationEvent>(TIntegrationEvent integrationEvent)
        where TIntegrationEvent : Diaspora.Contracts.Events.IIntegrationEvent
    {
        using var scope = Factory.Services.CreateScope();

        var publisher = scope.ServiceProvider.GetRequiredService<MediatR.IPublisher>();

        await publisher.Publish(
            new Diaspora.Application.Abstractions.IntegrationEventNotification<TIntegrationEvent>(
                integrationEvent));

        await scope.ServiceProvider
            .GetRequiredService<Diaspora.Domain.Abstractions.IUnitOfWork>()
            .SaveChangesAsync();
    }

    private async Task PublishAsync(Diaspora.Contracts.Events.IIntegrationEvent integrationEvent)
    {
        using var scope = Factory.Services.CreateScope();

        var publisher = scope.ServiceProvider.GetRequiredService<IIntegrationEventPublisher>();
        var unitOfWork = scope.ServiceProvider
            .GetRequiredService<Diaspora.Domain.Abstractions.IUnitOfWork>();

        await publisher.PublishAsync(integrationEvent);
        await unitOfWork.SaveChangesAsync();
    }

    private async Task<AutomationDto> CreateAutomationAsync(
        string name,
        string trigger = "manual.run",
        string action = "notify.self",
        bool activate = false,
        object[]? conditions = null,
        string? key = null)
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
                idempotencyKey = key ?? Guid.NewGuid().ToString("N")
            },
            JsonOptions);

        return await ReadAsync<AutomationDto>(response);
    }

    private Task<HttpResponseMessage> CreateReminderAsync(Guid subject, DateTimeOffset moment) =>
        HttpClient.PostAsJsonAsync(
            $"{Automations}/reminders",
            new
            {
                subjectKind = "event",
                subjectId = subject,
                subjectTitle = "A community meetup",
                subjectMoment = moment,
                offsetMinutes = 60,
                anchor = "start",
                timeZoneId = "Africa/Lagos",
                idempotencyKey = Guid.NewGuid().ToString("N")
            },
            JsonOptions);

    private Task<HttpResponseMessage> SaveSearchAsync(string name, string criteria) =>
        HttpClient.PostAsJsonAsync(
            $"{Automations}/saved-searches",
            new
            {
                subject = "jobs",
                name,
                criteria,
                querySummary = name,
                alertsEnabled = true,
                alertFrequency = "instant",
                watchFor = new[] { "newMatch" }
            },
            JsonOptions);

    private static object DraftBody(string name, string? key) => new
    {
        name,
        trigger = new { key = "manual.run" },
        actions = new[] { new { key = "notify.self" } },
        status = "DRAFT",
        idempotencyKey = key
    };

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        response.IsSuccessStatusCode.Should().BeTrue(
            $"the request should have succeeded; body was {await response.Content.ReadAsStringAsync()}");

        var envelope = await response.Content.ReadFromJsonAsync<Envelope<T>>(JsonOptions);

        envelope.Should().NotBeNull();
        return envelope!.Data;
    }

    private sealed record Envelope<T>(T Data);

    private sealed record PageDto<T>(IReadOnlyList<T> Items, string? NextCursor, int? TotalCount);

    private sealed record AccessDto(
        bool CanViewAutomations,
        bool CanCreatePersonal,
        bool CanCreatePlatformWide,
        bool CanNotifyOtherMembers,
        bool CanUseFinancialTriggers,
        bool CanUseLocationTriggers,
        int? MaxActiveAutomations,
        string Provenance,
        IReadOnlyList<string> GrantedCapabilities);

    private sealed record CatalogDto(
        IReadOnlyList<CatalogEntryDto> Triggers,
        IReadOnlyList<CatalogEntryDto> Conditions,
        IReadOnlyList<CatalogEntryDto> Actions);

    private sealed record CatalogEntryDto(string Key, bool IsAvailable, string? UnavailableReason);

    private sealed record AutomationDto(
        string Id,
        string Name,
        string Status,
        string Support,
        IReadOnlyList<string> GapIds,
        DateTimeOffset UpdatedAt);

    private sealed record AuditDto(string Action, string ActorLabel, string Provenance);

    private sealed record RunDto(
        string Id,
        string AutomationId,
        string Status,
        string TriggerSummary,
        string? FailureKind,
        string? CorrelationId,
        bool IsSimulated,
        bool WasDeduplicated,
        IReadOnlyList<ConditionOutcomeDto> ConditionOutcomes,
        IReadOnlyList<ActionOutcomeDto> ActionOutcomes);

    private sealed record ConditionOutcomeDto(bool Matched, string EvaluatedBy, string Explanation);

    private sealed record ActionOutcomeDto(string ActionKey, string Status, string? SuppressionReason);

    private sealed record SimulationDto(
        RunDto Run, IReadOnlyList<WouldHaveDoneDto> WouldHaveDone);

    private sealed record WouldHaveDoneDto(string ActionKey, string Reason);

    private sealed record ReminderDto(
        string ReminderId, string State, string? ScheduledFor, string TimeZoneId);

    private sealed record SavedSearchDto(
        string Id, string Name, int? NewMatchCount, DateTimeOffset? LastMatchedAt);
}
