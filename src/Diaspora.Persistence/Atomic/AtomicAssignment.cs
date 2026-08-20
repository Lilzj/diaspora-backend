namespace Diaspora.Persistence.Atomic;

/// <summary>
/// What an assignment does to the column it names.
/// <para>
/// The two clamped forms exist because releasing a resource must never fail. A release that could
/// be refused leaves a room held forever or stock nobody can buy, which is worse than the
/// double-booking the guarded forms prevent — so the clamps are expressed in SQL rather than left
/// to a caller who might forget them.
/// </para>
/// </summary>
public enum AtomicAssignmentKind
{
    /// <summary><c>col = @value</c>. The value passes through the property's value converter.</summary>
    Set,

    /// <summary><c>col = col + @delta</c>.</summary>
    Add,

    /// <summary><c>col = col - @delta</c>. Unclamped: pair it with a guard, or use the floored form.</summary>
    Subtract,

    /// <summary>
    /// <c>col = CASE WHEN col &gt;= @delta THEN col - @delta ELSE 0 END</c>. Never goes negative and
    /// never refuses.
    /// </summary>
    SubtractFloored,

    /// <summary>
    /// <c>col = col + CASE WHEN limit &gt;= @delta THEN @delta ELSE limit END</c> — add no more than
    /// another column holds. The other half of a floored move between two counters: whatever the
    /// source actually gave up is what the destination receives, so the pair cannot invent or lose
    /// a unit however far the counts have drifted.
    /// </summary>
    AddLimited
}

/// <summary>One column's new value, inside a single <c>UPDATE</c>.</summary>
public sealed class AtomicAssignment
{
    internal AtomicAssignment(
        string path, AtomicAssignmentKind kind, object? value, string? limitPath = null)
    {
        Path = path;
        Kind = kind;
        Value = value;
        LimitPath = limitPath;
    }

    public string Path { get; }

    public AtomicAssignmentKind Kind { get; }

    /// <summary>The delta on the arithmetic kinds; the new value on <see cref="AtomicAssignmentKind.Set"/>.</summary>
    public object? Value { get; }

    /// <summary>The bounding column, on <see cref="AtomicAssignmentKind.AddLimited"/>.</summary>
    public string? LimitPath { get; }
}
