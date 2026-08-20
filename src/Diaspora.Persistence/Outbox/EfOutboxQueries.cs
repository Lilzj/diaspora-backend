using Diaspora.Platform.Messaging.Outbox.Abstractions;
using Diaspora.Platform.Messaging.Outbox.Model;
using Diaspora.Platform.Messaging.Outbox.Statistics;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Persistence.Outbox;

/// <summary>
/// The read side: statistics for gauges and health, listings for operators.
/// <para>
/// Every query here is <c>AsNoTracking</c> and every one is bounded. Nothing on this class is
/// on a delivery path, and nothing on a delivery path is allowed to call it — an aggregate over
/// the hot table belongs on a five-second cache, not in the loop that drains the queue.
/// </para>
/// </summary>
public sealed class EfOutboxQueries : IOutboxQueries
{
    /// <summary>
    /// How many modules and event types the breakdowns report.
    /// <para>
    /// The breakdowns become metric dimensions, and an unbounded dimension is how a metrics
    /// backend acquires a cardinality problem. The busiest few are what an operator looks at
    /// anyway; the totals above them are exact regardless.
    /// </para>
    /// </summary>
    private const int BreakdownLimit = 25;

    private readonly DiasporaDbContext _context;

    public EfOutboxQueries(DiasporaDbContext context)
    {
        _context = context;
    }

    public async Task<OutboxStatistics> GetStatisticsAsync(
        DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var messages = _context.Set<OutboxMessage>().AsNoTracking();
        var deadLetters = _context.Set<OutboxDeadLetter>().AsNoTracking();

        // One grouped pass over the message table rather than four counts. The table is the
        // hottest in the platform and this runs on a timer on every instance.
        var byState = await messages
            .GroupBy(message => message.State)
            .Select(group => new OutboxStateCount(group.Key, group.Count()))
            .ToListAsync(cancellationToken);

        var retrying = await messages
            .CountAsync(
                message => message.State == OutboxState.Pending && message.AttemptCount > 0,
                cancellationToken);

        var oldestPending = await messages
            .Where(message => message.State != OutboxState.Delivered)
            .OrderBy(message => message.Sequence)
            .Select(message => (DateTimeOffset?)message.EnqueuedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var deadLetterCount = await deadLetters.CountAsync(cancellationToken);

        return new OutboxStatistics
        {
            Pending = CountFor(byState, OutboxState.Pending),
            Claimed = CountFor(byState, OutboxState.Claimed),
            Delivered = CountFor(byState, OutboxState.Delivered),
            Retrying = retrying,
            DeadLettered = deadLetterCount,
            OldestPendingAt = oldestPending,
            ObservedAt = now,
            ByModule = await ByModuleAsync(messages, deadLetters, cancellationToken),
            ByEventType = await ByEventTypeAsync(messages, deadLetters, cancellationToken)
        };
    }

    public async Task<IReadOnlyList<OutboxMessage>> ListPendingAsync(
        OutboxQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var messages = Filter(
            _context.Set<OutboxMessage>().AsNoTracking()
                .Where(message => message.State != OutboxState.Delivered),
            query);

        return await messages
            .OrderBy(message => message.Sequence)
            .Skip(query.Offset)
            .Take(query.Limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OutboxDeadLetter>> ListDeadLettersAsync(
        OutboxQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var deadLetters = FilterDeadLetters(
            _context.Set<OutboxDeadLetter>().AsNoTracking(), query);

        // Most recent first: during an incident the interesting dead letters are the ones that
        // arrived in the last ten minutes, not the ones from a fortnight ago.
        return await deadLetters
            .OrderByDescending(deadLetter => deadLetter.Id)
            .Skip(query.Offset)
            .Take(query.Limit)
            .ToListAsync(cancellationToken);
    }

    public Task<OutboxMessage?> FindMessageAsync(
        long sequence, CancellationToken cancellationToken = default) =>
        _context.Set<OutboxMessage>()
            .AsNoTracking()
            .FirstOrDefaultAsync(message => message.Sequence == sequence, cancellationToken);

    public Task<OutboxDeadLetter?> FindDeadLetterAsync(
        long id, CancellationToken cancellationToken = default) =>
        _context.Set<OutboxDeadLetter>()
            .AsNoTracking()
            .FirstOrDefaultAsync(deadLetter => deadLetter.Id == id, cancellationToken);

    public async Task<IReadOnlyList<OutboxDeliveryAttempt>> ListAttemptsAsync(
        Guid messageId, int limit, CancellationToken cancellationToken = default) =>
        await _context.Set<OutboxDeliveryAttempt>()
            .AsNoTracking()
            .Where(attempt => attempt.MessageId == messageId)
            .OrderBy(attempt => attempt.AttemptNumber)
            .Take(limit)
            .ToListAsync(cancellationToken);

    private static async Task<IReadOnlyList<OutboxModuleStatistics>> ByModuleAsync(
        IQueryable<OutboxMessage> messages,
        IQueryable<OutboxDeadLetter> deadLetters,
        CancellationToken cancellationToken)
    {
        var pending = await messages
            .Where(message => message.State != OutboxState.Delivered)
            .GroupBy(message => message.Module)
            .Select(group => new
            {
                Module = group.Key,
                Pending = group.Count(),
                Oldest = group.Min(message => (DateTimeOffset?)message.EnqueuedAt)
            })
            .OrderByDescending(row => row.Pending)
            .Take(BreakdownLimit)
            .ToListAsync(cancellationToken);

        var dead = await deadLetters
            .GroupBy(deadLetter => deadLetter.Module)
            .Select(group => new { Module = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        var deadByModule = dead.ToDictionary(row => row.Module, row => row.Count, StringComparer.Ordinal);

        // Modules that have nothing pending but do have dead letters would otherwise vanish from
        // the breakdown — which is the one case where a module most needs to appear.
        var modules = pending
            .Select(row => new OutboxModuleStatistics(
                row.Module,
                row.Pending,
                deadByModule.GetValueOrDefault(row.Module),
                row.Oldest))
            .ToList();

        var represented = modules.Select(module => module.Module).ToHashSet(StringComparer.Ordinal);

        modules.AddRange(dead
            .Where(row => !represented.Contains(row.Module))
            .Select(row => new OutboxModuleStatistics(row.Module, 0, row.Count, null)));

        return modules;
    }

    private static async Task<IReadOnlyList<OutboxEventTypeStatistics>> ByEventTypeAsync(
        IQueryable<OutboxMessage> messages,
        IQueryable<OutboxDeadLetter> deadLetters,
        CancellationToken cancellationToken)
    {
        var pending = await messages
            .Where(message => message.State != OutboxState.Delivered)
            .GroupBy(message => message.EventType)
            .Select(group => new
            {
                EventType = group.Key,
                Pending = group.Count(),
                MaxAttempts = group.Max(message => message.AttemptCount)
            })
            .OrderByDescending(row => row.Pending)
            .Take(BreakdownLimit)
            .ToListAsync(cancellationToken);

        var dead = await deadLetters
            .GroupBy(deadLetter => deadLetter.EventType)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        var deadByType = dead.ToDictionary(row => row.Key, row => row.Count, StringComparer.Ordinal);

        var types = pending
            .Select(row => new OutboxEventTypeStatistics(
                row.EventType,
                row.Pending,
                deadByType.GetValueOrDefault(row.EventType),
                row.MaxAttempts))
            .ToList();

        var represented = types.Select(type => type.EventType).ToHashSet(StringComparer.Ordinal);

        types.AddRange(dead
            .Where(row => !represented.Contains(row.Key))
            .Select(row => new OutboxEventTypeStatistics(row.Key, 0, row.Count, 0)));

        return types;
    }

    private static int CountFor(IEnumerable<OutboxStateCount> rows, OutboxState state) =>
        rows.FirstOrDefault(row => row.State == state)?.Count ?? 0;

    private static IQueryable<OutboxMessage> Filter(IQueryable<OutboxMessage> messages, OutboxQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.Module))
        {
            messages = messages.Where(message => message.Module == query.Module);
        }

        if (!string.IsNullOrWhiteSpace(query.EventType))
        {
            messages = messages.Where(message => message.EventType == query.EventType);
        }

        if (!string.IsNullOrWhiteSpace(query.TenantId))
        {
            messages = messages.Where(message => message.TenantId == query.TenantId);
        }

        if (!string.IsNullOrWhiteSpace(query.CorrelationId))
        {
            messages = messages.Where(message => message.CorrelationId == query.CorrelationId);
        }

        if (query.MinAttempts is { } minAttempts)
        {
            messages = messages.Where(message => message.AttemptCount >= minAttempts);
        }

        return messages;
    }

    private static IQueryable<OutboxDeadLetter> FilterDeadLetters(
        IQueryable<OutboxDeadLetter> deadLetters, OutboxQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.Module))
        {
            deadLetters = deadLetters.Where(deadLetter => deadLetter.Module == query.Module);
        }

        if (!string.IsNullOrWhiteSpace(query.EventType))
        {
            deadLetters = deadLetters.Where(deadLetter => deadLetter.EventType == query.EventType);
        }

        if (!string.IsNullOrWhiteSpace(query.TenantId))
        {
            deadLetters = deadLetters.Where(deadLetter => deadLetter.TenantId == query.TenantId);
        }

        if (!string.IsNullOrWhiteSpace(query.CorrelationId))
        {
            deadLetters = deadLetters.Where(deadLetter => deadLetter.CorrelationId == query.CorrelationId);
        }

        return deadLetters;
    }
}
