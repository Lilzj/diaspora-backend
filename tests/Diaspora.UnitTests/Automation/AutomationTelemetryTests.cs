using System.Net;
using System.Net.Sockets;
using Diaspora.Contracts.Automation.V1.Extensibility;
using Diaspora.Modules.Automation.Domain.Enums;
using Diaspora.Modules.Automation.Domain.Identifiers;
using Diaspora.Modules.Automation.Domain.Runs;
using Diaspora.Modules.Automation;
using Diaspora.Modules.Automation.Observability;
using Diaspora.Operations.Alerting;
using Diaspora.Operations.Resilience;
using Diaspora.Platform.Operations.Alerting;
using Diaspora.Platform.Operations.Resilience;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Automation;

/// <summary>
/// B24 — the two decisions the automation telemetry rests on, isolated from everything else.
/// <para>
/// One is <b>how a failure is classified</b>, which decides both what an operator diagnoses and what
/// a member is told to do about it. The other is <b>who gets to report a run</b>, which decides
/// whether an execution is counted once, twice or never — and which cannot be tested end to end
/// without arranging a race, so it is tested here where the race is a method call.
/// </para>
/// </summary>
public sealed class AutomationTelemetryTests
{
    // ════════════════════════════════════════════════════════════════════════════════════════
    // B24-G40 — failure classification.
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// An exception is classified by what it says, not filed under one label.
    /// <para>
    /// Every action that threw used to be recorded as <c>ActionFailed</c> — §17's "do not classify
    /// everything as <c>Unknown</c>" with a more specific name on it. The consequence was not only a
    /// poor diagnostic: <c>ActionFailed</c> renders to the member as "something went wrong", so a
    /// member whose calendar provider was down and a member who hit a bug were told the same thing
    /// and given the same non-advice.
    /// </para>
    /// </summary>
    [Fact]
    public void A_timeout_a_provider_outage_and_a_rate_limit_are_told_apart()
    {
        var classifier = new AutomationFailureClassifier(new DefaultFailureClassifier());

        classifier.Classify(new TimeoutException())
            .Should().Be(AutomationFailureKind.Timeout);

        classifier.Classify(new CircuitOpenException("calendar"))
            .Should().Be(
                AutomationFailureKind.ProviderUnavailable,
                "the breaker refused the call, so the dependency was not reached and retrying this "
                + "rule is not what fixes it");

        classifier.Classify(new BulkheadFullException("calendar"))
            .Should().Be(
                AutomationFailureKind.BackendUnavailable,
                "this deployment shed the work — the platform, not the provider");

        classifier.Classify(new HttpRequestException("too many", null, HttpStatusCode.TooManyRequests))
            .Should().Be(AutomationFailureKind.RateLimited);

        classifier.Classify(new HttpRequestException("no", null, HttpStatusCode.Unauthorized))
            .Should().Be(
                AutomationFailureKind.IntegrationExpired,
                "an unauthorised provider call is a connection the member has to re-authorise, and "
                + "that is the one they can act on");

        classifier.Classify(new HttpRequestException("boom", null, HttpStatusCode.BadGateway))
            .Should().Be(AutomationFailureKind.ProviderUnavailable);
    }

    /// <summary>
    /// What the exception type cannot say, the platform's own retry classifier decides.
    /// <para>
    /// Reused rather than re-implemented — §10. A socket failure is transient by the platform's
    /// definition and reaches the member as "something the platform depends on did not answer",
    /// which is a different thing from a rule that is wrong.
    /// </para>
    /// </summary>
    [Fact]
    public void An_unrecognised_exception_falls_back_to_the_platforms_own_classification()
    {
        var classifier = new AutomationFailureClassifier(new DefaultFailureClassifier());

        classifier.Classify(new SocketException(10061))
            .Should().Be(
                AutomationFailureKind.BackendUnavailable,
                "transient by the platform's classifier, and a dependency that did not answer");

        classifier.Classify(new OperationCanceledException())
            .Should().Be(
                AutomationFailureKind.Timeout,
                "a cancelled action is the host stopping, and Timeout is the kind whose "
                + "member-facing wording is true of it");

        classifier.Classify(new NotSupportedException("nope"))
            .Should().Be(
                AutomationFailureKind.ActionFailed,
                "a permanent failure of unknown shape is exactly what ActionFailed has always meant");
    }

    /// <summary>
    /// Nothing is ever classified as the member's misconfiguration on the strength of an exception.
    /// <para>
    /// <b>The deliberate omission, asserted so a later change has to argue with a test.</b>
    /// <c>InvalidConfiguration</c> renders to the member as "this rule needs changing", and the
    /// exception types that would tempt it are thrown far more often by this repository's own code
    /// than by a rule a member wrote. Blaming a member for our bug is the one misclassification worse
    /// than none.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData(typeof(ArgumentException))]
    [InlineData(typeof(InvalidOperationException))]
    [InlineData(typeof(FormatException))]
    public void A_platform_bug_is_never_reported_as_the_members_misconfiguration(Type exceptionType)
    {
        var classifier = new AutomationFailureClassifier(new DefaultFailureClassifier());
        var exception = (Exception)Activator.CreateInstance(exceptionType)!;

        classifier.Classify(exception)
            .Should().NotBe(AutomationFailureKind.InvalidConfiguration);
    }

    /// <summary>
    /// A wrapped provider failure is still a provider failure.
    /// <para>
    /// One level of unwrapping, and no more: unwrapping indefinitely classifies a failure by whatever
    /// happened deepest, which is often the least relevant thing about it.
    /// </para>
    /// </summary>
    [Fact]
    public void A_wrapped_exception_is_classified_by_what_it_wraps()
    {
        var classifier = new AutomationFailureClassifier(new DefaultFailureClassifier());

        var wrapped = new InvalidOperationException(
            "dispatch failed", new HttpRequestException("gone", null, HttpStatusCode.ServiceUnavailable));

        classifier.Classify(wrapped).Should().Be(AutomationFailureKind.ProviderUnavailable);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // B24-G48 — the exactly-once report claim.
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// A finished run can be reported once, by whichever path gets there first.
    /// <para>
    /// Three paths finish a run — the engine, a settlement receipt, and the stale-run sweep — and two
    /// of them can be looking at the same row at the same moment. A status check would let both
    /// through; the claim is persisted precisely so the second one loses.
    /// </para>
    /// </summary>
    [Fact]
    public void A_finished_run_may_be_reported_once_and_only_once()
    {
        var run = FinishedRun();
        var now = DateTimeOffset.UtcNow;

        run.TryClaimReport(now).Should().BeTrue("the first caller reports it");
        run.ReportedAt.Should().Be(now);

        run.TryClaimReport(now.AddMinutes(1)).Should().BeFalse(
            "a redelivered settlement racing the sweep must not count a second execution");

        run.ReportedAt.Should().Be(now, "and the first claim's instant stands");
    }

    /// <summary>
    /// A run that has not finished is not reportable.
    /// <para>
    /// §32, from the side that is easy to get wrong. A community broadcast publishes one delivery
    /// request per recipient, so a receipt that settles the first of five leaves the run genuinely
    /// running — and announcing a completion the platform has not reached is the same defect as
    /// announcing one twice, arriving early instead of late.
    /// </para>
    /// </summary>
    [Fact]
    public void A_running_run_is_not_reportable()
    {
        var run = StartedRun();

        run.Status.Should().Be(AutomationRunStatus.Running);

        run.TryClaimReport(DateTimeOffset.UtcNow).Should().BeFalse();
        run.ReportedAt.Should().BeNull();

        // And once it does finish, it becomes reportable — otherwise the assertion above would hold
        // against a method that always refused.
        run.CompleteFromOutcomes(DateTimeOffset.UtcNow);

        run.TryClaimReport(DateTimeOffset.UtcNow).Should().BeTrue();
    }

    /// <summary>
    /// A run with an action still pending stays running, whatever the others did.
    /// <para>
    /// The property the reporter's claim depends on. <c>CompleteFromOutcomes</c> returning early on a
    /// pending action is what stops a five-recipient broadcast being announced when the first
    /// recipient's receipt arrives.
    /// </para>
    /// </summary>
    [Fact]
    public void A_pending_action_holds_the_run_open()
    {
        var run = StartedRun();
        var actionOne = AutomationActionId.New();
        var actionTwo = AutomationActionId.New();

        run.RecordAction(
            actionOne, "notify.self", "Tell me", AutomationActionOutcomeStatus.Succeeded,
            null, AutomationFailureKind.None, null, null, null, null, DateTimeOffset.UtcNow);

        run.RecordAction(
            actionTwo, "notify.self", "Tell them", AutomationActionOutcomeStatus.Pending,
            null, AutomationFailureKind.None, null, null, null, null, null);

        run.CompleteFromOutcomes(DateTimeOffset.UtcNow);

        run.Status.Should().Be(
            AutomationRunStatus.Running, "one recipient is not the whole broadcast");

        run.TryClaimReport(DateTimeOffset.UtcNow).Should().BeFalse();

        run.SettleAction(
            actionTwo, AutomationActionOutcomeStatus.Suppressed, "quiet hours", DateTimeOffset.UtcNow);

        run.CompleteFromOutcomes(DateTimeOffset.UtcNow);

        run.Status.Should().Be(
            AutomationRunStatus.Completed,
            "a member's own preferences holding a message is the feature working, not a failure");

        run.TryClaimReport(DateTimeOffset.UtcNow).Should().BeTrue();
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // B24-G65 — the alert rules, validated where a build can see them.
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// The automation alert rules compose into B17's catalogue.
    /// <para>
    /// <b>The catalogue validates on construction and is constructed on first resolve — which is a
    /// request to the operations endpoint.</b> A rule with no runbook, no metric or a zero window
    /// throws, so without this test a bad rule would ship and surface as a 500 on an operator's
    /// dashboard at the moment they most needed it. Asserting it here moves the failure to a build.
    /// </para>
    /// <para>
    /// Composed alongside the platform's own contributor rather than alone, because the check that
    /// actually matters between two contributors is the duplicate-name one, and a catalogue built
    /// from one contributor can never fail it.
    /// </para>
    /// </summary>
    [Fact]
    public void The_automation_alert_rules_compose_with_the_platforms_own()
    {
        var catalogue = new AlertRuleCatalog(
            [new AutomationAlertRuleContributor(), new PlatformAlertRuleContributor()]);

        var automation = catalogue.ForModule(AutomationModuleMetadata.Name);

        automation.Should().NotBeEmpty("the module declares rules and the catalogue found them");

        automation.Should().OnlyContain(
            rule => !string.IsNullOrWhiteSpace(rule.Runbook),
            "an alert with no stated response gets acknowledged, then ignored, then muted");

        automation.Should().OnlyContain(
            rule => rule.Metric.StartsWith("diaspora.automation.", StringComparison.Ordinal),
            "a rule naming another module's metric is a rule whose owner cannot act on it");

        // Two of them watch for silence rather than for noise, and they are the ones that catch a
        // platform which has stopped working rather than one which is failing. A catalogue with only
        // GreaterThan rules cannot see a disconnected consumer.
        automation.Should().Contain(
            rule => rule.Comparison == AlertComparison.LessThan,
            "an automation platform fails quietly, and a failure rate of zero executions is zero");
    }

    // ── Fixtures ─────────────────────────────────────────────────────────────────────────────

    private static AutomationRun StartedRun() =>
        AutomationRun.Start(
            AutomationId.New(),
            "Tell me about payments",
            Guid.NewGuid(),
            AutomationOwnerKind.User,
            AutomationRunReason.DomainEvent,
            triggerEventId: Guid.NewGuid().ToString("N"),
            triggerKey: "payment.completed",
            triggerSummary: "A payment completed",
            triggeredAt: DateTimeOffset.UtcNow.AddSeconds(-2),
            startedAt: DateTimeOffset.UtcNow,
            correlationId: Guid.NewGuid().ToString("N")[..16],
            isSimulated: false,
            chainDepth: 0,
            chainId: Guid.NewGuid().ToString("N")[..16]);

    private static AutomationRun FinishedRun()
    {
        var run = StartedRun();

        run.RecordAction(
            AutomationActionId.New(), "notify.self", "Tell me",
            AutomationActionOutcomeStatus.Succeeded, null, AutomationFailureKind.None,
            null, null, null, null, DateTimeOffset.UtcNow);

        run.CompleteFromOutcomes(DateTimeOffset.UtcNow);

        return run;
    }
}
