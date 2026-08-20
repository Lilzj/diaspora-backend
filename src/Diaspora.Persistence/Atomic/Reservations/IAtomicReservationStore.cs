namespace Diaspora.Persistence.Atomic.Reservations;

/// <summary>
/// The four things anybody ever does to a finite resource, each as one statement the database
/// arbitrates.
/// <para>
/// <b>This is the layer verticals consume.</b> <see cref="IAtomicUpdateExecutor"/> underneath it is
/// general enough to express a compare-and-set or a conditional state transition; this is the
/// reservation shape, named once so that Marketplace, Housing and whatever needs seats next do not
/// each rediscover which of the four operations may refuse and which must never.
/// </para>
/// <para>
/// <b>Only <see cref="ReserveAsync"/> can fail.</b> That asymmetry is the design. Taking a unit is
/// a claim on something finite and has to be arbitrated; giving one back is not, and a release that
/// could be refused leaves a room held forever or stock nobody can buy — which is worse than the
/// double-booking the guard exists to prevent. So the returning operations clamp in SQL and always
/// apply.
/// </para>
/// <para>
/// None of them commits. Every statement runs on the caller's connection inside the caller's
/// transaction, so the hold and whatever justifies it are one unit of work.
/// </para>
/// </summary>
public interface IAtomicReservationStore
{
    /// <summary>
    /// Takes units, if they are still there. <c>held += n</c> guarded on
    /// <c>total − held − consumed ≥ n</c>.
    /// <para>
    /// The one operation that closes B10's and B11's window: two callers reaching for the last unit
    /// both reach the database, and the second one's <c>WHERE</c> no longer holds.
    /// </para>
    /// </summary>
    Task<AtomicReservationOutcome> ReserveAsync<TEntity>(
        AtomicReservation<TEntity> reservation, CancellationToken cancellationToken = default)
        where TEntity : class;

    /// <summary>
    /// Gives held units back to the pool. Clamped at zero, never refused.
    /// </summary>
    Task<AtomicReservationOutcome> ReleaseAsync<TEntity>(
        AtomicReservation<TEntity> reservation, CancellationToken cancellationToken = default)
        where TEntity : class;

    /// <summary>
    /// Turns held units into taken ones — sold, occupied, issued. Moves no more than is actually
    /// held, so the pair of counters cannot invent or lose a unit however far they have drifted.
    /// </summary>
    Task<AtomicReservationOutcome> ConsumeAsync<TEntity>(
        AtomicReservation<TEntity> reservation, CancellationToken cancellationToken = default)
        where TEntity : class;

    /// <summary>
    /// Takes units straight out of the pool with no hold behind them — the sale done in person, the
    /// seat handed over at the door. Guarded exactly as <see cref="ReserveAsync"/> is, because it
    /// is the same claim on the same finite thing.
    /// </summary>
    Task<AtomicReservationOutcome> ConsumeDirectlyAsync<TEntity>(
        AtomicReservation<TEntity> reservation, CancellationToken cancellationToken = default)
        where TEntity : class;

    /// <summary>
    /// Returns taken units to the pool — a tenancy that ended, a ticket refunded. Clamped at zero,
    /// never refused. Meaningless for resources that are consumed permanently, which is why it is
    /// separate from <see cref="ReleaseAsync"/> rather than a flag on it.
    /// </summary>
    Task<AtomicReservationOutcome> RestoreAsync<TEntity>(
        AtomicReservation<TEntity> reservation, CancellationToken cancellationToken = default)
        where TEntity : class;
}
