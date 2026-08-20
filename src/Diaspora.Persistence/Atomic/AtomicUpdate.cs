using System.Linq.Expressions;

namespace Diaspora.Persistence.Atomic;

/// <summary>
/// The entry point to a guarded update.
/// <para>
/// <b>This is the primitive B10 and B11 each closed by recommending, and each declined to build
/// privately.</b> Marketplace needed "decrement this stock if and only if there is still stock";
/// Housing needed "hold this room if and only if the room is still free". They are the same
/// statement, and every scarce resource the platform will carry next — seats, appointment slots,
/// tickets, tables — is that statement again with different column names.
/// </para>
/// <para>
/// <b>What it buys.</b> An aggregate that loads, decides and saves is correct only if nothing else
/// wrote the row in between. Optimistic concurrency catches the case where the two writers touch
/// the same fields for the same reason — two editors — and misses the case that actually costs
/// money: two <em>buyers</em>, neither editing anything, each holding the last unit against a count
/// they both read as one. There is no version conflict there because neither transaction is stale
/// in any way EF can see. A single statement whose <c>WHERE</c> re-checks the arithmetic the
/// database itself holds is the only thing that closes it, and this is that statement.
/// </para>
/// <para>
/// <b>It does not commit.</b> The statement runs on the caller's connection, inside the caller's
/// transaction, so the hold and the reservation that justifies it are one unit of work. A primitive
/// that committed on its own would open exactly the window it exists to close.
/// </para>
/// </summary>
public static class AtomicUpdate
{
    /// <summary>Begins an update against one row, named by its primary key value.</summary>
    /// <param name="key">
    /// The key as the aggregate holds it — a strongly typed identifier is fine, and is converted to
    /// its stored form by the property's own value converter before the statement is built.
    /// </param>
    public static AtomicUpdate<TEntity> For<TEntity>(object key)
        where TEntity : class =>
        new(key);
}

/// <summary>
/// One guarded <c>UPDATE</c>, described in terms of the aggregate rather than the table.
/// <para>
/// Mutable and fluent by design: it is built at the call site, handed to
/// <see cref="IAtomicUpdateExecutor"/>, and discarded. Nothing holds one.
/// </para>
/// </summary>
public sealed class AtomicUpdate<TEntity>
    where TEntity : class
{
    private readonly List<AtomicAssignment> _assignments = [];
    private readonly List<AtomicCondition> _conditions = [];
    private readonly List<string> _readBack = [];

    internal AtomicUpdate(object key)
    {
        Key = key ?? throw new ArgumentNullException(nameof(key));
    }

    /// <summary>The primary key value of the row being changed.</summary>
    public object Key { get; }

    public IReadOnlyList<AtomicAssignment> Assignments => _assignments;

    public IReadOnlyList<AtomicCondition> Conditions => _conditions;

    /// <summary>The paths whose post-statement values the caller wants read back.</summary>
    public IReadOnlyList<string> ReadBackPaths => _readBack;

    /// <summary>
    /// The tracked instance to reconcile once the statement has applied, if the caller holds one.
    /// </summary>
    public TEntity? Tracked { get; private set; }

    /// <summary><c>col = col + delta</c>.</summary>
    public AtomicUpdate<TEntity> Increment<TValue>(
        Expression<Func<TEntity, TValue>> property, int delta) =>
        Assign(new AtomicAssignment(AtomicPath.Of(property), AtomicAssignmentKind.Add, delta));

    /// <summary>
    /// <c>col = col - delta</c>, unclamped. Meaningful only with a guard that keeps it non-negative;
    /// use <see cref="DecrementToZero"/> where the operation must never fail.
    /// </summary>
    public AtomicUpdate<TEntity> Decrement<TValue>(
        Expression<Func<TEntity, TValue>> property, int delta) =>
        Assign(new AtomicAssignment(AtomicPath.Of(property), AtomicAssignmentKind.Subtract, delta));

    /// <summary>
    /// <c>col = col - delta</c>, floored at zero. The release path: it cannot fail, and it cannot
    /// drive a counter negative however far the counts have already drifted.
    /// </summary>
    public AtomicUpdate<TEntity> DecrementToZero<TValue>(
        Expression<Func<TEntity, TValue>> property, int delta) =>
        Assign(new AtomicAssignment(
            AtomicPath.Of(property), AtomicAssignmentKind.SubtractFloored, delta));

    /// <summary>
    /// <c>col = col + min(delta, limit)</c> — the receiving half of a floored move between two
    /// counters, bounded by whatever the source column actually held.
    /// </summary>
    public AtomicUpdate<TEntity> IncrementLimited<TValue, TLimit>(
        Expression<Func<TEntity, TValue>> property,
        int delta,
        Expression<Func<TEntity, TLimit>> limit) =>
        Assign(new AtomicAssignment(
            AtomicPath.Of(property),
            AtomicAssignmentKind.AddLimited,
            delta,
            AtomicPath.Of(limit)));

    /// <summary>
    /// <c>col = value</c>. The write half of a compare-and-update or a conditional state transition;
    /// pair it with <see cref="Requiring"/>.
    /// </summary>
    public AtomicUpdate<TEntity> Set<TValue>(
        Expression<Func<TEntity, TValue>> property, TValue value) =>
        Assign(new AtomicAssignment(AtomicPath.Of(property), AtomicAssignmentKind.Set, value));

    /// <summary>
    /// <c>AND col = expected</c>. The read half of a compare-and-update: the statement applies only
    /// if the column still holds what the caller last saw.
    /// </summary>
    public AtomicUpdate<TEntity> Requiring<TValue>(
        Expression<Func<TEntity, TValue>> property, TValue expected)
    {
        _conditions.Add(
            AtomicCondition.ForValue(AtomicPath.Of(property), AtomicComparison.Equal, expected));

        return this;
    }

    /// <summary><c>AND col &lt;&gt; value</c>.</summary>
    public AtomicUpdate<TEntity> Unless<TValue>(
        Expression<Func<TEntity, TValue>> property, TValue value)
    {
        _conditions.Add(
            AtomicCondition.ForValue(AtomicPath.Of(property), AtomicComparison.NotEqual, value));

        return this;
    }

    /// <summary>
    /// <c>AND (…arithmetic…)</c>. The clause that makes a reservation safe:
    /// <c>Guard(t =&gt; t.Column(x =&gt; x.Total).Minus(x =&gt; x.Held).Minus(x =&gt; x.Consumed).AtLeast(n))</c>.
    /// </summary>
    public AtomicUpdate<TEntity> Guard(
        Func<AtomicTermBuilder<TEntity>, AtomicCondition> guard)
    {
        ArgumentNullException.ThrowIfNull(guard);

        _conditions.Add(guard(new AtomicTermBuilder<TEntity>()));

        return this;
    }

    /// <summary>
    /// Reads the named columns back in the same transaction once the statement has applied.
    /// <para>
    /// One extra round trip on a row the statement already holds a lock on, and it is what makes
    /// the answer usable: the caller learns the counts the database now holds rather than the ones
    /// it guessed before the statement ran.
    /// </para>
    /// </summary>
    public AtomicUpdate<TEntity> ReadingBack<TValue>(
        Expression<Func<TEntity, TValue>> property)
    {
        var path = AtomicPath.Of(property);

        if (!_readBack.Contains(path, StringComparer.Ordinal))
        {
            _readBack.Add(path);
        }

        return this;
    }

    /// <summary>
    /// Reconciles a tracked instance with what was read back, and marks those columns as already
    /// persisted.
    /// <para>
    /// <b>Without this the statement is worse than useless.</b> An aggregate whose owned counters
    /// were mutated in memory has them marked modified, so the unit of work's own <c>UPDATE</c>
    /// writes the values it computed — over the values the database just arbitrated. The tracked
    /// entry has to be told that those particular columns are settled, and this is how.
    /// </para>
    /// </summary>
    public AtomicUpdate<TEntity> Synchronizing(TEntity? entity)
    {
        Tracked = entity;
        return this;
    }

    private AtomicUpdate<TEntity> Assign(AtomicAssignment assignment)
    {
        _assignments.Add(assignment);
        return this;
    }
}
