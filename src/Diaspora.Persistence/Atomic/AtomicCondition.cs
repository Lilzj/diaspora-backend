namespace Diaspora.Persistence.Atomic;

/// <summary>The comparisons a guard may make. Nothing here is provider-specific.</summary>
public enum AtomicComparison
{
    Equal,
    NotEqual,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual
}

/// <summary>
/// A single clause of the <c>WHERE</c> the database arbitrates.
/// <para>
/// Two shapes, because two questions are worth asking about a row before changing it. The
/// <b>arithmetic</b> shape compares one <see cref="AtomicTerm"/> to another — <c>Total − Held −
/// Consumed ≥ n</c> — and is what makes a reservation safe. The <b>value</b> shape compares a
/// single column to a literal — <c>Status = 'Reserved'</c>, <c>Revision = 4</c> — and is what makes
/// a compare-and-set or a conditional state transition safe.
/// </para>
/// <para>
/// Conditions are always combined with <c>AND</c>. A guard that needed <c>OR</c> would be
/// expressing a business rule, and business rules belong to the aggregate; the persistence layer
/// only enforces the arithmetic the aggregate cannot enforce for itself.
/// </para>
/// </summary>
public sealed class AtomicCondition
{
    private AtomicCondition(
        AtomicTerm? left,
        AtomicTerm? right,
        string? valuePath,
        object? value,
        AtomicComparison comparison)
    {
        Left = left;
        Right = right;
        ValuePath = valuePath;
        Value = value;
        Comparison = comparison;
    }

    /// <summary>The left-hand term, on an arithmetic condition.</summary>
    public AtomicTerm? Left { get; }

    /// <summary>The right-hand term, on an arithmetic condition.</summary>
    public AtomicTerm? Right { get; }

    /// <summary>The column, on a value condition.</summary>
    public string? ValuePath { get; }

    /// <summary>
    /// The expected value, on a value condition. Passed through the property's own value converter
    /// before it reaches the database, so a strongly typed identifier or a converted enum compares
    /// in its stored form rather than its CLR one.
    /// </summary>
    public object? Value { get; }

    public AtomicComparison Comparison { get; }

    /// <summary>Whether this clause compares a column to a literal rather than two terms.</summary>
    public bool IsValueComparison => ValuePath is not null;

    internal static AtomicCondition Between(
        AtomicTerm left, AtomicComparison comparison, AtomicTerm right) =>
        new(left, right, valuePath: null, value: null, comparison);

    internal static AtomicCondition ForValue(
        string path, AtomicComparison comparison, object? value) =>
        new(left: null, right: null, path, value, comparison);
}
