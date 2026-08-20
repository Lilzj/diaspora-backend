using Diaspora.Modules.Automation.Domain.Automations;
using Diaspora.Modules.Automation.Domain.Enums;
using Diaspora.Modules.Automation.Domain.Identifiers;
using Diaspora.Modules.Automation.Domain.Repositories;
using Diaspora.Modules.Automation.Domain.Runs;
using Diaspora.Modules.Automation.Domain.SavedSearches;
using Diaspora.Modules.Automation.Domain.Scheduling;
using Diaspora.Persistence;
using Diaspora.Persistence.Paging;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Automation.Persistence.Repositories;

/// <summary>
/// The registry.
/// <para>
/// <b>Every read that answers a request filters on the owner.</b> The two that do not —
/// <see cref="ListRunnableForTriggerAsync"/> and the ceiling count — are reachable only from the
/// engine and from the write path, neither of which takes an owner from a client.
/// </para>
/// </summary>
public sealed class AutomationRepository : IAutomationRepository
{
    private readonly DiasporaDbContext _dbContext;

    public AutomationRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<AutomationDefinition> Automations => _dbContext.Set<AutomationDefinition>();

    public Task<AutomationDefinition?> GetAsync(AutomationId id, CancellationToken cancellationToken = default) =>
        Automations.FirstOrDefaultAsync(row => row.Id == id, cancellationToken);

    public Task<AutomationDefinition?> GetWithRuleAsync(
        AutomationId id, CancellationToken cancellationToken = default) =>
        Automations
            .Include(row => row.Conditions)
            .Include(row => row.Actions)
            .FirstOrDefaultAsync(row => row.Id == id, cancellationToken);

    /// <summary>
    /// The rule and its audit trail together.
    /// <para>
    /// <c>AsSplitQuery</c> because three collections on one root multiply: a rule with four conditions,
    /// three actions and forty audit entries would return four hundred and eighty rows to build one
    /// object. Three queries, one round trip each, is cheaper at every size that matters.
    /// </para>
    /// </summary>
    public Task<AutomationDefinition?> GetWithAuditAsync(
        AutomationId id, CancellationToken cancellationToken = default) =>
        Automations
            .Include(row => row.Conditions)
            .Include(row => row.Actions)
            .Include(row => row.Audit)
            .AsSplitQuery()
            .FirstOrDefaultAsync(row => row.Id == id, cancellationToken);

    public Task<AutomationDefinition?> FindByIdempotencyKeyAsync(
        UserId createdBy, string idempotencyKey, CancellationToken cancellationToken = default) =>
        Automations
            .Include(row => row.Conditions)
            .Include(row => row.Actions)
            .FirstOrDefaultAsync(
                row => row.CreatedByUserId == createdBy && row.IdempotencyKey == idempotencyKey,
                cancellationToken);

    /// <summary>
    /// The engine's hot path.
    /// <para>
    /// Narrowed in the database on the trigger key, the active status and the owner set. A
    /// platform-wide fact with ten thousand automations watching it must not load ten thousand rows to
    /// discard most of them, and the composite index on <c>(TriggerKey, Status)</c> is what makes this
    /// a seek.
    /// </para>
    /// <para>
    /// An empty owner set means "anybody's", which is what a public publication is — a guide published
    /// concerns every member watching guides, not one. The engine passes a set only when the fact is
    /// about specific people.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<AutomationDefinition>> ListRunnableForTriggerAsync(
        string triggerKey,
        IReadOnlyCollection<Guid> candidateOwnerIds,
        CancellationToken cancellationToken = default)
    {
        var query = Automations
            .Where(row => row.TriggerKey == triggerKey && row.Status == AutomationStatus.Active);

        if (candidateOwnerIds.Count > 0)
        {
            var owners = candidateOwnerIds.Distinct().ToArray();
            query = query.Where(row => owners.Contains(row.OwnerId));
        }

        return await query
            .OrderBy(row => row.CreatedAt)
            .ThenBy(row => row.Id)
            .Take(500)
            .ToListAsync(cancellationToken);
    }

    public async Task<CursorPagedResult<AutomationDefinition>> ListAsync(
        AutomationOwnerKind? ownerKind,
        Guid? ownerId,
        AutomationStatus? status,
        string? domain,
        Guid? communityId,
        string? search,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var query = Automations.AsQueryable();

        if (ownerKind is { } kind)
        {
            query = query.Where(row => row.OwnerKind == kind);
        }

        if (ownerId is { } owner)
        {
            query = query.Where(row => row.OwnerId == owner);
        }

        if (status is { } state)
        {
            query = query.Where(row => row.Status == state);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(row => row.Name.Contains(term));
        }

        if (communityId is { } community)
        {
            // The scope column is a delimited list, so this is a contains rather than a join. It is a
            // filter over an already-owner-narrowed set, not a scan.
            var token = community.ToString("D");
            query = query.Where(row => row.CommunityIds.Contains(token));
        }

        if (!string.IsNullOrWhiteSpace(domain))
        {
            // A trigger key's first segment is its domain — event.created is events, job.* is jobs.
            // Matching on the prefix avoids a second column that could disagree with the key.
            var prefix = DomainPrefix(domain);
            query = prefix is null ? query : query.Where(row => row.TriggerKey.StartsWith(prefix));
        }

        var parsed = AutomationCursor.Parse(cursor);

        if (parsed?.AsTimestamp() is { } boundary)
        {
            var exclusion = CursorBoundary.Exclusion(parsed.BoundaryKey, AutomationId.From);

            // Excluding an empty set is not the same as skipping the group — reading IsExhausted is
            // what stops an absurdly wide tie group being served for ever.
            query = exclusion.IsExhausted
                ? query.Where(row => row.CreatedAt < boundary)
                : query.Where(row =>
                    row.CreatedAt < boundary ||
                    (row.CreatedAt == boundary && !exclusion.Ids.Contains(row.Id)));
        }

        var rows = await query
            .OrderByDescending(row => row.CreatedAt)
            .ThenByDescending(row => row.Id)
            .Take(limit + 1)
            .Include(row => row.Conditions)
            .Include(row => row.Actions)
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > limit;
        var page = hasMore ? rows.Take(limit).ToList() : rows;

        if (!hasMore || page.Count == 0)
        {
            return CursorPagedResult<AutomationDefinition>.Create(page, null, false);
        }

        var boundaryKey = CursorBoundary.Key(
            page,
            row => row.CreatedAt,
            row => row.Id.Value,
            parsed?.BoundaryKey,
            parsed?.AsTimestamp());

        return CursorPagedResult<AutomationDefinition>.Create(
            page,
            AutomationCursor.FromTimestamp(page[^1].CreatedAt, boundaryKey).Encode(),
            true);
    }

    public Task<int> CountActiveAsync(
        AutomationOwnerKind ownerKind, Guid ownerId, CancellationToken cancellationToken = default) =>
        Automations.CountAsync(
            row => row.OwnerKind == ownerKind && row.OwnerId == ownerId &&
                   row.Status == AutomationStatus.Active,
            cancellationToken);

    public void Add(AutomationDefinition automation) => Automations.Add(automation);

    public void Remove(AutomationDefinition automation) => Automations.Remove(automation);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _dbContext.SaveChangesAsync(cancellationToken);

    private static string? DomainPrefix(string domain) => domain.ToLowerInvariant() switch
    {
        "events" => "event.",
        "community" => "community.",
        "organizations" => "organization.",
        "guides" => "guide.",
        "jobs" => "job.",
        "marketplace" => "listing.",
        "housing" => "housing.",
        "providers" => "provider.",
        "creator" => "creator.",
        "wallet" => "payment.",
        "monetization" => "subscription.",
        "search" => "savedSearch.",
        "messaging" => "conversation.",
        "location" => "location.",
        _ => null
    };
}

/// <summary>Execution history.</summary>
public sealed class AutomationRunRepository : IAutomationRunRepository
{
    private readonly DiasporaDbContext _dbContext;

    public AutomationRunRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<AutomationRun> Runs => _dbContext.Set<AutomationRun>();

    public Task<AutomationRun?> GetAsync(
        AutomationRunId id, CancellationToken cancellationToken = default) =>
        Runs.FirstOrDefaultAsync(row => row.Id == id, cancellationToken);

    public Task<AutomationRun?> GetWithOutcomesAsync(
        AutomationRunId id, CancellationToken cancellationToken = default) =>
        Runs
            .Include(row => row.ConditionOutcomes)
            .Include(row => row.ActionOutcomes)
            .FirstOrDefaultAsync(row => row.Id == id, cancellationToken);

    public Task<AutomationRun?> FindByTriggerAsync(
        AutomationId automationId,
        string triggerEventId,
        CancellationToken cancellationToken = default) =>
        Runs.FirstOrDefaultAsync(
            row => row.AutomationId == automationId && row.TriggerEventId == triggerEventId,
            cancellationToken);

    public Task<AutomationRun?> FindForPendingActionAsync(
        AutomationRunId runId, CancellationToken cancellationToken = default) =>
        Runs
            .Include(row => row.ActionOutcomes)
            .FirstOrDefaultAsync(row => row.Id == runId, cancellationToken);

    public async Task<CursorPagedResult<AutomationRun>> ListAsync(
        Guid ownerId,
        AutomationId? automationId,
        AutomationRunStatus? status,
        AutomationFailureKind? failureKind,
        string? triggerKey,
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default)
    {
        // The owner filter is applied first and unconditionally. §66: a member must not be able to read
        // another member's runs by supplying a run id, and the route takes no owner parameter, so this
        // is the only place the constraint can live.
        var query = Runs.Where(row => row.OwnerId == ownerId);

        if (automationId is { } automation)
        {
            query = query.Where(row => row.AutomationId == automation);
        }

        if (status is { } state)
        {
            query = query.Where(row => row.Status == state);
        }

        if (failureKind is { } kind)
        {
            query = query.Where(row => row.FailureKind == kind);
        }

        if (!string.IsNullOrWhiteSpace(triggerKey))
        {
            query = query.Where(row => row.TriggerKey == triggerKey);
        }

        if (from is { } start)
        {
            query = query.Where(row => row.StartedAt >= start);
        }

        if (to is { } end)
        {
            query = query.Where(row => row.StartedAt <= end);
        }

        var parsed = AutomationCursor.Parse(cursor);

        if (parsed?.AsTimestamp() is { } boundary)
        {
            var exclusion = CursorBoundary.Exclusion(parsed.BoundaryKey, AutomationRunId.From);

            query = exclusion.IsExhausted
                ? query.Where(row => row.StartedAt < boundary)
                : query.Where(row =>
                    row.StartedAt < boundary ||
                    (row.StartedAt == boundary && !exclusion.Ids.Contains(row.Id)));
        }

        var rows = await query
            .OrderByDescending(row => row.StartedAt)
            .ThenByDescending(row => row.Id)
            .Take(limit + 1)
            .Include(row => row.ConditionOutcomes)
            .Include(row => row.ActionOutcomes)
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > limit;
        var page = hasMore ? rows.Take(limit).ToList() : rows;

        if (!hasMore || page.Count == 0)
        {
            return CursorPagedResult<AutomationRun>.Create(page, null, false);
        }

        var boundaryKey = CursorBoundary.Key(
            page,
            row => row.StartedAt,
            row => row.Id.Value,
            parsed?.BoundaryKey,
            parsed?.AsTimestamp());

        return CursorPagedResult<AutomationRun>.Create(
            page,
            AutomationCursor.FromTimestamp(page[^1].StartedAt, boundaryKey).Encode(),
            true);
    }

    /// <summary>
    /// The B24 summary, in four queries rather than one.
    /// <para>
    /// <b>Grouped in the database, never in memory.</b> A member with a busy community automation can
    /// have tens of thousands of runs inside a 30-day window; materialising them to count statuses
    /// would put the whole window in the web process, and the resulting figure would be no more
    /// accurate than a <c>GROUP BY</c>.
    /// </para>
    /// <para>
    /// <b>Four queries and not one join, deliberately.</b> Statuses, failure kinds and durations
    /// aggregate over runs; failing actions aggregate over a child table with a different
    /// cardinality. Expressing them as one statement would multiply the run rows by their outcomes
    /// and give a status count that is wrong in exactly the direction nobody checks — high, and only
    /// for the runs that did the most.
    /// </para>
    /// </summary>
    public async Task<AutomationRunSummary> SummariseAsync(
        Guid ownerId,
        AutomationId? automationId,
        string? triggerKey,
        DateTimeOffset from,
        DateTimeOffset to,
        int topFailingActions,
        CancellationToken cancellationToken = default)
    {
        var runs = Scope(ownerId, automationId, triggerKey, from, to);

        var byStatus = await runs
            .GroupBy(row => row.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        var byFailure = await runs
            .Where(row => row.FailureKind != AutomationFailureKind.None)
            .GroupBy(row => row.FailureKind)
            .Select(group => new { Kind = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        // Settled runs only. A run still awaiting a receipt has no duration, and averaging over a
        // null-as-zero would report a platform getting faster the more work it has outstanding.
        //
        // Two aggregates rather than one grouped projection. `GroupBy(_ => 1)` reads better and does
        // not translate on every provider; a query that renders on one database and throws on
        // another is a 500 that only appears in whichever environment the suite does not run in.
        var settled = runs.Where(row => row.DurationMs != null);

        var average = await settled
            .Select(row => (double?)row.DurationMs)
            .AverageAsync(cancellationToken);

        var slowest = await settled
            .Select(row => (long?)row.DurationMs)
            .MaxAsync(cancellationToken);

        var deduplicated = await runs.CountAsync(row => row.WasDeduplicated, cancellationToken);

        // Projected into an anonymous type and mapped afterwards. Constructing the domain record
        // inside the `Select` is what a reader expects and is not translatable: EF renders a grouped
        // projection into an anonymous shape and refuses an arbitrary constructor, so the readable
        // version becomes a runtime failure on a route that returns 500 rather than a compile error.
        // The grouping, the ordering and the limit all still happen in the database.
        var grouped = await _dbContext.Set<AutomationRunActionOutcome>()
            .Where(outcome => outcome.Status == AutomationActionOutcomeStatus.Failed)
            .Join(runs, outcome => outcome.RunId, run => run.Id, (outcome, _) => outcome.ActionKey)
            .GroupBy(key => key)
            .Select(group => new { ActionKey = group.Key, Failures = group.Count() })
            .OrderByDescending(entry => entry.Failures)
            .ThenBy(entry => entry.ActionKey)
            .Take(topFailingActions)
            .ToListAsync(cancellationToken);

        var failingActions = grouped
            .Select(entry => new AutomationActionFailureCount(entry.ActionKey, entry.Failures))
            .ToList();

        return new AutomationRunSummary(
            byStatus.Sum(entry => entry.Count),
            byStatus.ToDictionary(entry => entry.Status, entry => entry.Count),
            byFailure.ToDictionary(entry => entry.Kind, entry => entry.Count),
            average,
            slowest,
            deduplicated,
            DeadLettered: 0,
            failingActions);
    }

    /// <summary>The one predicate every summary query shares, so none of them can forget the owner.</summary>
    private IQueryable<AutomationRun> Scope(
        Guid ownerId,
        AutomationId? automationId,
        string? triggerKey,
        DateTimeOffset from,
        DateTimeOffset to)
    {
        var query = Runs.Where(row =>
            row.OwnerId == ownerId && row.StartedAt >= from && row.StartedAt <= to);

        if (automationId is { } automation)
        {
            query = query.Where(row => row.AutomationId == automation);
        }

        if (!string.IsNullOrWhiteSpace(triggerKey))
        {
            query = query.Where(row => row.TriggerKey == triggerKey);
        }

        return query;
    }

    public async Task<IReadOnlyList<AutomationRun>> ListStaleRunningAsync(
        DateTimeOffset olderThan, int limit, CancellationToken cancellationToken = default) =>
        await Runs
            .Where(row => row.Status == AutomationRunStatus.Running && row.StartedAt < olderThan)
            .OrderBy(row => row.StartedAt)
            .Take(limit)
            .Include(row => row.ActionOutcomes)
            .ToListAsync(cancellationToken);

    public Task<int> CountSinceAsync(
        AutomationId automationId, DateTimeOffset since, CancellationToken cancellationToken = default) =>
        Runs.CountAsync(
            row => row.AutomationId == automationId && row.StartedAt >= since, cancellationToken);

    public Task<int> CountForOwnerSinceAsync(
        Guid ownerId, DateTimeOffset since, CancellationToken cancellationToken = default) =>
        Runs.CountAsync(row => row.OwnerId == ownerId && row.StartedAt >= since, cancellationToken);

    public void Add(AutomationRun run) => Runs.Add(run);

    /// <summary>
    /// Applies the retention horizon.
    /// <para>
    /// Finite and stated, because an execution log over a wallet or a location trigger is a history of
    /// somebody's money and movements. Child outcomes go with it through the cascade.
    /// </para>
    /// <para>
    /// <b>A bounded read then <c>RemoveRange</c>, rather than <c>ExecuteDeleteAsync</c>.</b> The
    /// platform's persistence rule refuses a set-based delete from a vertical, and the batch ceiling is
    /// the reason the rule is right here as well as in principle: an unbounded delete over a year of
    /// runs is one statement holding one lock for as long as it takes, and the sweeper runs every
    /// thirty seconds, so bounded batches converge without ever being the longest transaction on the
    /// connection.
    /// </para>
    /// </summary>
    public async Task<int> DeleteOlderThanAsync(
        DateTimeOffset cutoff, CancellationToken cancellationToken = default)
    {
        const int BatchSize = 500;

        var expired = await Runs
            .Where(row => row.StartedAt < cutoff)
            .OrderBy(row => row.StartedAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (expired.Count == 0)
        {
            return 0;
        }

        Runs.RemoveRange(expired);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return expired.Count;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _dbContext.SaveChangesAsync(cancellationToken);
}

/// <summary>The durable due-store.</summary>
public sealed class AutomationOccurrenceRepository : IAutomationOccurrenceRepository
{
    private readonly DiasporaDbContext _dbContext;

    public AutomationOccurrenceRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<AutomationOccurrence> Occurrences => _dbContext.Set<AutomationOccurrence>();

    public Task<AutomationOccurrence?> GetAsync(
        AutomationOccurrenceId id, CancellationToken cancellationToken = default) =>
        Occurrences.FirstOrDefaultAsync(row => row.Id == id, cancellationToken);

    public Task<AutomationOccurrence?> FindAsync(
        AutomationId automationId, DateTimeOffset dueAt, CancellationToken cancellationToken = default) =>
        Occurrences.FirstOrDefaultAsync(
            row => row.AutomationId == automationId && row.DueAt == dueAt, cancellationToken);

    /// <summary>
    /// B24: dead-lettered moments for one owner.
    /// <para>
    /// The owner is reached through the definition, which is the only place it lives. A subquery
    /// rather than a join so the shape stays a count and EF cannot be tempted into materialising the
    /// definitions to get at one column.
    /// </para>
    /// </summary>
    public Task<int> CountDeadLetteredForOwnerAsync(
        Guid ownerId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default) =>
        Occurrences.CountAsync(
            row =>
                row.Status == AutomationOccurrenceStatus.DeadLettered &&
                row.DueAt >= from &&
                row.DueAt <= to &&
                _dbContext.Set<AutomationDefinition>()
                    .Any(definition =>
                        definition.Id == row.AutomationId && definition.OwnerId == ownerId),
            cancellationToken);

    public async Task<IReadOnlyList<AutomationOccurrence>> ListDueAsync(
        DateTimeOffset asOf, int limit, CancellationToken cancellationToken = default) =>
        await Occurrences
            .Where(row =>
                row.DueAt <= asOf &&
                (row.Status == AutomationOccurrenceStatus.Pending ||
                 (row.Status == AutomationOccurrenceStatus.Claimed &&
                  row.ClaimExpiresAt != null && row.ClaimExpiresAt < asOf)))
            .OrderBy(row => row.DueAt)
            .ThenBy(row => row.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AutomationOccurrence>> ListPendingForAutomationAsync(
        AutomationId automationId, CancellationToken cancellationToken = default) =>
        await Occurrences
            .Where(row =>
                row.AutomationId == automationId &&
                (row.Status == AutomationOccurrenceStatus.Pending ||
                 row.Status == AutomationOccurrenceStatus.Claimed))
            .ToListAsync(cancellationToken);

    public void Add(AutomationOccurrence occurrence) => Occurrences.Add(occurrence);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _dbContext.SaveChangesAsync(cancellationToken);
}

/// <summary>
/// Dated subjects the platform will look at again — Wave 12.
/// <para>
/// The due read mirrors the occurrence store's exactly, including the lapsed-claim clause: an instance
/// that died holding a claim must leave a row somebody else can take, and a query that only looked at
/// <c>Pending</c> would leave it stranded for ever.
/// </para>
/// </summary>
public sealed class AutomationSubjectHorizonRepository : IAutomationSubjectHorizonRepository
{
    private readonly DiasporaDbContext _dbContext;

    public AutomationSubjectHorizonRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<AutomationSubjectHorizon> Horizons => _dbContext.Set<AutomationSubjectHorizon>();

    public Task<AutomationSubjectHorizon?> FindAsync(
        string triggerKey, Guid subjectId, CancellationToken cancellationToken = default) =>
        Horizons.FirstOrDefaultAsync(
            row => row.TriggerKey == triggerKey && row.SubjectId == subjectId, cancellationToken);

    public async Task<IReadOnlyList<AutomationSubjectHorizon>> ListForSubjectAsync(
        Guid subjectId, CancellationToken cancellationToken = default) =>
        await Horizons
            .Where(row =>
                row.SubjectId == subjectId &&
                (row.Status == AutomationOccurrenceStatus.Pending ||
                 row.Status == AutomationOccurrenceStatus.Claimed))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AutomationSubjectHorizon>> ListDueAsync(
        DateTimeOffset asOf, int limit, CancellationToken cancellationToken = default) =>
        await Horizons
            .Where(row =>
                row.DueAt <= asOf &&
                (row.Status == AutomationOccurrenceStatus.Pending ||
                 (row.Status == AutomationOccurrenceStatus.Claimed &&
                  row.ClaimExpiresAt != null && row.ClaimExpiresAt < asOf)))
            .OrderBy(row => row.DueAt)
            .ThenBy(row => row.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public void Add(AutomationSubjectHorizon horizon) => Horizons.Add(horizon);

    /// <summary>
    /// Bounded, then <c>RemoveRange</c>.
    /// <para>
    /// Never <c>ExecuteDeleteAsync</c>: Wave 11 shipped one and the platform's own persistence
    /// guardrail reported it, because an unbounded set delete from a vertical is a lock held over a
    /// table nobody sized.
    /// </para>
    /// </summary>
    public async Task<int> DeleteSettledOlderThanAsync(
        DateTimeOffset cutoff, CancellationToken cancellationToken = default)
    {
        const int BatchSize = 500;

        var settled = await Horizons
            .Where(row =>
                row.CompletedAt != null && row.CompletedAt < cutoff &&
                row.Status != AutomationOccurrenceStatus.Pending &&
                row.Status != AutomationOccurrenceStatus.Claimed)
            .OrderBy(row => row.CompletedAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (settled.Count == 0)
        {
            return 0;
        }

        Horizons.RemoveRange(settled);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return settled.Count;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _dbContext.SaveChangesAsync(cancellationToken);
}

/// <summary>Reminders.</summary>
public sealed class AutomationReminderRepository : IAutomationReminderRepository
{
    private readonly DiasporaDbContext _dbContext;

    public AutomationReminderRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<AutomationReminder> Reminders => _dbContext.Set<AutomationReminder>();

    public Task<AutomationReminder?> GetAsync(
        AutomationReminderId id, CancellationToken cancellationToken = default) =>
        Reminders.FirstOrDefaultAsync(row => row.Id == id, cancellationToken);

    public Task<AutomationReminder?> FindByKeyAsync(
        string deduplicationKey, CancellationToken cancellationToken = default) =>
        Reminders.FirstOrDefaultAsync(
            row => row.DeduplicationKey == deduplicationKey, cancellationToken);

    public async Task<IReadOnlyList<AutomationReminder>> ListForSubjectAsync(
        UserId memberId,
        string subjectKind,
        Guid subjectId,
        CancellationToken cancellationToken = default) =>
        await Reminders
            .Where(row =>
                row.MemberId == memberId && row.SubjectKind == subjectKind &&
                row.SubjectId == subjectId)
            .OrderBy(row => row.OffsetMinutes)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AutomationReminder>> ListPendingBySubjectAsync(
        Guid subjectId, CancellationToken cancellationToken = default) =>
        await Reminders
            .Where(row =>
                row.SubjectId == subjectId &&
                (row.State == AutomationReminderState.Configured ||
                 row.State == AutomationReminderState.Scheduled))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AutomationReminder>> ListDueAsync(
        DateTimeOffset asOf, int limit, CancellationToken cancellationToken = default) =>
        await Reminders
            .Where(row =>
                row.State == AutomationReminderState.Scheduled &&
                row.ScheduledFor != null && row.ScheduledFor <= asOf)
            .OrderBy(row => row.ScheduledFor)
            .ThenBy(row => row.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public void Add(AutomationReminder reminder) => Reminders.Add(reminder);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _dbContext.SaveChangesAsync(cancellationToken);
}

/// <summary>Saved searches and their match history.</summary>
public sealed class SavedSearchRepository : ISavedSearchRepository
{
    private readonly DiasporaDbContext _dbContext;

    public SavedSearchRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<SavedSearch> Searches => _dbContext.Set<SavedSearch>();

    private DbSet<SavedSearchMatch> Matches => _dbContext.Set<SavedSearchMatch>();

    public Task<SavedSearch?> GetAsync(
        SavedSearchId id, CancellationToken cancellationToken = default) =>
        Searches.FirstOrDefaultAsync(row => row.Id == id, cancellationToken);

    public async Task<IReadOnlyList<SavedSearch>> ListForMemberAsync(
        UserId memberId, SavedSearchSubject? subject, CancellationToken cancellationToken = default)
    {
        var query = Searches.Where(row => row.MemberId == memberId);

        if (subject is { } value)
        {
            query = query.Where(row => row.Subject == value);
        }

        return await query
            .OrderByDescending(row => row.CreatedAt)
            .ThenByDescending(row => row.Id)
            .Take(200)
            .ToListAsync(cancellationToken);
    }

    public Task<SavedSearch?> FindByFingerprintAsync(
        UserId memberId,
        SavedSearchSubject subject,
        string fingerprint,
        CancellationToken cancellationToken = default) =>
        Searches.FirstOrDefaultAsync(
            row => row.MemberId == memberId && row.Subject == subject &&
                   row.CriteriaFingerprint == fingerprint,
            cancellationToken);

    public async Task<IReadOnlyList<SavedSearch>> ListWatchingAsync(
        SavedSearchSubject subject, CancellationToken cancellationToken = default) =>
        await Searches
            .Where(row => row.Subject == subject && row.AlertsEnabled)
            .OrderBy(row => row.CreatedAt)
            .ThenBy(row => row.Id)
            .Take(2_000)
            .ToListAsync(cancellationToken);

    public Task<bool> HasMatchedAsync(
        SavedSearchId savedSearchId, Guid matchedItemId, CancellationToken cancellationToken = default) =>
        Matches.AnyAsync(
            row => row.SavedSearchId == savedSearchId && row.MatchedItemId == matchedItemId,
            cancellationToken);

    public void Add(SavedSearch savedSearch) => Searches.Add(savedSearch);

    public void AddMatch(SavedSearchMatch match) => Matches.Add(match);

    public void Remove(SavedSearch savedSearch) => Searches.Remove(savedSearch);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _dbContext.SaveChangesAsync(cancellationToken);
}
