namespace Diaspora.Persistence.Atomic.Reservations;

/// <summary>
/// The reservation shape, written once over <see cref="IAtomicUpdateExecutor"/>.
/// <para>
/// <b>There is almost nothing here, and that is the point.</b> Each operation is three or four
/// lines: an assignment, a guard where one belongs, and a read-back. The value is not the code — it
/// is that the four operations are decided in one place, so no module has to work out for itself
/// that a release must clamp and a hold must not, and no two modules can answer that question
/// differently.
/// </para>
/// </summary>
public sealed class AtomicReservationStore : IAtomicReservationStore
{
    private readonly IAtomicUpdateExecutor _executor;

    public AtomicReservationStore(IAtomicUpdateExecutor executor)
    {
        _executor = executor;
    }

    public Task<AtomicReservationOutcome> ReserveAsync<TEntity>(
        AtomicReservation<TEntity> reservation, CancellationToken cancellationToken = default)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(reservation);

        var capacity = reservation.Capacity;

        var update = Begin(reservation)
            .Increment(capacity.Held, reservation.Units)
            .Guard(term => capacity.Remaining(term, reservation.Units));

        return RunAsync(update, capacity, cancellationToken);
    }

    public Task<AtomicReservationOutcome> ReleaseAsync<TEntity>(
        AtomicReservation<TEntity> reservation, CancellationToken cancellationToken = default)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(reservation);

        var capacity = reservation.Capacity;

        var update = Begin(reservation)
            .DecrementToZero(capacity.Held, reservation.Units);

        return RunAsync(update, capacity, cancellationToken);
    }

    public Task<AtomicReservationOutcome> ConsumeAsync<TEntity>(
        AtomicReservation<TEntity> reservation, CancellationToken cancellationToken = default)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(reservation);

        var capacity = RequireConsumed(reservation.Capacity, nameof(ConsumeAsync));

        // Both right-hand sides read the row as it was before the statement, so whatever the held
        // column actually gives up is exactly what the consumed column receives. A drifted pair
        // stays consistent instead of quietly gaining a unit.
        var update = Begin(reservation)
            .DecrementToZero(capacity.Held, reservation.Units)
            .IncrementLimited(capacity.Consumed!, reservation.Units, capacity.Held);

        return RunAsync(update, capacity, cancellationToken);
    }

    public Task<AtomicReservationOutcome> ConsumeDirectlyAsync<TEntity>(
        AtomicReservation<TEntity> reservation, CancellationToken cancellationToken = default)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(reservation);

        var capacity = RequireConsumed(reservation.Capacity, nameof(ConsumeDirectlyAsync));

        var update = Begin(reservation)
            .Increment(capacity.Consumed!, reservation.Units)
            .Guard(term => capacity.Remaining(term, reservation.Units));

        return RunAsync(update, capacity, cancellationToken);
    }

    public Task<AtomicReservationOutcome> RestoreAsync<TEntity>(
        AtomicReservation<TEntity> reservation, CancellationToken cancellationToken = default)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(reservation);

        var capacity = RequireConsumed(reservation.Capacity, nameof(RestoreAsync));

        var update = Begin(reservation)
            .DecrementToZero(capacity.Consumed!, reservation.Units);

        return RunAsync(update, capacity, cancellationToken);
    }

    private static AtomicUpdate<TEntity> Begin<TEntity>(AtomicReservation<TEntity> reservation)
        where TEntity : class =>
        AtomicUpdate.For<TEntity>(reservation.Key)
            .Synchronizing(reservation.Synchronize);

    private async Task<AtomicReservationOutcome> RunAsync<TEntity>(
        AtomicUpdate<TEntity> update,
        AtomicCapacity<TEntity> capacity,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        update.ReadingBack(capacity.Total).ReadingBack(capacity.Held);

        if (capacity.Consumed is not null)
        {
            update.ReadingBack(capacity.Consumed);
        }

        var result = await _executor.ExecuteAsync(update, cancellationToken);

        if (!result.Applied)
        {
            return new AtomicReservationOutcome(applied: false, state: null);
        }

        var total = (int)(result.Value(capacity.TotalPath) ?? 0);
        var held = (int)(result.Value(capacity.HeldPath) ?? 0);
        var consumed = capacity.ConsumedPath is { } path ? (int)(result.Value(path) ?? 0) : 0;

        return new AtomicReservationOutcome(
            applied: true, new AtomicReservationState(total, held, consumed));
    }

    private static AtomicCapacity<TEntity> RequireConsumed<TEntity>(
        AtomicCapacity<TEntity> capacity, string operation)
        where TEntity : class
    {
        if (capacity.Consumed is null)
        {
            throw new InvalidOperationException(
                $"{operation} moves units into a consumed state, and this capacity declares none. " +
                "Declare one, or use Reserve and Release only.");
        }

        return capacity;
    }
}
