namespace Diaspora.Persistence.Atomic.Reservations;

/// <summary>
/// One request against one row's capacity.
/// <para>
/// The row is named by its key rather than handed over as an aggregate, because a guarded update is
/// a statement about a row and an implementation given a loaded aggregate is one step from writing
/// through the change tracker — the exact path that cannot be made atomic.
/// <see cref="Synchronize"/> is the deliberate exception: a caller that already holds the tracked
/// instance passes it so the executor can put it back in step afterwards, which is bookkeeping
/// rather than a way of deciding anything.
/// </para>
/// </summary>
public sealed class AtomicReservation<TEntity>
    where TEntity : class
{
    public AtomicReservation(AtomicCapacity<TEntity> capacity, object key, int units)
    {
        ArgumentNullException.ThrowIfNull(capacity);
        ArgumentNullException.ThrowIfNull(key);

        if (units < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(units), units, "A reservation moves at least one unit.");
        }

        Capacity = capacity;
        Key = key;
        Units = units;
    }

    public AtomicCapacity<TEntity> Capacity { get; }

    public object Key { get; }

    public int Units { get; }

    /// <summary>
    /// The tracked instance to reconcile once the statement has applied, where the caller holds one.
    /// </summary>
    public TEntity? Synchronize { get; init; }
}

/// <summary>The counts as the database holds them, after a statement applied.</summary>
public sealed record AtomicReservationState(int Total, int Held, int Consumed)
{
    /// <summary>What a caller could still take.</summary>
    public int Available => Math.Max(0, Total - Held - Consumed);
}

/// <summary>
/// What the database did with a reservation request.
/// <para>
/// <see cref="Applied"/> false on a reserve means the units are gone — or the row is — and it is an
/// ordinary outcome rather than a fault. Losing a race for the last room is not an error condition;
/// it is the answer to a question two people asked at once.
/// </para>
/// </summary>
public sealed class AtomicReservationOutcome
{
    internal AtomicReservationOutcome(bool applied, AtomicReservationState? state)
    {
        Applied = applied;
        State = state;
    }

    public bool Applied { get; }

    /// <summary>The post-statement counts. Null when nothing applied.</summary>
    public AtomicReservationState? State { get; }
}
