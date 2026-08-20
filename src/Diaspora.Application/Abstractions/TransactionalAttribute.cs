namespace Diaspora.Application.Abstractions;

/// <summary>
/// Marks a command whose handler writes more than once and must do so atomically.
/// <para>
/// Opt-in, not automatic. A handler that calls <c>SaveChangesAsync</c> exactly once is
/// already atomic — EF wraps a single save in its own transaction — and wrapping it again
/// buys nothing but a longer-held connection. Requiring the attribute makes the handful of
/// genuinely multi-write handlers visible in a grep, which is worth more than a blanket
/// policy nobody can audit.
/// </para>
/// <para>
/// Queries are never wrapped, with or without the attribute.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class TransactionalAttribute : Attribute
{
    /// <summary>
    /// Why this command needs a transaction. Required in spirit if not by the compiler —
    /// the next person to read the handler should not have to reconstruct the reasoning.
    /// </summary>
    public string? Reason { get; init; }
}
