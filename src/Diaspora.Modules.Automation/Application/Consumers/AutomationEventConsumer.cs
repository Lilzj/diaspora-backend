using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Events;
using Diaspora.Modules.Automation.Application.Engine;
using Diaspora.Modules.Automation.Application.Services;
using Diaspora.Modules.Automation.Domain.Enums;
using Diaspora.Modules.Automation.Domain.Repositories;
using Diaspora.Modules.Automation.Domain.Scheduling;

namespace Diaspora.Modules.Automation.Application.Consumers;

/// <summary>
/// The base every consumer of another module's contract derives from.
/// <para>
/// <b>It exists to make the consumers boring, and boring is the requirement.</b> A consumer's whole
/// job is to read a published payload and produce trigger contexts. It must not name a condition, an
/// action, a recipient or a template — those are the member's rule, and the rule is read by the
/// engine. Giving every consumer the same two-method shape means a new one is twenty obviously
/// correct lines, and means the architecture test that checks this can be simple.
/// </para>
/// <para>
/// <b>Delivery is at-least-once and out of band.</b> This runs on a background thread, in its own
/// scope, after the producer's transaction committed — possibly minutes later, possibly twice. The
/// event's own <c>EventId</c> is the idempotency key and the engine keys its run on it, so a
/// consumer never has to think about redelivery.
/// </para>
/// <para>
/// <b>A consumer that produces no context is normal.</b> An event about a member the platform cannot
/// identify, or one that concerns nobody with an automation, yields an empty list and nothing happens.
/// </para>
/// </summary>
public abstract class AutomationEventConsumer<TIntegrationEvent>
    : IntegrationEventHandler<TIntegrationEvent>
    where TIntegrationEvent : IIntegrationEvent
{
    private readonly AutomationEngine _engine;

    protected AutomationEventConsumer(AutomationEngine engine) => _engine = engine;

    /// <summary>
    /// The engine, for a consumer whose owner set needs an asynchronous read before it can be built —
    /// a saved-search match, a saver list. Such a consumer overrides <c>HandleAsync</c> and yields
    /// nothing from <c>Translate</c>.
    /// </summary>
    protected AutomationEngine Engine => _engine;

    /// <summary>
    /// The trigger contexts this event produces — one per catalogue key it satisfies.
    /// <para>
    /// Several is the normal case: an application being submitted is <c>job.applicationSubmitted</c>
    /// for the employer and <c>job.applicationThreshold</c> for the same employer under a different
    /// rule. One consumer per contract, several contexts, which keeps a single settlement owner per
    /// event and keeps DSP1003 satisfied.
    /// </para>
    /// </summary>
    protected abstract IEnumerable<AutomationTriggerContext> Translate(TIntegrationEvent integrationEvent);

    protected override async Task HandleAsync(
        TIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        foreach (var context in Translate(integrationEvent))
        {
            await _engine.ObserveAsync(context, cancellationToken);
        }
    }

    /// <summary>
    /// Builds a context with the event's identity already filled in.
    /// <para>
    /// <paramref name="triggerKey"/> is appended to the event id so two triggers over one contract
    /// produce two runs rather than colliding on one idempotency key — an employer who watches both
    /// <c>applicationSubmitted</c> and <c>applicationThreshold</c> must get both.
    /// </para>
    /// </summary>
    protected static AutomationTriggerContext Context(
        string triggerKey,
        Guid eventId,
        IReadOnlyList<Guid> concernedOwnerIds,
        string summary,
        DateTimeOffset occurredAt,
        string? subjectType = null,
        Guid? subjectId = null,
        string? subjectTitle = null,
        DateTimeOffset? subjectMoment = null,
        string? subjectTimeZoneId = null,
        IReadOnlyDictionary<string, string?>? values = null) =>
        new()
        {
            TriggerKey = triggerKey,
            TriggerEventId = $"{eventId:N}:{triggerKey}",
            ConcernedOwnerIds = concernedOwnerIds,
            SubjectType = subjectType,
            SubjectId = subjectId,
            SubjectTitle = subjectTitle,
            SubjectMoment = subjectMoment,
            SubjectTimeZoneId = subjectTimeZoneId,
            Summary = summary,
            OccurredAt = occurredAt,
            Values = values ?? AutomationTriggerContext.NewValues()
        };

    /// <summary>A values dictionary with the comparer the evaluator expects.</summary>
    protected static Dictionary<string, string?> Values() => AutomationTriggerContext.NewValues();

    /// <summary>The owner set, with empty identifiers dropped.</summary>
    protected static IReadOnlyList<Guid> Owners(params Guid?[] candidates) =>
        [.. candidates.Where(id => id is { } value && value != Guid.Empty).Select(id => id!.Value).Distinct()];
}

/// <summary>
/// The base for a consumer that matches saved searches rather than automations.
/// <para>
/// Separate because the two do different things with one publication: the engine runs a member's
/// rule, and the matcher answers "does this match a query somebody stored". Sharing a base would have
/// meant every publication consumer resolving both.
/// </para>
/// </summary>
public abstract class SavedSearchConsumer<TIntegrationEvent>
    : IntegrationEventHandler<TIntegrationEvent>
    where TIntegrationEvent : IIntegrationEvent
{
    private readonly SavedSearchMatcher _matcher;
    private readonly AutomationEngine _engine;

    protected SavedSearchConsumer(SavedSearchMatcher matcher, AutomationEngine engine)
    {
        _matcher = matcher;
        _engine = engine;
    }

    protected abstract SavedSearchSubject Subject { get; }

    /// <summary>Which change this publication represents — <c>newMatch</c>, <c>availabilityChange</c>.</summary>
    protected virtual string Change => "newMatch";

    /// <summary>The item reduced to the fields a saved search can compare. Null skips it.</summary>
    protected abstract SavedSearchCandidate? ToCandidate(TIntegrationEvent integrationEvent);

    /// <summary>The automation contexts this publication also produces. Empty for most.</summary>
    protected virtual IEnumerable<AutomationTriggerContext> Translate(TIntegrationEvent integrationEvent) =>
        [];

    protected override async Task HandleAsync(
        TIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        if (ToCandidate(integrationEvent) is { } candidate)
        {
            await _matcher.MatchAsync(Subject, candidate, Change, cancellationToken);
        }

        foreach (var context in Translate(integrationEvent))
        {
            await _engine.ObserveAsync(context, cancellationToken);
        }
    }

    protected static Dictionary<string, string?> Fields() =>
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The same two helpers the automation base has, for the consumers that do both jobs.
    /// <para>
    /// Wave 12 made three of these consumers arm a subject horizon as well as match saved searches,
    /// because <c>No_module_consumes_one_integration_event_twice</c> forbids a second consumer over
    /// one contract. They therefore need the trigger-value bag and the owner set, and duplicating the
    /// two lines in each was the alternative.
    /// </para>
    /// </summary>
    protected static Dictionary<string, string?> Values() => AutomationTriggerContext.NewValues();

    /// <summary>The owner set, with empty identifiers dropped.</summary>
    protected static IReadOnlyList<Guid> Owners(params Guid?[] candidates) =>
        [.. candidates
            .Where(id => id is { } value && value != Guid.Empty)
            .Select(id => id!.Value)
            .Distinct()];
}
