namespace Diaspora.Domain.Abstractions;

/// <summary>
/// Thrown when a write lost a race: the row it meant to update had already been changed by
/// someone else.
/// <para>
/// Declared here rather than letting EF Core's <c>DbUpdateConcurrencyException</c> travel
/// upward. The application layer is not allowed to reference EF Core — an architecture test
/// enforces that — and more to the point, "somebody else got there first" is a fact about
/// the domain, not about the ORM that noticed it. A future module on a different store
/// raises the same exception.
/// </para>
/// <para>
/// The persistence layer translates at the boundary, in
/// <c>DiasporaDbContext.SaveChangesAsync</c>.
/// </para>
/// </summary>
public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException()
        : base("The record was modified by another request.")
    {
    }

    public ConcurrencyConflictException(string message)
        : base(message)
    {
    }

    public ConcurrencyConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
