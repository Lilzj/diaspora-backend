using Diaspora.Platform.Messaging.Outbox.Abstractions;
using Diaspora.Platform.Messaging.Outbox.Model;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Persistence.Outbox;

/// <summary>
/// Retention sweeps and operator repairs, on EF Core.
/// <para>
/// Every purge is bounded and every one deletes by a cutoff that an index covers. A retention
/// sweep that table-scans is worse than no retention sweep: it runs on a timer, holds locks on
/// the busiest table in the platform, and does so most expensively exactly when the table is
/// largest.
/// </para>
/// </summary>
public sealed class EfOutboxMaintenance : IOutboxMaintenance
{
    private readonly DiasporaDbContext _context;

    public EfOutboxMaintenance(DiasporaDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Removes delivered messages.
    /// <para>
    /// The cutoff is <c>DeliveredAt</c>, not <c>EnqueuedAt</c>. A message that took three days
    /// of retries before succeeding was delivered a minute ago, and purging it because it was
    /// written on Monday would erase the only record of the incident it belongs to.
    /// </para>
    /// </summary>
    public Task<int> PurgeDeliveredAsync(
        DateTimeOffset deliveredBefore, int batchSize, CancellationToken cancellationToken = default) =>
        Bounded(
            _context.Set<OutboxMessage>()
                .Where(message =>
                    message.State == OutboxState.Delivered &&
                    message.DeliveredAt != null &&
                    message.DeliveredAt < deliveredBefore)
                .OrderBy(message => message.Sequence)
                .Select(message => message.Sequence),
            batchSize,
            sequences => _context.Set<OutboxMessage>()
                .Where(message => sequences.Contains(message.Sequence)),
            cancellationToken);

    public Task<int> PurgeDeadLettersAsync(
        DateTimeOffset deadLetteredBefore, int batchSize, CancellationToken cancellationToken = default) =>
        Bounded(
            _context.Set<OutboxDeadLetter>()
                .Where(deadLetter => deadLetter.DeadLetteredAt < deadLetteredBefore)
                .OrderBy(deadLetter => deadLetter.Id)
                .Select(deadLetter => deadLetter.Id),
            batchSize,
            ids => _context.Set<OutboxDeadLetter>().Where(deadLetter => ids.Contains(deadLetter.Id)),
            cancellationToken);

    public Task<int> PurgeAttemptsAsync(
        DateTimeOffset startedBefore, int batchSize, CancellationToken cancellationToken = default) =>
        Bounded(
            _context.Set<OutboxDeliveryAttempt>()
                .Where(attempt => attempt.StartedAt < startedBefore)
                .OrderBy(attempt => attempt.Id)
                .Select(attempt => attempt.Id),
            batchSize,
            ids => _context.Set<OutboxDeliveryAttempt>().Where(attempt => ids.Contains(attempt.Id)),
            cancellationToken);

    public async Task<bool> ReplayDeadLetterAsync(
        long deadLetterId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var replayed = await _context.ExecuteTransactionalAsync(
            async token =>
            {
                var deadLetter = await _context.Set<OutboxDeadLetter>()
                    .FirstOrDefaultAsync(candidate => candidate.Id == deadLetterId, token);

                if (deadLetter is null)
                {
                    return 0;
                }

                return await ReenqueueAsync([deadLetter], now, token);
            },
            cancellationToken);

        return replayed > 0;
    }

    public Task<int> ReplayDeadLettersAsync(
        OutboxQuery query, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return _context.ExecuteTransactionalAsync(
            async token =>
            {
                var deadLetters = await FilterForReplay(_context.Set<OutboxDeadLetter>(), query)
                    .OrderBy(deadLetter => deadLetter.Id)
                    .Take(query.Limit)
                    .ToListAsync(token);

                return deadLetters.Count == 0 ? 0 : await ReenqueueAsync(deadLetters, now, token);
            },
            cancellationToken);
    }

    public async Task<bool> RetryNowAsync(
        long sequence, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        // Restricted to pending messages. Clearing the backoff on a message a worker currently
        // holds would make it eligible while it is still being published, and the operator
        // would have caused the duplicate they were trying to avoid.
        var updated = await _context.Set<OutboxMessage>()
            .Where(message => message.Sequence == sequence && message.State == OutboxState.Pending)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(message => message.NextAttemptAt, (DateTimeOffset?)now)
                    .SetProperty(message => message.ClaimId, (Guid?)null)
                    .SetProperty(message => message.LeaseOwner, (string?)null)
                    .SetProperty(message => message.LeaseExpiresAt, (DateTimeOffset?)null),
                cancellationToken);

        return updated > 0;
    }

    /// <summary>
    /// Writes the messages back and stamps the dead letters as replayed, in one transaction.
    /// <para>
    /// The dead letter is kept rather than deleted. "We already replayed that on Tuesday and it
    /// failed again" is a question that comes up during every long incident, and a row that
    /// deletes itself on replay cannot answer it.
    /// </para>
    /// </summary>
    private async Task<int> ReenqueueAsync(
        List<OutboxDeadLetter> deadLetters, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var messages = _context.Set<OutboxMessage>();

        foreach (var deadLetter in deadLetters)
        {
            messages.Add(OutboxMessage.Replay(deadLetter, now));
            deadLetter.MarkReplayed(now);
        }

        await _context.SaveChangesAsync(cancellationToken);

        return deadLetters.Count;
    }

    /// <summary>
    /// A dead letter already put back is excluded, so running a bulk replay twice does not
    /// enqueue everything twice. The unique index on <c>MessageId</c> would have caught it, but
    /// as a failed insert rolling back the whole batch rather than as a no-op.
    /// </summary>
    private static IQueryable<OutboxDeadLetter> FilterForReplay(
        IQueryable<OutboxDeadLetter> deadLetters, OutboxQuery query)
    {
        deadLetters = deadLetters.Where(deadLetter => deadLetter.ReplayedAt == null);

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

    /// <summary>
    /// Deletes at most <paramref name="batchSize"/> rows, chosen by key.
    /// <para>
    /// Two statements rather than one <c>DELETE … LIMIT</c>, because <c>LIMIT</c> on a delete is
    /// not portable — PostgreSQL does not support it at all. Selecting the keys first is the
    /// portable form and keeps the delete's lock footprint to exactly the rows named.
    /// </para>
    /// </summary>
    private static async Task<int> Bounded<TEntity, TKey>(
        IQueryable<TKey> keys,
        int batchSize,
        Func<List<TKey>, IQueryable<TEntity>> target,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        var batch = await keys.Take(batchSize).ToListAsync(cancellationToken);

        return batch.Count == 0 ? 0 : await target(batch).ExecuteDeleteAsync(cancellationToken);
    }
}
