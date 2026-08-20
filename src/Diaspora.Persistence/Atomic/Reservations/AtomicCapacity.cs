using System.Linq.Expressions;

namespace Diaspora.Persistence.Atomic.Reservations;

/// <summary>
/// Three columns that between them describe a finite resource: how much exists, how much is
/// promised, and how much is gone.
/// <para>
/// <b>The shape is the whole abstraction, and it is deliberately anaemic.</b> There is no room, no
/// unit, no seat and no ticket here — only <see cref="Total"/>, <see cref="Held"/> and
/// <see cref="Consumed"/>. Marketplace maps them to quantity, reserved and sold; Housing to total
/// units, reserved units and occupied units; a ticketing module would map them to allocation, held
/// and issued, and an appointment book to slots, provisional and confirmed. The persistence layer
/// never learns which it is doing, which is what stops the next vertical needing a second
/// primitive.
/// </para>
/// <para>
/// <b><see cref="Consumed"/> is optional</b> because some resources are only ever held and released
/// — a rate limit, a concurrent-session count, a parking bay. Where it is absent the arithmetic
/// simply loses a term, and the operations that would move units into it are refused rather than
/// silently doing half a job.
/// </para>
/// </summary>
public sealed class AtomicCapacity<TEntity>
    where TEntity : class
{
    private AtomicCapacity(
        Expression<Func<TEntity, int>> total,
        Expression<Func<TEntity, int>> held,
        Expression<Func<TEntity, int>>? consumed)
    {
        Total = total;
        Held = held;
        Consumed = consumed;

        TotalPath = AtomicPath.Of(total);
        HeldPath = AtomicPath.Of(held);
        ConsumedPath = consumed is null ? null : AtomicPath.Of(consumed);
    }

    /// <summary>How much of the resource exists.</summary>
    public Expression<Func<TEntity, int>> Total { get; }

    /// <summary>How much is promised to somebody but not yet taken.</summary>
    public Expression<Func<TEntity, int>> Held { get; }

    /// <summary>How much has been taken, where that is a state the resource has.</summary>
    public Expression<Func<TEntity, int>>? Consumed { get; }

    internal string TotalPath { get; }

    internal string HeldPath { get; }

    internal string? ConsumedPath { get; }

    /// <summary>A resource with a consumed state — stock that sells, rooms that fill.</summary>
    public static AtomicCapacity<TEntity> Of(
        Expression<Func<TEntity, int>> total,
        Expression<Func<TEntity, int>> held,
        Expression<Func<TEntity, int>> consumed) =>
        new(total, held, consumed);

    /// <summary>A resource that is only ever held and released.</summary>
    public static AtomicCapacity<TEntity> Of(
        Expression<Func<TEntity, int>> total,
        Expression<Func<TEntity, int>> held) =>
        new(total, held, consumed: null);

    /// <summary>
    /// <c>Total − Held [− Consumed] ≥ units</c>: the condition every guarded reservation turns on.
    /// </summary>
    internal AtomicCondition Remaining(AtomicTermBuilder<TEntity> term, int units)
    {
        ArgumentNullException.ThrowIfNull(term);

        var running = term.Column(Total).Minus(Held);

        if (Consumed is not null)
        {
            running = running.Minus(Consumed);
        }

        return running.AtLeast(units);
    }
}
