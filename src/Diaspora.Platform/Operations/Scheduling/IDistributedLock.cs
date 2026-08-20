namespace Diaspora.Platform.Operations.Scheduling;

/// <summary>
/// A held lease. Released on dispose, and expired by the store if the holder dies.
/// </summary>
public interface ILeaseHandle : IAsyncDisposable
{
    /// <summary>What is held.</summary>
    string Resource { get; }

    /// <summary>Who holds it. A machine and process identifier, for diagnostics.</summary>
    string Owner { get; }

    /// <summary>When the lease lapses if it is not renewed.</summary>
    DateTimeOffset ExpiresAt { get; }

    /// <summary>
    /// Extends the lease.
    /// <para>
    /// Returns false when the lease has already lapsed and been taken by someone else — at which
    /// point the holder must stop what it is doing immediately, because another instance is now doing
    /// it. A renewal that silently re-acquired would produce two holders and no error.
    /// </para>
    /// </summary>
    Task<bool> RenewAsync(TimeSpan extension, CancellationToken cancellationToken = default);
}

/// <summary>
/// A mutual exclusion primitive that spans instances.
/// <para>
/// <b>DEBT-015's seam.</b> Every sweeper on this platform runs on every instance: the media orphan
/// sweep, the community counter sweep, the listing expiry, the lifecycle sweep, the outbox retention.
/// The work is idempotent so this is waste rather than corruption — three instances doing the same
/// scan produce the same result three times — and the mitigation on record is "disable sweepers on
/// all but one instance", which is a deployment convention that nothing enforces and that silently
/// stops working when the one instance is the one that restarts.
/// </para>
/// <para>
/// <b>Every lease has a time-to-live, and there is no way to ask for one that does not.</b> A lock
/// held by a process that has been killed must expire on its own or the resource is locked until a
/// human intervenes — which is the single most common way a distributed lock makes availability worse
/// than no lock at all.
/// </para>
/// </summary>
public interface IDistributedLock
{
    /// <summary>
    /// Takes the lease on <paramref name="resource"/>, or returns null immediately.
    /// <para>
    /// Non-blocking by design. A sweeper that could not take the lease should skip this cycle, not
    /// queue behind the instance that did — by the time it acquired, the work would already be done.
    /// </para>
    /// </summary>
    /// <param name="resource">What is being protected. Stable across instances and restarts.</param>
    /// <param name="timeToLive">
    /// How long the lease survives without renewal. Must exceed the longest plausible run of the
    /// protected work, or a slow run will lose its lease to a second instance mid-way.
    /// </param>
    Task<ILeaseHandle?> TryAcquireAsync(
        string resource,
        TimeSpan timeToLive,
        CancellationToken cancellationToken = default);

    /// <summary>Who holds a lease right now, or null. For the operational dashboard.</summary>
    Task<string?> FindHolderAsync(string resource, CancellationToken cancellationToken = default);
}
