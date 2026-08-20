using Diaspora.Contracts.Automation.V1.Extensibility;
using Diaspora.Modules.Automation.Application.Engine;
using Diaspora.Modules.Automation.Domain.Automations;
using Diaspora.Modules.Automation.Domain.Catalog;
using Diaspora.Modules.Automation.Domain.Enums;
using Diaspora.Modules.Automation.Domain.Identifiers;
using Diaspora.Modules.Automation.Domain.Scheduling;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Automation;

/// <summary>
/// B23.x Wave 12 — the vocabulary, the subject store and the two defects the extension exposed.
/// </summary>
public sealed class AutomationExtensionTests
{
    // ── The vocabulary did not grow ─────────────────────────────────────────────────────────

    /// <summary>
    /// Wave 12 made triggers available; it invented none.
    /// <para>
    /// <b>The brief's constraint as an assertion.</b> "Do not turn B23.x into an unrestricted workflow
    /// engine" and "implement only the automation capabilities actually supported by the existing
    /// frontend" are one number: the catalogue's counts are the frontend's, and closing a gap must
    /// change availability rather than size.
    /// </para>
    /// </summary>
    [Fact]
    public void The_vocabulary_is_the_same_size_it_was()
    {
        AutomationCatalog.Triggers.Should().HaveCount(
            49, "the frontend offers 51 and two are government, which stays deferred");

        AutomationCatalog.Conditions.Should().HaveCount(22, "the frontend's condition catalogue");
        AutomationCatalog.Actions.Should().HaveCount(12, "the frontend's action catalogue");
    }

    /// <summary>
    /// The nine triggers Wave 12 closed are available, and the ones it deferred are not.
    /// <para>
    /// Both directions, in one place, so a later change cannot quietly move an entry between them.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("savedSearch.priceChanged")]
    [InlineData("job.closingSoon")]
    [InlineData("listing.expiring")]
    [InlineData("listing.reservationEnding")]
    [InlineData("listing.inquiryReceived")]
    [InlineData("housing.inquiryReceived")]
    [InlineData("housing.applicationStatusChanged")]
    [InlineData("housing.viewingReminder")]
    [InlineData("organization.registrationReceived")]
    [InlineData("provider.leadReceived")]
    [InlineData("conversation.inactive")]
    [InlineData("conversation.unreadReminder")]
    [InlineData("creator.performanceThreshold")]
    public void Wave_twelve_made_these_triggers_available(string key) =>
        AutomationCatalog.FindTrigger(key)!.IsAvailable.Should().BeTrue();

    /// <summary>
    /// The community scope stays deferred, and stays explained.
    /// <para>
    /// It depends on a Community Directory backend that does not exist. Deferring it is the decision;
    /// asserting it is what stops a later phase quietly marking it available without building one.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("community.memberJoined")]
    [InlineData("community.milestoneReached")]
    [InlineData("community.eventCreated")]
    public void The_community_triggers_stay_deferred_and_explained(string key)
    {
        var trigger = AutomationCatalog.FindTrigger(key)!;

        trigger.IsAvailable.Should().BeFalse("no module owns community membership");
        trigger.UnavailableReason.Should().NotBeNullOrWhiteSpace();
    }

    /// <summary>Government stays out of the vocabulary entirely. Unchanged by Wave 12.</summary>
    [Theory]
    [InlineData("government.advisoryUpdated")]
    [InlineData("government.noticePublished")]
    [InlineData("government.embassyChanged")]
    public void No_government_trigger_was_added(string key) =>
        AutomationCatalog.FindTrigger(key).Should().BeNull(
            "Government Data is paused, and an entry would be a promise a feed exists behind it");

    // ── The conditions no payload may answer ────────────────────────────────────────────────

    /// <summary>
    /// A trigger payload can never satisfy an externally-sourced condition.
    /// <para>
    /// <b>The security property behind the read ports.</b> Any module that publishes an event writes
    /// a values bag; if a balance were read from it, any of them could decide whether a member's
    /// balance condition matched. The separation is two dictionaries, and this is the assertion.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("walletBalance", "greaterThan")]
    [InlineData("subscriptionActive", "isTrue")]
    [InlineData("creatorStatus", "equals")]
    [InlineData("metricAbove", "greaterThan")]
    [InlineData("metricBelow", "lessThan")]
    public void A_payload_value_never_satisfies_an_externally_sourced_condition(
        string key, string @operator)
    {
        var context = Context(values: [(key, "999999")]);

        var verdict = AutomationConditionEvaluator.Evaluate(
            Condition(key, @operator, key == "subscriptionActive" ? "true" : "1"), context);

        verdict.Matched.Should().BeFalse(
            "a payload must never be able to answer a condition an owning module owns");

        verdict.Evaluable.Should().BeFalse();
        verdict.Explanation.Should().NotBeNullOrWhiteSpace();
    }

    /// <summary>An answer from the owning module does satisfy it, and both directions are exercised.</summary>
    [Theory]
    [InlineData("412.60", true)]
    [InlineData("0.50", false)]
    public void A_resolved_answer_satisfies_the_condition(string balance, bool matched)
    {
        var context = Context(resolved: [("walletBalance", balance)]);

        var verdict = AutomationConditionEvaluator.Evaluate(
            Condition("walletBalance", "greaterThan", "100"), context);

        verdict.Matched.Should().Be(matched);
        verdict.Evaluable.Should().BeTrue("the owning module answered");
    }

    /// <summary>
    /// A resolved answer's explanation never quotes it.
    /// <para>
    /// A balance is the most sensitive figure this platform holds, and a run record outlives it.
    /// </para>
    /// </summary>
    [Fact]
    public void A_resolved_balance_never_appears_in_the_explanation() =>
        AutomationConditionEvaluator.Evaluate(
                Condition("walletBalance", "greaterThan", "100"),
                Context(resolved: [("walletBalance", "412.60")]))
            .Explanation.Should().NotContain("412.60");

    /// <summary>Every externally-sourced condition names a module that answers for it.</summary>
    [Fact]
    public void Every_externally_sourced_condition_names_its_owner() =>
        AutomationCatalog.Conditions.Values
            .Where(condition => condition.ExternalSource is not null)
            .Should().OnlyContain(condition =>
                condition.ExternalSource == AutomationConditionSources.Entitlement ||
                condition.ExternalSource == AutomationConditionSources.Creator ||
                condition.ExternalSource == AutomationConditionSources.Metric)
            .And.HaveCount(5);

    /// <summary>
    /// An externally-sourced condition is never also payload-evaluable.
    /// <para>
    /// The two are mutually exclusive by construction, and an entry that was both would read from the
    /// payload as soon as no source answered — which is the exact hole the separation exists to close.
    /// </para>
    /// </summary>
    [Fact]
    public void No_condition_is_both_payload_evaluable_and_externally_sourced() =>
        AutomationCatalog.Conditions.Values
            .Where(condition => condition.ExternalSource is not null)
            .Should().OnlyContain(condition => !condition.IsServerEvaluable);

    // ── The subject-horizon store ───────────────────────────────────────────────────────────

    /// <summary>
    /// A horizon that already fired is not re-armed by a republication.
    /// <para>
    /// The most likely duplicate-notification source in the whole extension: a listing republished
    /// after its expiry warning went out must not produce a second one.
    /// </para>
    /// </summary>
    [Fact]
    public void A_horizon_that_already_fired_is_not_rebased()
    {
        var now = new DateTimeOffset(2026, 8, 12, 9, 0, 0, TimeSpan.Zero);
        var horizon = Horizon(now, now.AddDays(30));

        horizon.Complete(now);

        horizon.Rebase(now.AddDays(40), now.AddDays(39), "Still listed", "Still listed", "{}")
            .Should().BeFalse("a member has already been told about this one");

        horizon.Status.Should().Be(AutomationOccurrenceStatus.Completed);
    }

    /// <summary>A pending horizon does move when its subject's date moves.</summary>
    [Fact]
    public void A_pending_horizon_moves_with_its_subject()
    {
        var now = new DateTimeOffset(2026, 8, 12, 9, 0, 0, TimeSpan.Zero);
        var horizon = Horizon(now, now.AddDays(30));

        horizon.Rebase(now.AddDays(40), now.AddDays(39), "Moved", "Moved", "{}")
            .Should().BeTrue();

        horizon.SubjectMoment.Should().Be(now.AddDays(40));
        horizon.DueAt.Should().Be(now.AddDays(39));
        horizon.Status.Should().Be(AutomationOccurrenceStatus.Pending);
    }

    /// <summary>A claim is exclusive until it lapses, which is what makes the sweep safe across instances.</summary>
    [Fact]
    public void One_instance_claims_a_horizon_until_the_claim_lapses()
    {
        var now = new DateTimeOffset(2026, 8, 12, 9, 0, 0, TimeSpan.Zero);
        var horizon = Horizon(now, now.AddDays(30));

        horizon.TryClaim("instance-a", now, TimeSpan.FromMinutes(5)).Should().BeTrue();
        horizon.TryClaim("instance-b", now, TimeSpan.FromMinutes(5)).Should().BeFalse(
            "two instances must not both run one due moment");

        horizon.TryClaim("instance-b", now.AddMinutes(6), TimeSpan.FromMinutes(5)).Should().BeTrue(
            "an instance that died holding a claim must not strand the row");
    }

    /// <summary>A cancelled subject is never resurrected by a later publication.</summary>
    [Fact]
    public void A_cancelled_horizon_stays_cancelled()
    {
        var now = new DateTimeOffset(2026, 8, 12, 9, 0, 0, TimeSpan.Zero);
        var horizon = Horizon(now, now.AddDays(30));

        horizon.Cancel("The listing sold.", now);

        horizon.Rebase(now.AddDays(40), now.AddDays(39), "Back", "Back", "{}")
            .Should().BeFalse();

        horizon.Status.Should().Be(AutomationOccurrenceStatus.Cancelled);
    }

    /// <summary>The owner list round-trips, and empty means anybody.</summary>
    [Fact]
    public void The_owner_list_round_trips()
    {
        var now = new DateTimeOffset(2026, 8, 12, 9, 0, 0, TimeSpan.Zero);
        var seller = Guid.NewGuid();
        var member = Guid.NewGuid();

        var scoped = AutomationSubjectHorizon.Schedule(
            "listing.expiring", AutomationSubjectKinds.Listing, Guid.NewGuid(), "A listing",
            now.AddDays(30), now.AddDays(29), "UTC", [seller, member, Guid.Empty], "{}", "Expiring",
            now);

        scoped.Owners().Should().BeEquivalentTo([seller, member], "empty identifiers are dropped");

        Horizon(now, now.AddDays(30)).Owners().Should().BeEmpty(
            "empty means anybody watching, which is the platform's established reading");
    }

    // ── The reminder subject-kind defect Wave 12 fixed ──────────────────────────────────────

    /// <summary>
    /// Two subjects of different kinds sharing an identifier do not collide.
    /// <para>
    /// <b>The Wave 11 defect.</b> The dispatcher wrote every reminder with the subject kind
    /// <c>event</c>, so the deduplication key — <c>(member, kind, subject, offset, anchor)</c> — was
    /// the same for a job and a viewing that happened to share a GUID, and the second member's
    /// reminder would be suppressed as a duplicate of the first's. Invisible while every reminder was
    /// an event; two lines of consequence the moment four more subjects could be reminded about.
    /// </para>
    /// </summary>
    [Fact]
    public void Two_subject_kinds_sharing_an_identifier_do_not_collide()
    {
        var member = UserId.From(Guid.NewGuid());
        var shared = Guid.NewGuid();

        var asEvent = AutomationReminder.BuildKey(
            member, AutomationSubjectKinds.Event, shared, 60, "start");

        var asViewing = AutomationReminder.BuildKey(
            member, AutomationSubjectKinds.Viewing, shared, 60, "start");

        asViewing.Should().NotBe(asEvent);
    }

    /// <summary>An unrecognised subject kind normalises rather than throwing inside a sweep.</summary>
    [Theory]
    [InlineData("job", AutomationSubjectKinds.Job)]
    [InlineData("JOB", AutomationSubjectKinds.Job)]
    [InlineData("nonsense", AutomationSubjectKinds.Generic)]
    [InlineData(null, AutomationSubjectKinds.Generic)]
    [InlineData("", AutomationSubjectKinds.Generic)]
    public void An_unrecognised_subject_kind_normalises(string? input, string expected) =>
        AutomationSubjectKinds.Normalise(input).Should().Be(expected);

    /// <summary>
    /// A reminder deep-links where its subject lives, and the generic kind links nowhere.
    /// <para>
    /// The other half of the same defect: every reminder used to link into Community Life whatever it
    /// was about.
    /// </para>
    /// </summary>
    [Fact]
    public void A_reminder_links_where_its_subject_lives()
    {
        var subject = Guid.NewGuid();

        // Remediation Wave G: singular. `/jobs` is this platform's API prefix; the client's
        // router serves `/job/[id]`, so the plural form linked to a screen that does not exist.
        AutomationSubjectKinds.DeepLink(AutomationSubjectKinds.Job, subject)
            .Should().StartWith("/job/");

        AutomationSubjectKinds.DeepLink(AutomationSubjectKinds.Housing, subject)
            .Should().StartWith("/housing/");

        AutomationSubjectKinds.DeepLink(AutomationSubjectKinds.Event, subject)
            .Should().StartWith("/community-life/event/");

        AutomationSubjectKinds.DeepLink(AutomationSubjectKinds.Generic, subject)
            .Should().BeNull("an unknown subject has nowhere honest to send a member");
    }

    /// <summary>
    /// A late reminder says what it was about.
    /// <para>
    /// Wave 11's sweeper said "the event had already started" for every subject, which stopped being
    /// true the moment a job closing could also be reminded about.
    /// </para>
    /// </summary>
    [Fact]
    public void A_late_reminder_names_its_own_subject()
    {
        AutomationSubjectKinds.LateReason(AutomationSubjectKinds.Job)
            .Should().Contain("job").And.NotContain("event");

        AutomationSubjectKinds.LateReason(AutomationSubjectKinds.Viewing)
            .Should().Contain("viewing");

        AutomationSubjectKinds.LateReason(AutomationSubjectKinds.Event)
            .Should().Contain("event");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    private static AutomationSubjectHorizon Horizon(DateTimeOffset now, DateTimeOffset moment) =>
        AutomationSubjectHorizon.Schedule(
            "job.closingSoon",
            AutomationSubjectKinds.Job,
            Guid.NewGuid(),
            "A job",
            moment,
            moment.AddDays(-1),
            "UTC",
            [],
            "{}",
            "Closing soon",
            now);

    /// <summary>
    /// A condition, built through an automation, because the factory is internal to the aggregate.
    /// <para>
    /// The same route the evaluator's own suite takes: a rule reaches the evaluator only ever as one
    /// of an automation's conditions, so building it any other way would test a shape that cannot
    /// occur.
    /// </para>
    /// </summary>
    private static AutomationConditionRule Condition(string key, string @operator, string value)
    {
        var automation = AutomationDefinition.Create(
            AutomationOwnerKind.User,
            Guid.NewGuid(),
            "You",
            UserId.New(),
            "Test",
            string.Empty,
            AutomationSource.Builder,
            null,
            "manual.run",
            "{}",
            activateImmediately: false,
            DateTimeOffset.UnixEpoch,
            null);

        automation.ReplaceConditions([(null, key, @operator, value, null)]);
        return automation.Conditions[0];
    }

    private static AutomationTriggerContext Context(
        (string Key, string Value)[]? values = null,
        (string Key, string Value)[]? resolved = null)
    {
        var payload = AutomationTriggerContext.NewValues();
        var answers = AutomationTriggerContext.NewValues();

        foreach (var (key, value) in values ?? [])
        {
            payload[key] = value;
        }

        foreach (var (key, value) in resolved ?? [])
        {
            answers[key] = value;
        }

        return new AutomationTriggerContext
        {
            TriggerKey = "manual.run",
            TriggerEventId = Guid.NewGuid().ToString("N"),
            ConcernedOwnerIds = [],
            Summary = "A test",
            OccurredAt = DateTimeOffset.UtcNow,
            Values = payload,
            ResolvedValues = answers
        };
    }
}
