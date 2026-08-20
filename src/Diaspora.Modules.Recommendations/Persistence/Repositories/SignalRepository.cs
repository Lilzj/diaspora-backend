using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Domain.Signals;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Recommendations.Persistence.Repositories;

/// <summary>
/// EF Core repository for the signal record.
/// <para>
/// <b>Every read here is bounded and every one of them is ordered, because this is the largest table
/// in the schema and an unbounded read of it is the module's first out-of-memory failure.</b> The
/// bound is the caller's, not a default, so a caller that wants a hundred rows gets a hundred rather
/// than whatever a page size happened to be.
/// </para>
/// <para>
/// <b>The deletes go through the change tracker rather than through a set-based statement, and that
/// is B1.3's rule rather than this module's preference.</b> A vertical writing its own
/// <c>ExecuteDelete</c> writes it for one provider, tests it on one provider, and bypasses the
/// reconciliation the platform's persistence layer exists to run — so the platform owns the
/// primitive and modules use the tracker. The cost here is real and bounded: a retention batch is
/// materialised before it is removed, which is why the batch size is a parameter and why the sweep
/// loops rather than deleting a month in one statement.
/// </para>
/// </summary>
public sealed class SignalRepository : ISignalRepository
{
    private readonly DiasporaDbContext _dbContext;

    public SignalRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<UserSignal> Signals => _dbContext.Set<UserSignal>();

    public void Add(UserSignal signal)
    {
        ArgumentNullException.ThrowIfNull(signal);

        Signals.Add(signal);
    }

    public void AddRange(IEnumerable<UserSignal> signals)
    {
        ArgumentNullException.ThrowIfNull(signals);

        Signals.AddRange(signals);
    }

    public async Task<IReadOnlyList<UserSignal>> ListForUserAsync(
        UserId userId, int limit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        return await Signals
            .Where(signal => signal.UserId == userId)
            .OrderByDescending(signal => signal.OccurredAt)
            .Take(Math.Max(1, limit))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<UserSignal>> ListUnprocessedAsync(
        UserId userId, int limit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        // Oldest first: the exponential fold has to see them in the order they happened, or the
        // oldest observations get the most influence — the opposite of what the decay is for.
        return await Signals
            .Where(signal => signal.UserId == userId && signal.ProcessedAt == null)
            .OrderBy(signal => signal.OccurredAt)
            .Take(Math.Max(1, limit))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Members with work waiting, oldest signal first.
    /// <para>
    /// Grouped in the database rather than loading the signals and distinct-ing in memory: on a busy
    /// platform the unprocessed set is large and the answer is a handful of identifiers.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<UserId>> ListUsersWithUnprocessedSignalsAsync(
        int limit, CancellationToken cancellationToken = default) =>
        await Signals
            .Where(signal => signal.ProcessedAt == null)
            .GroupBy(signal => signal.UserId)
            .OrderBy(group => group.Min(signal => signal.OccurredAt))
            .Take(Math.Max(1, limit))
            .Select(group => group.Key)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<UserSignal>> ListForUserSinceAsync(
        UserId userId, DateTimeOffset since, int limit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        return await Signals
            .Where(signal => signal.UserId == userId && signal.OccurredAt >= since)
            .OrderByDescending(signal => signal.OccurredAt)
            .Take(Math.Max(1, limit))
            .ToListAsync(cancellationToken);
    }

    public async Task<UserSignal?> FindLatestAsync(
        UserId userId, SignalKind kind, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        return await Signals
            .Where(signal => signal.UserId == userId && signal.Kind == kind)
            .OrderByDescending(signal => signal.OccurredAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> ListEngagedTargetIdsAsync(
        UserId userId,
        RecommendationTargetType targetType,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        return await Signals
            .Where(signal => signal.UserId == userId &&
                             signal.TargetType == targetType &&
                             signal.TargetId != null)
            .OrderByDescending(signal => signal.OccurredAt)
            .Select(signal => signal.TargetId!.Value)
            .Distinct()
            .Take(Math.Max(1, limit))
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountForUserAsync(UserId userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        return Signals.CountAsync(signal => signal.UserId == userId, cancellationToken);
    }

    public async Task<DateTimeOffset?> FindLatestOccurrenceAsync(
        UserId userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        var latest = await Signals
            .Where(signal => signal.UserId == userId)
            .OrderByDescending(signal => signal.OccurredAt)
            .Select(signal => (DateTimeOffset?)signal.OccurredAt)
            .FirstOrDefaultAsync(cancellationToken);

        return latest;
    }

    /// <summary>
    /// Retention, in bounded batches.
    /// <para>
    /// Batched because the alternative is one statement holding a lock over a table the write path
    /// is inserting into continuously. The sweep calls this repeatedly until it returns less than a
    /// full batch.
    /// </para>
    /// </summary>
    public async Task<int> DeleteExpiredAsync(
        DateTimeOffset asOf, int batchSize, CancellationToken cancellationToken = default)
    {
        var doomed = await Signals
            .Where(signal => signal.ExpiresAt <= asOf)
            .OrderBy(signal => signal.ExpiresAt)
            .Take(Math.Max(1, batchSize))
            .ToListAsync(cancellationToken);

        if (doomed.Count == 0)
        {
            return 0;
        }

        Signals.RemoveRange(doomed);

        return doomed.Count;
    }

    /// <summary>
    /// Erasure.
    /// <para>
    /// Unbounded by design and correct: an erasure request must remove everything, and stopping at a
    /// batch would leave a member half-forgotten with nothing recording where the sweep got to.
    /// </para>
    /// </summary>
    public async Task<int> DeleteForUserAsync(
        UserId userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        var doomed = await Signals
            .Where(signal => signal.UserId == userId)
            .ToListAsync(cancellationToken);

        if (doomed.Count == 0)
        {
            return 0;
        }

        Signals.RemoveRange(doomed);

        return doomed.Count;
    }
}
