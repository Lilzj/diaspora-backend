namespace Diaspora.Persistence.Atomic;

/// <summary>
/// One column inside a term, and whether it is added or subtracted.
/// </summary>
public readonly record struct AtomicTermColumn(string Path, bool IsSubtracted);

/// <summary>
/// A sum of columns and a constant — the only arithmetic a guarded update needs.
/// <para>
/// <b>Deliberately linear, and deliberately integral.</b> Every scarce resource this platform will
/// ever guard is a count: units, seats, beds, slots, tickets. A term of the form
/// <c>±col ±col … ± constant</c> expresses "what is left" for all of them, translates to the same
/// SQL on every provider, and cannot be used to smuggle business logic into the persistence layer —
/// which a general expression language could.
/// </para>
/// <para>
/// The canonical use is one comparison: <c>Total − Held − Consumed ≥ n</c>. Everything else the
/// brief lists — a counter above zero, a compare-and-set, a bounded increment — is that shape with
/// fewer columns.
/// </para>
/// </summary>
public sealed class AtomicTerm
{
    private readonly List<AtomicTermColumn> _columns;

    internal AtomicTerm(IEnumerable<AtomicTermColumn> columns, long constant)
    {
        _columns = [.. columns];
        Constant = constant;
    }

    /// <summary>The columns the term reads, in the order they were declared.</summary>
    public IReadOnlyList<AtomicTermColumn> Columns => _columns;

    /// <summary>The literal added to the sum. The whole term for a bare number.</summary>
    public long Constant { get; }

    /// <summary>A term that is nothing but a number — the right-hand side of most guards.</summary>
    public static AtomicTerm Number(long value) => new([], value);
}
