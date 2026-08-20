using Diaspora.Modules.Automation.Domain.Automations;
using Diaspora.Modules.Automation.Domain.Enums;
using Diaspora.Modules.Automation.Domain.Identifiers;
using Diaspora.Modules.Automation.Domain.Runs;
using Diaspora.Modules.Automation.Domain.SavedSearches;
using Diaspora.Modules.Automation.Domain.Scheduling;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Automation.Domain.Repositories;

/// <summary>How the registry is read and written.</summary>
public interface IAutomationRepository
{
    Task<AutomationDefinition?> GetAsync(AutomationId id, CancellationToken cancellationToken = default);

    /// <summary>Loads the rule with its conditions and actions, for execution or for an edit.</summary>
    Task<AutomationDefinition?> GetWithRuleAsync(AutomationId id, CancellationToken cancellationToken = default);

    /// <summary>Loads the automation with its audit trail.</summary>
    Task<AutomationDefinition?> GetWithAuditAsync(AutomationId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// The idempotency lookup for creation.
    /// <para>
    /// Scoped to the member as well as the key, so one member's key cannot return another member's
    /// automation — an idempotency store keyed on a client-supplied string alone is a cross-tenant
    /// read with a friendly name.
    /// </para>
    /// </summary>
    Task<AutomationDefinition?> FindByIdempotencyKeyAsync(
        UserId createdBy, string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every active automation watching one trigger, within one owner scope where one is given.
    /// <para>
    /// The engine's hot path. Filtered in the database rather than in memory: a platform-wide event
    /// with ten thousand automations watching it must not load ten thousand rows to discard most.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<AutomationDefinition>> ListRunnableForTriggerAsync(
        string triggerKey,
        IReadOnlyCollection<Guid> candidateOwnerIds,
        CancellationToken cancellationToken = default);

    Task<CursorPagedResult<AutomationDefinition>> ListAsync(
        AutomationOwnerKind? ownerKind,
        Guid? ownerId,
        AutomationStatus? status,
        string? domain,
        Guid? communityId,
        string? search,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>How many active automations this owner already has. The ceiling is checked before a write.</summary>
    Task<int> CountActiveAsync(
        AutomationOwnerKind ownerKind, Guid ownerId, CancellationToken cancellationToken = default);

    void Add(AutomationDefinition automation);

    void Remove(AutomationDefinition automation);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>How execution history is written and read.</summary>
public interface IAutomationRunRepository
{
    Task<AutomationRun?> GetAsync(AutomationRunId id, CancellationToken cancellationToken = default);

    Task<AutomationRun?> GetWithOutcomesAsync(
        AutomationRunId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// The idempotency lookup.
    /// <para>
    /// A redelivered event must find the run it already produced rather than start a second. The
    /// unique index is the real barrier — two instances would both pass this read — and this exists so
    /// the common case answers without provoking a constraint violation.
    /// </para>
    /// </summary>
    Task<AutomationRun?> FindByTriggerAsync(
        AutomationId automationId, string triggerEventId, CancellationToken cancellationToken = default);

    /// <summary>Finds a run by the action awaiting a settlement receipt.</summary>
    Task<AutomationRun?> FindForPendingActionAsync(
        AutomationRunId runId, CancellationToken cancellationToken = default);

    /// <summary>
    /// One page of history for one owner.
    /// <para>
    /// <b>B24 added <paramref name="failureKind"/> and <paramref name="triggerKey"/>.</b> §21 asks
    /// for both and neither could be asked before: "show me everything that timed out" and "show me
    /// what this trigger has been doing" are the two questions a diagnosis actually starts from, and
    /// a status filter answers neither — <c>Failed</c> covers ten failure kinds.
    /// </para>
    /// </summary>
    Task<CursorPagedResult<AutomationRun>> ListAsync(
        Guid ownerId,
        AutomationId? automationId,
        AutomationRunStatus? status,
        AutomationFailureKind? failureKind,
        string? triggerKey,
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The aggregate an operator or an owner reads instead of counting a page by hand.
    /// <para>
    /// <b>Computed from the run store, not from a second one.</b> §2 forbids competing analytics
    /// storage without a demonstrated requirement, and `automation_runs` already holds every field
    /// the summary reports, already has a stated retention and is already owner-scoped. What was
    /// missing was the aggregate query, not the data.
    /// </para>
    /// <para>
    /// <paramref name="ownerId"/> is applied unconditionally and first, exactly as it is on the page
    /// query. §24 and §25: this is a telemetry surface and a telemetry surface that can be widened by
    /// a parameter is a cross-tenant read with a friendly name.
    /// </para>
    /// </summary>
    Task<AutomationRunSummary> SummariseAsync(
        Guid ownerId,
        AutomationId? automationId,
        string? triggerKey,
        DateTimeOffset from,
        DateTimeOffset to,
        int topFailingActions,
        CancellationToken cancellationToken = default);

    /// <summary>Runs that started and never settled, for the sweeper to time out.</summary>
    Task<IReadOnlyList<AutomationRun>> ListStaleRunningAsync(
        DateTimeOffset olderThan, int limit, CancellationToken cancellationToken = default);

    /// <summary>How many runs this automation has started since a moment. The rate ceiling reads it.</summary>
    Task<int> CountSinceAsync(
        AutomationId automationId, DateTimeOffset since, CancellationToken cancellationToken = default);

    /// <summary>How many runs this owner has started since a moment, across every automation.</summary>
    Task<int> CountForOwnerSinceAsync(
        Guid ownerId, DateTimeOffset since, CancellationToken cancellationToken = default);

    void Add(AutomationRun run);

    Task<int> DeleteOlderThanAsync(DateTimeOffset cutoff, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// What one owner's automations have been doing over a window.
/// <para>
/// Counts and durations. <b>No identifiers beyond the ones the caller already supplied</b>, and no
/// run bodies — a summary is read far more widely than a run detail and is the wrong place to widen
/// what a reader can see.
/// </para>
/// </summary>
/// <param name="Total">Runs that started in the window, whatever became of them.</param>
/// <param name="ByStatus">
/// How they ended. <c>Running</c> appears here and is not an anomaly: a run awaiting a notification
/// receipt is genuinely still going, and reporting it as anything else would be the §77 violation
/// this platform has refused twice already.
/// </param>
/// <param name="ByFailureKind">
/// Why the failures failed. Empty when nothing failed, rather than a map of zeroes — a caller
/// rendering "0 timeouts, 0 rate-limited, 0 provider-unavailable" is noise pretending to be data.
/// </param>
/// <param name="AverageDurationMs">Over settled runs only. Null when none settled.</param>
/// <param name="SlowestDurationMs">The worst one. Null when none settled.</param>
/// <param name="Deduplicated">
/// Runs that were a recognised redelivery rather than a fresh firing. A rate approaching the total is
/// a consumer that is not acknowledging.
/// </param>
/// <param name="DeadLettered">
/// Scheduled moments given up on. Read from the occurrence store rather than the run store, because a
/// moment that dead-lettered never produced a run — which is exactly why it needs reporting.
/// </param>
/// <param name="TopFailingActions">Which actions fail most, descending. The ranked fix list.</param>
public sealed record AutomationRunSummary(
    int Total,
    IReadOnlyDictionary<AutomationRunStatus, int> ByStatus,
    IReadOnlyDictionary<AutomationFailureKind, int> ByFailureKind,
    double? AverageDurationMs,
    long? SlowestDurationMs,
    int Deduplicated,
    int DeadLettered,
    IReadOnlyList<AutomationActionFailureCount> TopFailingActions);

/// <summary>How often one action key failed.</summary>
/// <param name="ActionKey">A catalogue key. Bounded, and safe to render.</param>
/// <param name="Failures">How many times.</param>
public sealed record AutomationActionFailureCount(string ActionKey, int Failures);

/// <summary>How due moments are stored and claimed.</summary>
public interface IAutomationOccurrenceRepository
{
    /// <summary>
    /// How many of this owner's scheduled moments were given up on in a window.
    /// <para>
    /// Joined to the definition rather than denormalised onto the occurrence, because an occurrence
    /// has exactly one automation and the alternative is an owner column that can drift from it.
    /// </para>
    /// </summary>
    Task<int> CountDeadLetteredForOwnerAsync(
        Guid ownerId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default);

    Task<AutomationOccurrence?> GetAsync(
        AutomationOccurrenceId id, CancellationToken cancellationToken = default);

    /// <summary>Whether this automation already has this moment scheduled. The uniqueness read.</summary>
    Task<AutomationOccurrence?> FindAsync(
        AutomationId automationId, DateTimeOffset dueAt, CancellationToken cancellationToken = default);

    /// <summary>Everything due at or before a moment that nobody holds. Ordered oldest first.</summary>
    Task<IReadOnlyList<AutomationOccurrence>> ListDueAsync(
        DateTimeOffset asOf, int limit, CancellationToken cancellationToken = default);

    /// <summary>The pending occurrences for one automation, so a pause can cancel them and count them.</summary>
    Task<IReadOnlyList<AutomationOccurrence>> ListPendingForAutomationAsync(
        AutomationId automationId, CancellationToken cancellationToken = default);

    void Add(AutomationOccurrence occurrence);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// How dated subjects are remembered, rebased and swept — Wave 12.
/// <para>
/// Deliberately narrow. There is no list-by-owner and no paging: nothing outside the sweep and the
/// consumers that write it reads this table, and a route over it would be a way to enumerate every
/// job closing date on the platform.
/// </para>
/// </summary>
public interface IAutomationSubjectHorizonRepository
{
    /// <summary>The uniqueness read on <c>(TriggerKey, SubjectId)</c>.</summary>
    Task<AutomationSubjectHorizon?> FindAsync(
        string triggerKey, Guid subjectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every horizon on one subject, whatever the trigger.
    /// <para>
    /// The cancellation path reads this: a listing that sold cancels both its expiry horizon and its
    /// reservation-ending horizon, and a caller that had to name each trigger would forget one.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<AutomationSubjectHorizon>> ListForSubjectAsync(
        Guid subjectId, CancellationToken cancellationToken = default);

    /// <summary>Everything due at or before a moment that nobody holds. Ordered oldest first.</summary>
    Task<IReadOnlyList<AutomationSubjectHorizon>> ListDueAsync(
        DateTimeOffset asOf, int limit, CancellationToken cancellationToken = default);

    void Add(AutomationSubjectHorizon horizon);

    /// <summary>Removes settled rows past the retention horizon. Bounded, like the run sweep.</summary>
    Task<int> DeleteSettledOlderThanAsync(
        DateTimeOffset cutoff, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>How reminders are stored and swept.</summary>
public interface IAutomationReminderRepository
{
    Task<AutomationReminder?> GetAsync(
        AutomationReminderId id, CancellationToken cancellationToken = default);

    /// <summary>The mandatory idempotency lookup on (member, subject, offset, anchor).</summary>
    Task<AutomationReminder?> FindByKeyAsync(
        string deduplicationKey, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AutomationReminder>> ListForSubjectAsync(
        UserId memberId, string subjectKind, Guid subjectId, CancellationToken cancellationToken = default);

    /// <summary>Every pending reminder on a subject, so an event that moved can move them all.</summary>
    Task<IReadOnlyList<AutomationReminder>> ListPendingBySubjectAsync(
        Guid subjectId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AutomationReminder>> ListDueAsync(
        DateTimeOffset asOf, int limit, CancellationToken cancellationToken = default);

    void Add(AutomationReminder reminder);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>How saved searches and their match history are stored.</summary>
public interface ISavedSearchRepository
{
    Task<SavedSearch?> GetAsync(SavedSearchId id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SavedSearch>> ListForMemberAsync(
        UserId memberId, SavedSearchSubject? subject, CancellationToken cancellationToken = default);

    Task<SavedSearch?> FindByFingerprintAsync(
        UserId memberId,
        SavedSearchSubject subject,
        string fingerprint,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Every search watching a subject, for the matcher.
    /// <para>
    /// Filtered to alerts-enabled searches on the subject that published, which is the only narrowing
    /// the database can do — the criteria themselves are matched in memory against one item, which is
    /// bounded work per published item rather than per search.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<SavedSearch>> ListWatchingAsync(
        SavedSearchSubject subject, CancellationToken cancellationToken = default);

    /// <summary>Whether this search has already matched this item. The once-ever barrier's read half.</summary>
    Task<bool> HasMatchedAsync(
        SavedSearchId savedSearchId, Guid matchedItemId, CancellationToken cancellationToken = default);

    void Add(SavedSearch savedSearch);

    void AddMatch(SavedSearchMatch match);

    void Remove(SavedSearch savedSearch);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
