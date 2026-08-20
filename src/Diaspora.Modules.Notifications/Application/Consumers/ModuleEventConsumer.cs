using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Events;
using Diaspora.Modules.Notifications.Application.Dispatch;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Application.Consumers;

/// <summary>
/// The base every consumer of another module's contract derives from.
/// <para>
/// <b>It exists to make the consumers boring, and boring is the requirement.</b> A consumer's whole
/// job is to read a published payload and produce triggers. It must not name a template, a category,
/// a priority or a channel — those are rules, and rules are reference data. Giving every consumer
/// the same two-method shape means the architecture test that checks this can be a simple one, and
/// means a new consumer is fifteen lines that are obviously correct.
/// </para>
/// <para>
/// <b>Delivery is at-least-once and out of band.</b> This runs on a background thread, in its own
/// scope, after the producer's transaction committed — possibly minutes later, possibly twice.
/// De-duplication is the dispatcher's, keyed on the source rather than on the event identifier, so a
/// consumer never has to think about it.
/// </para>
/// <para>
/// <b>A consumer that produces no trigger is normal.</b> An event about a member the platform cannot
/// identify, or one that concerns nobody, yields an empty list and nothing happens.
/// </para>
/// </summary>
public abstract class ModuleEventConsumer<TIntegrationEvent> : IntegrationEventHandler<TIntegrationEvent>
    where TIntegrationEvent : IIntegrationEvent
{
    private readonly INotificationDispatcher _dispatcher;

    protected ModuleEventConsumer(INotificationDispatcher dispatcher) => _dispatcher = dispatcher;

    /// <summary>The module that published the contract. Recorded as the message's source.</summary>
    protected abstract string SourceModule { get; }

    /// <summary>
    /// The triggers this event produces — one per person to be told.
    /// <para>
    /// Several is the normal case: an interview being scheduled addresses the candidate and the
    /// employer, with different wording and different urgency, through two rules.
    /// </para>
    /// </summary>
    protected abstract IEnumerable<NotificationTrigger> Translate(TIntegrationEvent integrationEvent);

    protected override async Task HandleAsync(
        TIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        foreach (var trigger in Translate(integrationEvent))
        {
            await _dispatcher.DispatchAsync(trigger, cancellationToken);
        }
    }

    /// <summary>
    /// Builds a trigger with the module and the contract name filled in.
    /// <para>
    /// <b>The namespace-qualified name, not the short one, and that is not tidiness.</b> Jobs and
    /// Marketplace both publish an <c>OfferAcceptedIntegrationEvent</c>; Marketplace and Housing both
    /// publish a <c>ReservationCancelledIntegrationEvent</c>. A rule keyed on the short name would
    /// match both, and a member selling a sofa would receive the template written for a job offer.
    /// </para>
    /// <para>
    /// Taken from the CLR type rather than a literal, so renaming a published contract breaks the
    /// seeded rule at startup rather than silently stopping a notification.
    /// </para>
    /// </summary>
    protected NotificationTrigger Trigger(
        UserId recipientUserId,
        string audience,
        DateTimeOffset occurredAt,
        IReadOnlyDictionary<string, object?> values,
        string? subjectType = null,
        string? subjectId = null,
        DateTimeOffset? subjectMoment = null,
        int? count = null,
        string? groupKey = null) =>
        new()
        {
            EventName = typeof(TIntegrationEvent).FullName!,
            SourceModule = SourceModule,
            Audience = audience,
            RecipientUserId = recipientUserId,
            SubjectType = subjectType,
            SubjectId = subjectId,
            SubjectMoment = subjectMoment,
            Values = values,
            Count = count,
            GroupKey = groupKey,
            OccurredAt = occurredAt
        };

    /// <summary>
    /// Dispatches one trigger.
    /// <para>
    /// Exposed for the consumers that resolve an audience rather than a person and therefore
    /// override <c>HandleAsync</c> — they still route through the one dispatcher, which is what keeps
    /// "the only way to create a notification" true.
    /// </para>
    /// </summary>
    protected Task DispatchOneAsync(
        NotificationTrigger trigger, CancellationToken cancellationToken) =>
        _dispatcher.DispatchAsync(trigger, cancellationToken);

    /// <summary>A values dictionary with the case-insensitive comparer the renderer expects.</summary>
    protected static Dictionary<string, object?> Values() =>
        new(StringComparer.OrdinalIgnoreCase);
}
