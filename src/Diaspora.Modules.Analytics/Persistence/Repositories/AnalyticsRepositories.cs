using Diaspora.Modules.Analytics.Domain.Dimensions;
using Diaspora.Modules.Analytics.Domain.Enums;
using Diaspora.Modules.Analytics.Domain.Events;
using Diaspora.Modules.Analytics.Domain.Exports;
using Diaspora.Modules.Analytics.Domain.Identifiers;
using Diaspora.Modules.Analytics.Domain.Policies;
using Diaspora.Modules.Analytics.Domain.Projection;
using Diaspora.Modules.Analytics.Domain.Reports;
using Diaspora.Modules.Analytics.Domain.Repositories;
using Diaspora.Modules.Analytics.Domain.Rollups;
using Diaspora.Modules.Analytics.Domain.Schema;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Analytics.Persistence.Repositories;

/// <summary>
/// Staging that cannot turn an insert into an update.
/// <para>
/// The platform's own lesson, applied here on the module's first day: calling <c>Update</c> on an
/// aggregate the same unit of work has just <c>Add</c>ed flips it from <c>Added</c> to
/// <c>Modified</c>, EF skips the parent INSERT, and the failure surfaces as a foreign-key violation
/// naming a child table several frames from the cause. This module's projection is exactly the shape
/// that hits it — find-or-create a rollup cell, then immediately write to it.
/// </para>
/// </summary>
internal static class TrackingSafety
{
    public static void StageUpdate<TEntity>(this DiasporaDbContext dbContext, TEntity entity)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(entity);

        if (dbContext.Entry(entity).State is EntityState.Added)
        {
            return;
        }

        dbContext.Set<TEntity>().Update(entity);
    }
}

/// <summary>
/// Reads and writes the raw analytics store.
/// <para>
/// <b>Every read here is bounded, and the bound is structural rather than a convention.</b> There is
/// no method that returns an unfiltered list, no method whose only filter is a date, and no method a
/// caller can ask for "everything". §25 forbids loading events into memory to aggregate them, and
/// the way to keep that promise is to have no signature that would let a handler do it.
/// </para>
/// </summary>
public sealed class AnalyticsEventRepository : IAnalyticsEventRepository
{
    private readonly DiasporaDbContext _dbContext;

    public AnalyticsEventRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<AnalyticsEvent> Events => _dbContext.Set<AnalyticsEvent>();

    public void Add(AnalyticsEvent analyticsEvent) => Events.Add(analyticsEvent);

    /// <summary>
    /// Which of these keys are already stored.
    /// <para>
    /// <b>The change tracker is consulted first, and that is not an optimisation.</b> A telemetry
    /// batch can carry the same event twice — a client that retried mid-flush legitimately does —
    /// and the rows staged earlier in the same request are invisible to a database query. Without
    /// this the batch would stage two rows with one deduplication key and the unique index would
    /// refuse the whole save, failing an entire batch because one event was repeated inside it.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyCollection<string>> FindExistingKeysAsync(
        IReadOnlyCollection<string> dedupKeys, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dedupKeys);

        var distinct = dedupKeys.Distinct(StringComparer.Ordinal).ToList();

        if (distinct.Count == 0)
        {
            return [];
        }

        var staged = _dbContext.ChangeTracker
            .Entries<AnalyticsEvent>()
            .Where(entry => entry.State is EntityState.Added)
            .Select(entry => entry.Entity.DedupKey)
            .ToHashSet(StringComparer.Ordinal);

        var stored = await Events
            .AsNoTracking()
            .Where(row => distinct.Contains(row.DedupKey))
            .Select(row => row.DedupKey)
            .ToListAsync(cancellationToken);

        staged.UnionWith(stored);

        return staged;
    }

    public async Task<IReadOnlyList<AnalyticsEvent>> ListAfterAsync(
        DateTimeOffset position,
        int limit,
        CancellationToken cancellationToken = default) =>
        await Events
            .AsNoTracking()
            .Where(row => row.ReceivedAt > position)
            .OrderBy(row => row.ReceivedAt)
            .Take(Math.Max(1, limit))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AnalyticsEvent>> ListAtAsync(
        DateTimeOffset instant, CancellationToken cancellationToken = default) =>
        await Events
            .AsNoTracking()
            .Where(row => row.ReceivedAt == instant)
            .ToListAsync(cancellationToken);

    public Task<AnalyticsCellCounts> AggregateContentDayAsync(
        AnalyticsContentType contentType,
        Guid contentId,
        DateOnly day,
        CancellationToken cancellationToken = default) =>
        AggregateAsync(
            Events.Where(row => row.ContentType == contentType
                && row.ContentId == contentId
                && row.OccurredOn == day),
            cancellationToken);

    public Task<AnalyticsCellCounts> AggregateOwnerDayAsync(
        Guid ownerUserId, DateOnly day, CancellationToken cancellationToken = default) =>
        AggregateAsync(
            Events.Where(row => row.OwnerUserId == ownerUserId && row.OccurredOn == day),
            cancellationToken);

    /// <summary>
    /// The conditional sums, in one grouped query.
    /// <para>
    /// <c>Sum(… ? 1 : 0)</c> rather than <c>Count(predicate)</c> because the ternary translates to
    /// <c>SUM(CASE WHEN … THEN 1 ELSE 0 END)</c> on every provider this platform supports, and the
    /// filtered overload does not. <c>GroupBy(_ =&gt; 1)</c> collapses the whole filtered set to one
    /// row; an empty set yields no rows at all, which is why the caller falls back to
    /// <see cref="AnalyticsCellCounts.Empty"/> rather than expecting a row of zeroes.
    /// </para>
    /// <para>
    /// Conversions are summed by weight and everything else by occurrence: an RSVP for a party of
    /// four is one conversion event carrying a weight, and a figure that ignored it would report
    /// four people attending as one.
    /// </para>
    /// </summary>
    private static async Task<AnalyticsCellCounts> AggregateAsync(
        IQueryable<AnalyticsEvent> source, CancellationToken cancellationToken)
    {
        var aggregate = await source
            .AsNoTracking()
            .GroupBy(_ => 1)
            .Select(group => new AnalyticsCellCounts(
                group.Sum(row => row.EventType == AnalyticsEventTypes.ContentViewed ? 1L : 0L),
                group.Sum(row => row.EventType == AnalyticsEventTypes.ContentSaved ? 1L : 0L),
                group.Sum(row => row.EventType == AnalyticsEventTypes.ContentShared ? 1L : 0L),
                group.Sum(row => row.EventType == AnalyticsEventTypes.ContentCommented ? 1L : 0L),
                group.Sum(row => row.EventType == AnalyticsEventTypes.ContentReacted ? 1L : 0L),
                group.Sum(row =>
                    row.EventType == AnalyticsEventTypes.ContentReacted && row.Qualifier == HelpfulReaction
                        ? 1L
                        : 0L),
                group.Sum(row =>
                    row.EventType == AnalyticsEventTypes.ContentConverted ? (long)row.Weight : 0L),
                group.Sum(row => row.EventType == AnalyticsEventTypes.ContentPublished ? 1L : 0L),
                group.Sum(row => row.EventType == AnalyticsEventTypes.AudienceFollowed ? 1L : 0L),
                group.Sum(row => row.EventType == AnalyticsEventTypes.AudienceUnfollowed ? 1L : 0L),
                group.Sum(row => row.EventType == AnalyticsEventTypes.ProfileViewed ? 1L : 0L),
                group.Sum(row => row.EventType == AnalyticsEventTypes.ContentEnquired ? 1L : 0L)))
            .FirstOrDefaultAsync(cancellationToken);

        return aggregate ?? AnalyticsCellCounts.Empty;
    }

    /// <summary>B6's catalogue key for the reaction the client calls a helpful vote.</summary>
    private const string HelpfulReaction = "helpful";

    public Task<long> CountDistinctViewersForContentDayAsync(
        AnalyticsContentType contentType,
        Guid contentId,
        DateOnly day,
        CancellationToken cancellationToken = default) =>
        Events
            .AsNoTracking()
            .Where(row => row.ContentType == contentType
                && row.ContentId == contentId
                && row.OccurredOn == day
                && row.ActorKey != null
                && row.EventType == AnalyticsEventTypes.ContentViewed)
            .Select(row => row.ActorKey)
            .Distinct()
            .LongCountAsync(cancellationToken);

    public Task<long> CountDistinctViewersForOwnerDayAsync(
        Guid ownerUserId, DateOnly day, CancellationToken cancellationToken = default) =>
        Events
            .AsNoTracking()
            .Where(row => row.OwnerUserId == ownerUserId
                && row.OccurredOn == day
                && row.ActorKey != null
                && row.EventType == AnalyticsEventTypes.ContentViewed)
            .Select(row => row.ActorKey)
            .Distinct()
            .LongCountAsync(cancellationToken);

    public async Task<IReadOnlyList<(DateOnly Day, Guid OwnerUserId)>> ListOwnerCellsAsync(
        DateOnly fromDay, DateOnly toDay, CancellationToken cancellationToken = default)
    {
        var rows = await Events
            .AsNoTracking()
            .Where(row => row.OccurredOn >= fromDay
                && row.OccurredOn <= toDay
                && row.OwnerUserId != null)
            .Select(row => new { row.OccurredOn, row.OwnerUserId })
            .Distinct()
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => (row.OccurredOn, row.OwnerUserId!.Value))
            .ToList();
    }

    public async Task<IReadOnlyList<(DateOnly Day, AnalyticsContentType ContentType, Guid ContentId)>>
        ListContentCellsAsync(
            DateOnly fromDay, DateOnly toDay, CancellationToken cancellationToken = default)
    {
        var rows = await Events
            .AsNoTracking()
            .Where(row => row.OccurredOn >= fromDay
                && row.OccurredOn <= toDay
                && row.ContentId != null)
            .Select(row => new { row.OccurredOn, row.ContentType, row.ContentId })
            .Distinct()
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => (row.OccurredOn, row.ContentType, row.ContentId!.Value))
            .ToList();
    }

    /// <summary>
    /// Distinct viewers of one owner's content over a window.
    /// <para>
    /// <b>Server-side, and the shape matters.</b> <c>Select(actor).Distinct().Count()</c> translates
    /// to <c>COUNT(DISTINCT actor_key)</c>; materialising the actors and counting them in memory
    /// would be the client-side aggregation §25 forbids, and would load one row per view.
    /// </para>
    /// <para>
    /// Filtered to the view event alone. A distinct count over every event class would answer "how
    /// many people did anything", which is a different question wearing the same name.
    /// </para>
    /// </summary>
    public async Task<long> CountDistinctViewersForOwnerAsync(
        Guid ownerUserId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default) =>
        await Events
            .AsNoTracking()
            .Where(row => row.OwnerUserId == ownerUserId
                && row.OccurredAt >= from
                && row.OccurredAt < to
                && row.ActorKey != null
                && row.EventType == AnalyticsEventTypes.ContentViewed)
            .Select(row => row.ActorKey)
            .Distinct()
            .LongCountAsync(cancellationToken);

    public async Task<long> CountDistinctViewersForOwnerContentTypeAsync(
        Guid ownerUserId,
        AnalyticsContentType contentType,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default) =>
        await Events
            .AsNoTracking()
            .Where(row => row.OwnerUserId == ownerUserId
                && row.ContentType == contentType
                && row.OccurredAt >= from
                && row.OccurredAt < to
                && row.ActorKey != null
                && row.EventType == AnalyticsEventTypes.ContentViewed)
            .Select(row => row.ActorKey)
            .Distinct()
            .LongCountAsync(cancellationToken);

    public async Task<long> CountDistinctViewersForContentAsync(
        AnalyticsContentType contentType,
        Guid contentId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default) =>
        await Events
            .AsNoTracking()
            .Where(row => row.ContentType == contentType
                && row.ContentId == contentId
                && row.OccurredAt >= from
                && row.OccurredAt < to
                && row.ActorKey != null
                && row.EventType == AnalyticsEventTypes.ContentViewed)
            .Select(row => row.ActorKey)
            .Distinct()
            .LongCountAsync(cancellationToken);

    public async Task<IReadOnlyList<AnalyticsEvent>> ListUnattributedAsync(
        AnalyticsContentType contentType,
        Guid contentId,
        int limit,
        CancellationToken cancellationToken = default) =>
        await Events
            .Where(row => row.ContentType == contentType
                && row.ContentId == contentId
                && row.OwnerUserId == null)
            .OrderBy(row => row.ReceivedAt)
            .Take(Math.Max(1, limit))
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Retention, in bounded batches.
    /// <para>
    /// Loaded and removed rather than deleted by predicate, because a bounded delete is what keeps a
    /// retention pass from taking a table lock for the length of the oldest deployment's backlog.
    /// The caller saves; this stages.
    /// </para>
    /// </summary>
    public async Task<int> DeleteExpiredAsync(
        DateTimeOffset olderThan, int batchSize, CancellationToken cancellationToken = default)
    {
        var doomed = await Events
            .Where(row => row.OccurredAt < olderThan)
            .OrderBy(row => row.OccurredAt)
            .Take(Math.Max(1, batchSize))
            .ToListAsync(cancellationToken);

        if (doomed.Count == 0)
        {
            return 0;
        }

        Events.RemoveRange(doomed);

        return doomed.Count;
    }

    public async Task<int> DeleteForActorAsync(
        string actorKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorKey);

        var doomed = await Events
            .Where(row => row.ActorKey == actorKey)
            .ToListAsync(cancellationToken);

        if (doomed.Count == 0)
        {
            return 0;
        }

        Events.RemoveRange(doomed);

        return doomed.Count;
    }

    public async Task<long> CountFunnelJourneysAsync(
        AnalyticsFunnelScope scope,
        string reached,
        IReadOnlyList<string> required,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(reached);
        ArgumentNullException.ThrowIfNull(required);

        var contentType = scope.ContentType;

        var journeys = InFunnelScope(scope, from, to)
            .Where(row => row.EventType == reached);

        // Every required step must have been reached by the same person on the same item inside the
        // same window. One correlated EXISTS per step, so the database narrows and nothing is loaded.
        foreach (var step in required)
        {
            var earlier = step;

            journeys = journeys.Where(row => Events.Any(prior =>
                prior.EventType == earlier
                && prior.ContentType == contentType
                && prior.ContentId == row.ContentId
                && prior.ActorKey == row.ActorKey
                && prior.OccurredAt >= from
                && prior.OccurredAt < to));
        }

        return await journeys
            .Select(row => new { row.ActorKey, row.ContentId })
            .Distinct()
            .LongCountAsync(cancellationToken);
    }

    private IQueryable<AnalyticsEvent> InFunnelScope(
        AnalyticsFunnelScope scope, DateTimeOffset from, DateTimeOffset to)
    {
        var contentType = scope.ContentType;

        var rows = Events
            .AsNoTracking()
            .Where(row => row.ContentType == contentType
                && row.ContentId != null
                && row.ActorKey != null
                && row.OccurredAt >= from
                && row.OccurredAt < to);

        if (scope.ContentId is { } contentId)
        {
            rows = rows.Where(row => row.ContentId == contentId);
        }

        if (scope.OwnerUserId is { } ownerUserId)
        {
            rows = rows.Where(row => row.OwnerUserId == ownerUserId);
        }

        return rows;
    }

    public async Task<IReadOnlyList<(string ActorKey, DateOnly Day)>> ListActorDaysAsync(
        AnalyticsActivityScope scope,
        DateOnly fromDay,
        DateOnly toDay,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);

        var rows = Events
            .AsNoTracking()
            .Where(row => row.ActorKey != null && row.OccurredOn >= fromDay && row.OccurredOn <= toDay);

        if (scope.OwnerUserId is { } ownerUserId)
        {
            rows = rows.Where(row => row.OwnerUserId == ownerUserId);
        }

        if (scope.ContentType is { } contentType)
        {
            rows = rows.Where(row => row.ContentType == contentType);
        }

        if (scope.ContentId is { } contentId)
        {
            rows = rows.Where(row => row.ContentId == contentId);
        }

        var pairs = await rows
            .Select(row => new { ActorKey = row.ActorKey!, row.OccurredOn })
            .Distinct()
            .ToListAsync(cancellationToken);

        return pairs.Select(pair => (pair.ActorKey, pair.OccurredOn)).ToList();
    }

    public async Task<IReadOnlyList<(string? Surface, long Views)>> CountOwnerViewsBySurfaceAsync(
        Guid ownerUserId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        var groups = await Events
            .AsNoTracking()
            .Where(row => row.OwnerUserId == ownerUserId
                && row.EventType == AnalyticsEventTypes.ContentViewed
                && row.OccurredAt >= from
                && row.OccurredAt < to)
            .GroupBy(row => row.Surface)
            .Select(group => new { Surface = group.Key, Views = group.LongCount() })
            .ToListAsync(cancellationToken);

        return groups.Select(group => (group.Surface, group.Views)).ToList();
    }
}

/// <summary>Reads and writes the content dimension.</summary>
public sealed class AnalyticsContentDimensionRepository : IAnalyticsContentDimensionRepository
{
    private readonly DiasporaDbContext _dbContext;

    public AnalyticsContentDimensionRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<AnalyticsContentDimension> Dimensions => _dbContext.Set<AnalyticsContentDimension>();

    public void Add(AnalyticsContentDimension dimension) => Dimensions.Add(dimension);

    /// <summary>
    /// One dimension, staged or saved.
    /// <para>
    /// The change tracker is consulted first because a consumer that opens a dimension and
    /// immediately writes to it does both in one unit of work, and a lookup that only asked the
    /// database would open a second row for the unique index to refuse at save time.
    /// </para>
    /// </summary>
    public async Task<AnalyticsContentDimension?> FindAsync(
        AnalyticsContentType contentType, Guid contentId, CancellationToken cancellationToken = default)
    {
        var staged = _dbContext.ChangeTracker
            .Entries<AnalyticsContentDimension>()
            .Select(entry => entry.Entity)
            .FirstOrDefault(row => row.ContentType == contentType && row.ContentId == contentId);

        if (staged is not null)
        {
            return staged;
        }

        return await Dimensions
            .FirstOrDefaultAsync(
                row => row.ContentType == contentType && row.ContentId == contentId,
                cancellationToken);
    }

    /// <summary>
    /// A page's worth of dimensions in one query.
    /// <para>
    /// Filtered on the identifier alone and narrowed in memory. A predicate over the composite pair
    /// would need an <c>OR</c> per key, which most providers translate into a scan; the identifier
    /// is a Guid and a collision across two content types is possible but vanishingly rare, so the
    /// index does the work and the type check removes the rare extra row.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<AnalyticsContentDimension>> ListAsync(
        IReadOnlyCollection<(AnalyticsContentType ContentType, Guid ContentId)> keys,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(keys);

        var ids = keys.Select(key => key.ContentId).Distinct().ToList();

        if (ids.Count == 0)
        {
            return [];
        }

        var candidates = await Dimensions
            .AsNoTracking()
            .Where(row => ids.Contains(row.ContentId))
            .ToListAsync(cancellationToken);

        var wanted = keys.ToHashSet();

        return candidates
            .Where(row => wanted.Contains((row.ContentType, row.ContentId)))
            .ToList();
    }

    public async Task<IReadOnlyList<AnalyticsContentDimension>> ListForOwnerAsync(
        Guid ownerUserId, CancellationToken cancellationToken = default) =>
        await Dimensions
            .AsNoTracking()
            .Where(row => row.OwnerUserId == ownerUserId)
            .ToListAsync(cancellationToken);
}

/// <summary>Reads and writes the daily rollups.</summary>
public sealed class AnalyticsRollupRepository : IAnalyticsRollupRepository
{
    private readonly DiasporaDbContext _dbContext;

    public AnalyticsRollupRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<ContentDailyRollup> ContentRollups => _dbContext.Set<ContentDailyRollup>();

    private DbSet<OwnerDailyRollup> OwnerRollups => _dbContext.Set<OwnerDailyRollup>();

    public void Add(ContentDailyRollup rollup) => ContentRollups.Add(rollup);

    public void Add(OwnerDailyRollup rollup) => OwnerRollups.Add(rollup);

    public async Task<ContentDailyRollup?> FindContentAsync(
        AnalyticsContentType contentType,
        Guid contentId,
        DateOnly day,
        CancellationToken cancellationToken = default)
    {
        var staged = _dbContext.ChangeTracker
            .Entries<ContentDailyRollup>()
            .Select(entry => entry.Entity)
            .FirstOrDefault(row =>
                row.ContentType == contentType && row.ContentId == contentId && row.Day == day);

        if (staged is not null)
        {
            return staged;
        }

        return await ContentRollups.FirstOrDefaultAsync(
            row => row.ContentType == contentType && row.ContentId == contentId && row.Day == day,
            cancellationToken);
    }

    public async Task<OwnerDailyRollup?> FindOwnerAsync(
        Guid ownerUserId, DateOnly day, CancellationToken cancellationToken = default)
    {
        var staged = _dbContext.ChangeTracker
            .Entries<OwnerDailyRollup>()
            .Select(entry => entry.Entity)
            .FirstOrDefault(row => row.OwnerUserId == ownerUserId && row.Day == day);

        if (staged is not null)
        {
            return staged;
        }

        return await OwnerRollups.FirstOrDefaultAsync(
            row => row.OwnerUserId == ownerUserId && row.Day == day, cancellationToken);
    }

    public async Task<IReadOnlyList<OwnerDailyRollup>> ListOwnerAsync(
        Guid ownerUserId,
        DateOnly fromDay,
        DateOnly toDay,
        CancellationToken cancellationToken = default) =>
        await OwnerRollups
            .AsNoTracking()
            .Where(row => row.OwnerUserId == ownerUserId && row.Day >= fromDay && row.Day <= toDay)
            .OrderBy(row => row.Day)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ContentDailyRollup>> ListContentForOwnerAsync(
        Guid ownerUserId,
        DateOnly fromDay,
        DateOnly toDay,
        CancellationToken cancellationToken = default) =>
        await ContentRollups
            .AsNoTracking()
            .Where(row => row.OwnerUserId == ownerUserId && row.Day >= fromDay && row.Day <= toDay)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ContentDailyRollup>> ListContentAsync(
        AnalyticsContentType contentType,
        Guid contentId,
        DateOnly fromDay,
        DateOnly toDay,
        CancellationToken cancellationToken = default) =>
        await ContentRollups
            .AsNoTracking()
            .Where(row => row.ContentType == contentType
                && row.ContentId == contentId
                && row.Day >= fromDay
                && row.Day <= toDay)
            .OrderBy(row => row.Day)
            .ToListAsync(cancellationToken);

    public async Task<bool> AnyOwnerBeforeAsync(
        Guid ownerUserId, DateOnly day, CancellationToken cancellationToken = default) =>
        await OwnerRollups
            .AsNoTracking()
            .AnyAsync(row => row.OwnerUserId == ownerUserId && row.Day < day, cancellationToken);

    public async Task<bool> AnyOwnerContentTypeBeforeAsync(
        Guid ownerUserId,
        AnalyticsContentType contentType,
        DateOnly day,
        CancellationToken cancellationToken = default) =>
        await ContentRollups
            .AsNoTracking()
            .AnyAsync(
                row => row.OwnerUserId == ownerUserId && row.ContentType == contentType && row.Day < day,
                cancellationToken);

    public async Task<bool> AnyContentBeforeAsync(
        AnalyticsContentType contentType,
        Guid contentId,
        DateOnly day,
        CancellationToken cancellationToken = default) =>
        await ContentRollups
            .AsNoTracking()
            .AnyAsync(
                row => row.ContentType == contentType && row.ContentId == contentId && row.Day < day,
                cancellationToken);

    public async Task<IReadOnlyList<(AnalyticsContentType ContentType, Guid ContentId)>> ListContentRecordedBeforeAsync(
        Guid ownerUserId, DateOnly day, CancellationToken cancellationToken = default)
    {
        var keys = await ContentRollups
            .AsNoTracking()
            .Where(row => row.OwnerUserId == ownerUserId && row.Day < day)
            .Select(row => new { row.ContentType, row.ContentId })
            .Distinct()
            .ToListAsync(cancellationToken);

        return keys.Select(key => (key.ContentType, key.ContentId)).ToList();
    }

    public async Task<IReadOnlyList<AnalyticsItemTotals>> SumAllContentAsync(
        DateOnly fromDay, DateOnly toDay, CancellationToken cancellationToken = default)
    {
        var groups = await ContentRollups
            .AsNoTracking()
            .Where(row => row.Day >= fromDay && row.Day <= toDay)
            .GroupBy(row => new { row.ContentType, row.ContentId })
            .Select(group => new
            {
                group.Key.ContentType,
                group.Key.ContentId,
                Views = group.Sum(row => row.Counts.Views),
                Saves = group.Sum(row => row.Counts.Saves),
                Shares = group.Sum(row => row.Counts.Shares),
                Comments = group.Sum(row => row.Counts.Comments),
                Reactions = group.Sum(row => row.Counts.Reactions),
                HelpfulVotes = group.Sum(row => row.Counts.HelpfulVotes),
                Conversions = group.Sum(row => row.Counts.Conversions),
                Enquiries = group.Sum(row => row.Counts.Enquiries)
            })
            .ToListAsync(cancellationToken);

        return groups
            .Select(group => new AnalyticsItemTotals(
                group.ContentType,
                group.ContentId,
                group.Views,
                group.Saves,
                group.Shares,
                group.Comments,
                group.Reactions,
                group.HelpfulVotes,
                group.Conversions,
                group.Enquiries))
            .ToList();
    }

    public async Task<int> DeleteExpiredAsync(
        DateOnly olderThan, int batchSize, CancellationToken cancellationToken = default)
    {
        var limit = Math.Max(1, batchSize);

        var doomedContent = await ContentRollups
            .Where(row => row.Day < olderThan)
            .OrderBy(row => row.Day)
            .Take(limit)
            .ToListAsync(cancellationToken);

        var doomedOwner = await OwnerRollups
            .Where(row => row.Day < olderThan)
            .OrderBy(row => row.Day)
            .Take(limit)
            .ToListAsync(cancellationToken);

        ContentRollups.RemoveRange(doomedContent);
        OwnerRollups.RemoveRange(doomedOwner);

        return doomedContent.Count + doomedOwner.Count;
    }
}

/// <summary>Reads and writes the projection cursor and the backfill runs.</summary>
public sealed class AnalyticsProjectionRepository : IAnalyticsProjectionRepository
{
    private readonly DiasporaDbContext _dbContext;

    public AnalyticsProjectionRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<AnalyticsProjectionCheckpoint> Checkpoints =>
        _dbContext.Set<AnalyticsProjectionCheckpoint>();

    private DbSet<AnalyticsBackfillRun> Runs => _dbContext.Set<AnalyticsBackfillRun>();

    public async Task<AnalyticsProjectionCheckpoint?> FindCheckpointAsync(
        CancellationToken cancellationToken = default)
    {
        var staged = _dbContext.ChangeTracker
            .Entries<AnalyticsProjectionCheckpoint>()
            .Select(entry => entry.Entity)
            .FirstOrDefault();

        return staged ?? await Checkpoints.FirstOrDefaultAsync(cancellationToken);
    }

    public void Add(AnalyticsProjectionCheckpoint checkpoint) => Checkpoints.Add(checkpoint);

    public void Add(AnalyticsBackfillRun run) => Runs.Add(run);

    public async Task<AnalyticsBackfillRun?> FindRunAsync(
        AnalyticsBackfillRunId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return await Runs.FirstOrDefaultAsync(run => run.Id == id, cancellationToken);
    }

    public async Task<AnalyticsBackfillRun?> FindActiveRunAsync(
        CancellationToken cancellationToken = default) =>
        await Runs.FirstOrDefaultAsync(
            run => run.Status == AnalyticsBackfillStatus.Queued
                || run.Status == AnalyticsBackfillStatus.Running,
            cancellationToken);
}

/// <summary>Reads and writes the event schema registry.</summary>
public sealed class AnalyticsEventSchemaRepository : IAnalyticsEventSchemaRepository
{
    private readonly DiasporaDbContext _dbContext;

    public AnalyticsEventSchemaRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<AnalyticsEventSchema> Schemas => _dbContext.Set<AnalyticsEventSchema>();

    public void Add(AnalyticsEventSchema schema) => Schemas.Add(schema);

    public async Task<IReadOnlyList<AnalyticsEventSchema>> ListAsync(
        CancellationToken cancellationToken = default) =>
        await Schemas.AsNoTracking().OrderBy(row => row.EventType).ToListAsync(cancellationToken);

    public async Task<AnalyticsEventSchema?> FindAsync(
        string eventType, int version, CancellationToken cancellationToken = default) =>
        await Schemas
            .AsNoTracking()
            .FirstOrDefaultAsync(
                row => row.EventType == eventType && row.Version == version, cancellationToken);

    public async Task<bool> ExistsAsync(string eventType, CancellationToken cancellationToken = default) =>
        await Schemas.AsNoTracking().AnyAsync(row => row.EventType == eventType, cancellationToken);
}

/// <summary>Reads and writes saved report definitions. Every read is filtered to the owner.</summary>
public sealed class AnalyticsSavedReportRepository : IAnalyticsSavedReportRepository
{
    private readonly DiasporaDbContext _dbContext;

    public AnalyticsSavedReportRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<AnalyticsSavedReport> Reports => _dbContext.Set<AnalyticsSavedReport>();

    public void Add(AnalyticsSavedReport report) => Reports.Add(report);

    public void Remove(AnalyticsSavedReport report) => Reports.Remove(report);

    public Task<AnalyticsSavedReport?> FindOwnedAsync(
        AnalyticsSavedReportId id, Guid ownerUserId, CancellationToken cancellationToken = default) =>
        Reports.FirstOrDefaultAsync(
            report => report.Id == id && report.OwnerUserId == ownerUserId, cancellationToken);

    public async Task<IReadOnlyList<AnalyticsSavedReport>> ListOwnedAsync(
        Guid ownerUserId, bool favoritesOnly, int limit, CancellationToken cancellationToken = default)
    {
        var rows = Reports.AsNoTracking().Where(report => report.OwnerUserId == ownerUserId);

        if (favoritesOnly)
        {
            rows = rows.Where(report => report.IsFavorite);
        }

        return await rows
            .OrderByDescending(report => report.UpdatedAt)
            .Take(Math.Max(1, limit))
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountOwnedAsync(Guid ownerUserId, CancellationToken cancellationToken = default) =>
        Reports.CountAsync(report => report.OwnerUserId == ownerUserId, cancellationToken);
}

/// <summary>Reads and writes requested exports. Every read names the owner or the link's token.</summary>
public sealed class AnalyticsExportRepository : IAnalyticsExportRepository
{
    private readonly DiasporaDbContext _dbContext;

    public AnalyticsExportRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<AnalyticsExport> Exports => _dbContext.Set<AnalyticsExport>();

    public void Add(AnalyticsExport export) => Exports.Add(export);

    public Task<AnalyticsExport?> FindOwnedAsync(
        AnalyticsExportId id, Guid ownerUserId, CancellationToken cancellationToken = default) =>
        Exports
            .AsNoTracking()
            .FirstOrDefaultAsync(
                export => export.Id == id && export.OwnerUserId == ownerUserId, cancellationToken);

    public Task<AnalyticsExport?> FindByTokenAsync(
        AnalyticsExportId id, string downloadToken, CancellationToken cancellationToken = default) =>
        Exports
            .AsNoTracking()
            .FirstOrDefaultAsync(
                export => export.Id == id && export.DownloadToken == downloadToken, cancellationToken);
}
