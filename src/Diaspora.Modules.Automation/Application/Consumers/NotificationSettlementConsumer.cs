using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Automation.V1.Events;
using Diaspora.Modules.Automation.Domain.Enums;
using Diaspora.Modules.Automation.Domain.Identifiers;
using Diaspora.Modules.Automation.Domain.Repositories;
using Diaspora.Modules.Automation.Observability;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Automation.Application.Consumers;

/// <summary>
/// Closes a run's notification action from the Communication Platform's own receipt.
/// <para>
/// <b>This is what makes an honest run record possible.</b> A notification action is recorded as
/// <c>pending</c> the moment its request is published, because the platform has not yet decided
/// anything — the member's preferences, their quiet hours, their digest window and the de-duplication
/// window are all B13's, applied at dispatch, per recipient, in the recipient's own timezone. Claiming
/// a terminal status before that answer arrived would be the frontend's §77 violation committed on
/// the server.
/// </para>
/// <para>
/// <b>Suppressed is not failed, and the distinction is the whole point.</b> A member's own settings
/// deciding nothing should be sent is the feature working. §36 and §38 both depend on the run record
/// telling those apart, and rendering a suppression as a failure would teach members to distrust the
/// feature that was correctly obeying them.
/// </para>
/// <para>
/// <b>A redelivered receipt settles nothing twice.</b> The aggregate only settles an outcome that is
/// still pending, so the second delivery finds nothing to do and returns — which is the normal answer
/// to an at-least-once receipt and must not be an error.
/// </para>
/// <para>
/// <b>B24: this is also where most automation runs actually finish, and until now finishing here was
/// silent.</b> <c>notify.*</c> is the primary automation action, so the engine leaves the run
/// <c>Running</c> and this consumer completes it minutes later — without counting it, without timing
/// it, without moving the definition's execution count, and without announcing it to anything
/// downstream. Every one of those is now done by <c>AutomationRunReporter</c>, which claims the run
/// exactly once so a redelivered receipt racing the stale-run sweep cannot report it twice.
/// </para>
/// </summary>
public sealed class AutomationNotificationSettledConsumer
    : IntegrationEventHandler<AutomationDeliverySettledIntegrationEvent>
{
    private readonly IAutomationRunRepository _runs;
    private readonly AutomationRunReporter _reporter;
    private readonly IClock _clock;
    private readonly ILogger<AutomationNotificationSettledConsumer> _logger;

    public AutomationNotificationSettledConsumer(
        IAutomationRunRepository runs,
        AutomationRunReporter reporter,
        IClock clock,
        ILogger<AutomationNotificationSettledConsumer> logger)
    {
        _runs = runs;
        _reporter = reporter;
        _clock = clock;
        _logger = logger;
    }

    protected override async Task HandleAsync(
        AutomationDeliverySettledIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var run = await _runs.FindForPendingActionAsync(
            AutomationRunId.From(integrationEvent.RunId), cancellationToken);

        if (run is null)
        {
            // A receipt for a run that no longer exists — the retention horizon passed, or the run
            // was never written because its transaction rolled back. Neither is an error worth
            // retrying: the outbox would redeliver forever against a row that is not coming back.
            _logger.LogDebug(
                "Settlement receipt for run {RunId} found no run.", integrationEvent.RunId);

            return;
        }

        var settled = run.SettleAction(
            AutomationActionId.From(integrationEvent.ActionId),
            integrationEvent.Accepted > 0
                ? AutomationActionOutcomeStatus.Succeeded
                : AutomationActionOutcomeStatus.Suppressed,
            integrationEvent.Accepted > 0 ? null : integrationEvent.SuppressionReason,
            _clock.UtcNow);

        if (!settled)
        {
            // Already settled. The normal answer to a redelivery.
            return;
        }

        AutomationTelemetry.ActionCompleted(
            "notify",
            integrationEvent.Accepted > 0
                ? nameof(AutomationActionOutcomeStatus.Succeeded)
                : nameof(AutomationActionOutcomeStatus.Suppressed));

        // The run may still have other actions outstanding — a community broadcast publishes one
        // request per recipient. CompleteFromOutcomes returns without settling while any remain, so
        // this is safe to call on every receipt and correct on the last one.
        var now = _clock.UtcNow;

        run.CompleteFromOutcomes(now);

        // Reports only when the run actually reached a terminal state on this receipt, which is what
        // the claim inside the reporter decides. A receipt that settled one of five broadcast
        // recipients leaves the run running and announces nothing — §32, from the side that is easy
        // to get wrong: not announcing early is as much a correctness property as not announcing
        // twice.
        await _reporter.ReportAsync(run, automation: null, now, cancellationToken);

        await _runs.SaveChangesAsync(cancellationToken);
    }
}
