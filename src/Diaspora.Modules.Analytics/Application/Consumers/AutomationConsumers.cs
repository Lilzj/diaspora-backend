using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Automation.V1.Events;
using Diaspora.Modules.Analytics.Application.Ingestion;
using Diaspora.Modules.Analytics.Domain.Policies;

namespace Diaspora.Modules.Analytics.Application.Consumers;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// AUTOMATION — B24-G01 · B24-G18 · B24-G67 · FRONTEND-PHASE18-GAP-028
//
// **The gap this closes was reported closed.** Wave 11's final report records GAP-028 as "ALREADY
// CLOSED BY B19 — none needed; B23 publishes `AutomationRunCompleted`". B23 does publish it. Nothing
// consumed it:
//
//     $ grep -rn "AutomationRunCompletedIntegrationEvent" src/ tests/
//     …/AutomationIntegrationEvents.cs:161:  (declaration)
//     …/AutomationEngine.cs:399:            (publication)
//
// Two hits. The fact entered the outbox, was dispatched, found no handler and was marked delivered —
// which is silent, correct behaviour for a message nobody wants, and meant no figure anywhere counted
// an automation execution. These two consumers are the missing half.
//
// ── Why this lives in the analytics module and not in the automation module ──────────────────────
//
// A module publishes a fact and stops; whoever needs it consumes it. Putting the ingestion call in
// the automation engine would give the automation platform a reference on the analytics platform and
// make it responsible for a store it does not own — which is the shape §2 warns about, arriving from
// the other direction. This is the twelfth consumer in this file's neighbourhood and it is built like
// the other eleven.
//
// ── What is deliberately not attributed ─────────────────────────────────────────────────────────
//
// **`OwnerUserId` is left null on every row below, and that is the important line in this file.**
// The analytics projection walks owner cells to rebuild creator rollups; attributing an automation
// run to its owner would create an `owner_daily` row for somebody whose automation fired, alongside
// the rows that describe how their *content* performed. An automation is not content — it is not
// published, not readable by anybody else, and not a thing an audience engages with — and a run
// appearing in a creator's content analytics would be a category error with a number attached.
//
// The owner still travels, as the hashed actor, which is what makes a future per-owner automation
// dataset possible without any of these rows being rewritten.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Records an automation run.
/// <para>
/// <b>One fact per run, and exactly one.</b> The producing contract is announced once per run —
/// B24's reporter claims each run before publishing, so three terminal paths cannot announce the same
/// one twice — and a redelivery of that announcement composes the same deduplication key from the
/// contract's own <c>EventId</c>. §30's requirement, satisfied at both ends: the producer does not
/// double-announce and the consumer does not double-count.
/// </para>
/// <para>
/// <b>Executed and failed are two event types over one fact, chosen from the run's terminal
/// status.</b> The client's vocabulary has both names and a dataset keyed on them needs both to
/// exist. <c>Skipped</c> is deliberately an execution rather than a failure: the trigger fired and a
/// condition said no, which is the commonest outcome in a healthy platform, and a success rate that
/// counted it as a failure would tell an operator the engine was broken every time it worked.
/// </para>
/// </summary>
public sealed class AutomationRunAnalyticsConsumer
    : IntegrationEventHandler<AutomationRunCompletedIntegrationEvent>
{
    private readonly AnalyticsIngestionService _ingestion;

    public AutomationRunAnalyticsConsumer(AnalyticsIngestionService ingestion) =>
        _ingestion = ingestion;

    protected override async Task HandleAsync(
        AutomationRunCompletedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var failed = integrationEvent.Status is "Failed" or "TimedOut" or "PartiallyCompleted";

        await _ingestion.IngestAsync(
            new AnalyticsFact(
                failed ? AnalyticsEventTypes.AutomationFailed : AnalyticsEventTypes.AutomationExecuted,
                Version: 1,
                SourceModule: "Automation",
                integrationEvent.OccurredAt,

                // The owner as the actor, hashed before storage. Never as the content owner — see
                // the note at the top of this file for why that distinction is load-bearing.
                ActorUserId: integrationEvent.OwnerId,

                // The trigger key: catalogue vocabulary, bounded, and the dimension every automation
                // question is asked along. Never a trigger *parameter*.
                Surface: integrationEvent.TriggerKey,

                // The terminal status on a success, the failure kind on a failure. One column
                // answering "what happened" for both, because a row is only ever one of the two.
                Qualifier: failed
                    ? integrationEvent.FailureKind ?? integrationEvent.Status
                    : integrationEvent.Status,

                SourceEventId: integrationEvent.EventId),
            cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Records an automation being created, switched on, paused, switched off or removed.
/// <para>
/// <b>The denominator.</b> An execution count with no activation count cannot distinguish a template
/// people have just discovered from a regression: both look like a moving line. This is the fact that
/// separates them, and it is the reason B24 added a fifth published contract to a module whose
/// discipline was four.
/// </para>
/// <para>
/// <b>A creation from a template writes two rows.</b> "How many automations exist" and "how many came
/// from templates" are different questions, and deriving the second from the first would need the
/// template id on every row of the first — which is a column for the sake of a query, on a table
/// that already has a qualifier.
/// </para>
/// </summary>
public sealed class AutomationLifecycleAnalyticsConsumer
    : IntegrationEventHandler<AutomationLifecycleChangedIntegrationEvent>
{
    private readonly AnalyticsIngestionService _ingestion;

    public AutomationLifecycleAnalyticsConsumer(AnalyticsIngestionService ingestion) =>
        _ingestion = ingestion;

    protected override async Task HandleAsync(
        AutomationLifecycleChangedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var eventType = EventTypeFor(integrationEvent.Change);

        if (eventType is null)
        {
            // A change this vocabulary does not name. Ignored rather than stored under a guess: a row
            // whose event type was inferred is a row no dataset can trust, and the producing module
            // is free to grow its own vocabulary without this one having to keep up.
            return;
        }

        await _ingestion.IngestAsync(
            new AnalyticsFact(
                eventType,
                Version: 1,
                SourceModule: "Automation",
                integrationEvent.OccurredAt,
                ActorUserId: integrationEvent.OwnerId,
                Surface: integrationEvent.TriggerKey,

                // Whose scope, as a bounded label — `User`, `Community`, `Organization`. A community
                // automation and a personal one are different products and are read separately;
                // neither the community nor the person is named.
                Qualifier: integrationEvent.OwnerKind,
                SourceEventId: integrationEvent.EventId),
            cancellationToken).ConfigureAwait(false);

        if (integrationEvent.Change != AutomationLifecycleChanges.Created ||
            string.IsNullOrWhiteSpace(integrationEvent.TemplateId))
        {
            return;
        }

        await _ingestion.IngestAsync(
            new AnalyticsFact(
                AnalyticsEventTypes.AutomationTemplateUsed,
                Version: 1,
                SourceModule: "Automation",
                integrationEvent.OccurredAt,
                ActorUserId: integrationEvent.OwnerId,
                Surface: integrationEvent.TriggerKey,
                Qualifier: integrationEvent.TemplateId,

                // The same producing event as the row above, so the two would collide on a key
                // composed from it alone. They do not: the deduplication key is composed from the
                // event *type* and the source id together, which is what lets one published fact
                // produce more than one analytics row without either being lost.
                SourceEventId: integrationEvent.EventId),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The event name for a lifecycle change, or null when this vocabulary does not name it.
    /// </summary>
    private static string? EventTypeFor(string change) => change switch
    {
        AutomationLifecycleChanges.Created => AnalyticsEventTypes.AutomationCreated,
        AutomationLifecycleChanges.Activated => AnalyticsEventTypes.AutomationActivated,
        AutomationLifecycleChanges.Paused => AnalyticsEventTypes.AutomationPaused,
        AutomationLifecycleChanges.Disabled => AnalyticsEventTypes.AutomationDisabled,
        AutomationLifecycleChanges.Deleted => AnalyticsEventTypes.AutomationDeleted,
        _ => null
    };
}

/// <summary>
/// The change values the automation platform publishes.
/// <para>
/// Restated here rather than referenced, because the alternative is this module taking a reference on
/// the automation module to read five strings — which is the module boundary the whole contracts
/// assembly exists to prevent. The producing contract documents the same five, and an architecture
/// test asserts the two lists agree.
/// </para>
/// </summary>
internal static class AutomationLifecycleChanges
{
    public const string Created = "created";
    public const string Activated = "activated";
    public const string Paused = "paused";
    public const string Disabled = "disabled";
    public const string Deleted = "deleted";
}
