using Diaspora.Platform.Operations.Resilience;

namespace Diaspora.Operations.Resilience;

/// <summary>
/// Bounds how many calls a subsystem may have in flight at once.
/// <para>
/// The failure a bulkhead exists to stop: the notification provider slows from 100ms to 30 seconds,
/// every request that sends a notification parks on it, and within a minute every thread and every
/// database connection in the process is waiting on notifications. Sign-in — which does not send a
/// notification and has nothing to do with the provider — starts failing, and the outage is now
/// platform-wide because of one slow third party.
/// </para>
/// <para>
/// A semaphore is the whole implementation, and that is the point. The design work is deciding which
/// subsystems get separate ones and how large each is; those numbers live in the queue and rate-limit
/// policies rather than here.
/// </para>
/// </summary>
public sealed class Bulkhead : IDisposable
{
    private readonly SemaphoreSlim _slots;
    private readonly int _queueLimit;

    private int _queued;
    private long _rejections;

    public Bulkhead(string name, int capacity, int queueLimit = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(queueLimit);

        Name = name;
        Capacity = capacity;
        _queueLimit = queueLimit;
        _slots = new SemaphoreSlim(capacity, capacity);
    }

    public string Name { get; }

    public int Capacity { get; }

    /// <summary>
    /// Takes a slot, or refuses.
    /// <para>
    /// Refusing immediately when the queue is full is the behaviour that makes a bulkhead useful.
    /// An unbounded queue converts "too many concurrent calls" into "unbounded memory and unbounded
    /// latency", which is the same outage arriving more slowly.
    /// </para>
    /// </summary>
    /// <exception cref="BulkheadFullException">The bulkhead and its queue are both full.</exception>
    public async Task<IDisposable> EnterAsync(CancellationToken cancellationToken = default)
    {
        if (_slots.Wait(0, CancellationToken.None))
        {
            return new Slot(_slots);
        }

        if (Interlocked.Increment(ref _queued) > _queueLimit)
        {
            Interlocked.Decrement(ref _queued);
            Interlocked.Increment(ref _rejections);

            throw new BulkheadFullException(Name, Capacity);
        }

        try
        {
            await _slots.WaitAsync(cancellationToken).ConfigureAwait(false);
            return new Slot(_slots);
        }
        finally
        {
            Interlocked.Decrement(ref _queued);
        }
    }

    public BulkheadState Snapshot() => new(
        Name,
        Capacity - _slots.CurrentCount,
        Capacity,
        Volatile.Read(ref _queued),
        Interlocked.Read(ref _rejections));

    public void Dispose() => _slots.Dispose();

    /// <summary>
    /// The occupied slot. Released on dispose, which the executor does in a <c>finally</c>.
    /// <para>
    /// A struct would avoid the allocation and would be a mistake: an <c>IDisposable</c> struct
    /// returned through an interface is boxed anyway, and one that is accidentally copied releases
    /// the semaphore twice.
    /// </para>
    /// </summary>
    private sealed class Slot : IDisposable
    {
        private SemaphoreSlim? _owner;

        public Slot(SemaphoreSlim owner) => _owner = owner;

        public void Dispose() => Interlocked.Exchange(ref _owner, null)?.Release();
    }
}
