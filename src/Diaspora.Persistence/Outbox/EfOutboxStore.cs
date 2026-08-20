using Diaspora.Platform.Messaging.Outbox.Abstractions;
using Diaspora.Platform.Messaging.Outbox.Model;
using Diaspora.Platform.Messaging.Outbox.Options;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Persistence.Outbox;

/// <summary>
/// The dispatcher's durable operations, on EF Core.
/// <para>
/// <b>Nothing here goes through the change tracker except the dead-letter insert.</b> Every
/// other operation is a single set-based statement, which matters for two reasons: it is one
/// round trip instead of a read-modify-write, and — more importantly — a conditional
/// <c>UPDATE</c> whose <c>WHERE</c> clause restates the precondition is atomic against every
/// other worker without a lock. That is the whole of the competing-consumers story.
/// </para>
/// <para>
/// <b>No <c>SKIP LOCKED</c>.</b> It is the obvious PostgreSQL answer and it would have made the
/// claim untestable: SQLite has no equivalent, the integration suite runs on SQLite, and a
/// durability component whose concurrency control is only exercised in production is not a
/// durability component. Claiming by conditional update with a claim identifier is portable,
/// equally correct, and costs one extra round trip per pass.
/// </para>
/// </summary>
public sealed class EfOutboxStore : IOutboxStore
{
    private readonly DiasporaDbContext _context;

    public EfOutboxStore(DiasporaDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<OutboxMessage>> ClaimAsync(
        OutboxClaimRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var messages = _context.Set<OutboxMessage>();

        if (request.Ordering == OutboxOrderingMode.Global &&
            await IsAnythingInFlightAsync(messages, request.Now, cancellationToken))
        {
            // Global ordering means exactly one message may be in flight platform-wide. A
            // message waiting on backoff blocks everything behind it, which is the guarantee
            // this mode exists to provide and the reason almost nobody should choose it.
            return [];
        }

        var eligible = Eligible(messages, request.Now, request.TenantIds);

        if (request.Ordering == OutboxOrderingMode.Partitioned)
        {
            // A partition with a message in flight, or one waiting out a backoff, is skipped
            // entirely. Without this the claim would happily take message three of a partition
            // whose message two is thirty seconds from its next attempt, and deliver them out
            // of order while every individual guarantee still looked satisfied.
            var blocked = messages
                .Where(message =>
                    (message.State == OutboxState.Claimed && message.LeaseExpiresAt > request.Now) ||
                    (message.State == OutboxState.Pending &&
                     message.NextAttemptAt != null &&
                     message.NextAttemptAt > request.Now))
                .Select(message => message.PartitionKey);

            eligible = eligible.Where(message => !blocked.Contains(message.PartitionKey));
        }

        var candidates = await eligible
            .OrderBy(message => message.Sequence)
            .Take(request.BatchSize)
            .Select(message => message.Sequence)
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
        {
            return [];
        }

        // The predicate is restated, not assumed. Between selecting the candidates and running
        // this update another worker may have taken some of them; those rows simply do not
        // match, and this worker gets the remainder. One winner per row, no lock, no retry.
        var claimed = await messages
            .Where(message =>
                candidates.Contains(message.Sequence) &&
                message.State == OutboxState.Pending &&
                (message.LeaseExpiresAt == null || message.LeaseExpiresAt <= request.Now))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(message => message.State, OutboxState.Claimed)
                    .SetProperty(message => message.ClaimId, (Guid?)request.ClaimId)
                    .SetProperty(message => message.LeaseOwner, request.WorkerId)
                    .SetProperty(message => message.LeaseExpiresAt, (DateTimeOffset?)request.LeaseExpiresAt),
                cancellationToken);

        if (claimed == 0)
        {
            return [];
        }

        // Read back by claim identifier rather than by the candidate list: it returns exactly
        // the rows this worker won and nothing another worker took in between.
        return await messages
            .AsNoTracking()
            .Where(message => message.ClaimId == request.ClaimId)
            .OrderBy(message => message.Sequence)
            .ToListAsync(cancellationToken);
    }

    public Task MarkDeliveredAsync(
        long sequence, DateTimeOffset deliveredAt, CancellationToken cancellationToken = default) =>
        _context.Set<OutboxMessage>()
            .Where(message => message.Sequence == sequence)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(message => message.State, OutboxState.Delivered)
                    .SetProperty(message => message.DeliveredAt, (DateTimeOffset?)deliveredAt)
                    .SetProperty(message => message.LastAttemptAt, (DateTimeOffset?)deliveredAt)
                    .SetProperty(message => message.AttemptCount, message => message.AttemptCount + 1)
                    .SetProperty(message => message.NextAttemptAt, (DateTimeOffset?)null)
                    .SetProperty(message => message.LastError, (string?)null)
                    .SetProperty(message => message.ClaimId, (Guid?)null)
                    .SetProperty(message => message.LeaseOwner, (string?)null)
                    .SetProperty(message => message.LeaseExpiresAt, (DateTimeOffset?)null),
                cancellationToken);

    public Task MarkFailedAsync(
        long sequence,
        DateTimeOffset attemptedAt,
        DateTimeOffset nextAttemptAt,
        string error,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(error);

        var truncated = Truncate(error);

        return _context.Set<OutboxMessage>()
            .Where(message => message.Sequence == sequence)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(message => message.State, OutboxState.Pending)
                    // Incremented in SQL rather than read and written back: two workers that
                    // both failed the same message under an expired lease must not each write
                    // "one", losing an attempt from a budget that is meant to be finite.
                    .SetProperty(message => message.AttemptCount, message => message.AttemptCount + 1)
                    .SetProperty(message => message.LastAttemptAt, (DateTimeOffset?)attemptedAt)
                    .SetProperty(message => message.NextAttemptAt, (DateTimeOffset?)nextAttemptAt)
                    .SetProperty(message => message.LastError, truncated)
                    .SetProperty(message => message.ClaimId, (Guid?)null)
                    .SetProperty(message => message.LeaseOwner, (string?)null)
                    .SetProperty(message => message.LeaseExpiresAt, (DateTimeOffset?)null),
                cancellationToken);
    }

    /// <summary>
    /// Moves a message to the dead-letter table.
    /// <para>
    /// The insert and the delete are one transaction, and they have to be. A message that
    /// existed in both tables would be delivered again after being given up on; a message in
    /// neither would be the silent loss this entire phase was written to eliminate.
    /// </para>
    /// </summary>
    public Task DeadLetterAsync(
        long sequence,
        OutboxDeadLetterReason reason,
        string error,
        DateTimeOffset deadLetteredAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(error);

        return _context.ExecuteTransactionalAsync(
            async token =>
            {
                var message = await _context.Set<OutboxMessage>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(candidate => candidate.Sequence == sequence, token);

                if (message is null)
                {
                    return 0;
                }

                _context.Set<OutboxDeadLetter>()
                    .Add(message.ToDeadLetter(reason, error, deadLetteredAt));

                await _context.SaveChangesAsync(token);

                return await _context.Set<OutboxMessage>()
                    .Where(candidate => candidate.Sequence == sequence)
                    .ExecuteDeleteAsync(token);
            },
            cancellationToken);
    }

    public async Task RecordAttemptAsync(
        OutboxDeliveryAttempt attempt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(attempt);

        _context.Set<OutboxDeliveryAttempt>().Add(attempt);

        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task<int> RecoverExpiredLeasesAsync(
        DateTimeOffset now, CancellationToken cancellationToken = default) =>
        _context.Set<OutboxMessage>()
            .Where(message =>
                message.State == OutboxState.Claimed &&
                message.LeaseExpiresAt != null &&
                message.LeaseExpiresAt <= now)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(message => message.State, OutboxState.Pending)
                    // Eligible immediately. The message has already waited out a whole lease;
                    // applying a backoff on top would punish it for the worker's crash.
                    .SetProperty(message => message.NextAttemptAt, (DateTimeOffset?)now)
                    .SetProperty(message => message.ClaimId, (Guid?)null)
                    .SetProperty(message => message.LeaseOwner, (string?)null)
                    .SetProperty(message => message.LeaseExpiresAt, (DateTimeOffset?)null),
                cancellationToken);

    /// <summary>
    /// Messages a worker may take: pending, past their backoff, and not covered by a live lease.
    /// </summary>
    internal static IQueryable<OutboxMessage> Eligible(
        IQueryable<OutboxMessage> messages,
        DateTimeOffset now,
        IReadOnlyCollection<string> tenantIds)
    {
        var eligible = messages.Where(message =>
            message.State == OutboxState.Pending &&
            (message.NextAttemptAt == null || message.NextAttemptAt <= now) &&
            (message.LeaseExpiresAt == null || message.LeaseExpiresAt <= now));

        if (tenantIds.Count > 0)
        {
            // A tenant-scoped worker takes only its own tenants' messages. An untenanted
            // message is nobody's and is left to a worker with no filter.
            eligible = eligible.Where(message =>
                message.TenantId != null && tenantIds.Contains(message.TenantId));
        }

        return eligible;
    }

    private static Task<bool> IsAnythingInFlightAsync(
        IQueryable<OutboxMessage> messages, DateTimeOffset now, CancellationToken cancellationToken) =>
        messages.AnyAsync(
            message =>
                (message.State == OutboxState.Claimed && message.LeaseExpiresAt > now) ||
                (message.State == OutboxState.Pending &&
                 message.NextAttemptAt != null &&
                 message.NextAttemptAt > now),
            cancellationToken);

    private static string Truncate(string value) =>
        value.Length <= OutboxFieldLimits.Error ? value : value[..OutboxFieldLimits.Error];
}
