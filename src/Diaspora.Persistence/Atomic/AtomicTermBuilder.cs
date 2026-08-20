using System.Linq.Expressions;

namespace Diaspora.Persistence.Atomic;

/// <summary>
/// Writes a guard the way it reads on paper.
/// <para>
/// <c>Column(total).Minus(held).Minus(consumed).AtLeast(units)</c> is the whole grammar, and it is
/// the same sentence B10 and B11 each wrote in prose in their evolution reports before either of
/// them could write it in code.
/// </para>
/// </summary>
public sealed class AtomicTermBuilder<TEntity>
    where TEntity : class
{
    private readonly List<AtomicTermColumn> _columns = [];
    private long _constant;

    /// <summary>The first column of the term.</summary>
    public AtomicTermBuilder<TEntity> Column<TValue>(Expression<Func<TEntity, TValue>> property) =>
        Add(property, subtract: false);

    /// <summary>Adds a column to the running sum.</summary>
    public AtomicTermBuilder<TEntity> Plus<TValue>(Expression<Func<TEntity, TValue>> property) =>
        Add(property, subtract: false);

    /// <summary>Takes a column off the running sum.</summary>
    public AtomicTermBuilder<TEntity> Minus<TValue>(Expression<Func<TEntity, TValue>> property) =>
        Add(property, subtract: true);

    /// <summary>Adds a literal to the running sum.</summary>
    public AtomicTermBuilder<TEntity> Plus(long value)
    {
        _constant += value;
        return this;
    }

    /// <summary>Takes a literal off the running sum.</summary>
    public AtomicTermBuilder<TEntity> Minus(long value)
    {
        _constant -= value;
        return this;
    }

    /// <summary>… ≥ <paramref name="value"/>. The reservation guard.</summary>
    public AtomicCondition AtLeast(long value) =>
        Compare(AtomicComparison.GreaterThanOrEqual, value);

    /// <summary>… ≤ <paramref name="value"/>.</summary>
    public AtomicCondition AtMost(long value) =>
        Compare(AtomicComparison.LessThanOrEqual, value);

    /// <summary>… &gt; <paramref name="value"/>. A counter still above a floor.</summary>
    public AtomicCondition Above(long value) =>
        Compare(AtomicComparison.GreaterThan, value);

    /// <summary>… &lt; <paramref name="value"/>.</summary>
    public AtomicCondition Below(long value) =>
        Compare(AtomicComparison.LessThan, value);

    /// <summary>… = <paramref name="value"/>. The arithmetic half of a compare-and-set.</summary>
    public AtomicCondition EqualTo(long value) =>
        Compare(AtomicComparison.Equal, value);

    /// <summary>… ≠ <paramref name="value"/>.</summary>
    public AtomicCondition NotEqualTo(long value) =>
        Compare(AtomicComparison.NotEqual, value);

    private AtomicCondition Compare(AtomicComparison comparison, long value) =>
        AtomicCondition.Between(
            new AtomicTerm(_columns, _constant), comparison, AtomicTerm.Number(value));

    private AtomicTermBuilder<TEntity> Add<TValue>(
        Expression<Func<TEntity, TValue>> property, bool subtract)
    {
        _columns.Add(new AtomicTermColumn(AtomicPath.Of(property), subtract));
        return this;
    }
}
