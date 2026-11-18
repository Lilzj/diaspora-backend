namespace Diaspora.Domain.Abstractions;

/// <summary>
/// Thrown when a write was refused because a record with those details already exists: a unique
/// index said no.
/// <para>
/// Declared here beside <see cref="ConcurrencyConflictException"/> and for the same reason — the
/// application layer is not allowed to reference EF Core, and "that already exists" is a fact about
/// the data rather than about the ORM that noticed it. The persistence layer translates at the
/// boundary, in <c>DiasporaDbContext.PersistAsync</c>.
/// </para>
/// <para>
/// <b>Distinct from <see cref="ConcurrencyConflictException"/>, and the distinction is the reason
/// this type exists.</b> A concurrency conflict means the row you meant to change was changed by
/// someone else: reload and try again. A duplicate means the row you meant to create is already
/// there: the work is done, or the identifier is taken, and retrying unchanged will fail the same
/// way. Both are 409 and they ask the client for opposite things.
/// </para>
/// <para>
/// BBL-002. The platform declares 234 unique indexes outside migrations across 142 configuration
/// files, and before this type existed exactly three modules translated the resulting failure —
/// each with its own private copy of one message-matching predicate. Every other index answered a
/// concurrent duplicate with <c>500 Platform.Unexpected</c>, including the webhook replay index
/// whose own configuration documents the race it exists to win.
/// </para>
/// </summary>
public sealed class DuplicateRecordException : Exception
{
    public DuplicateRecordException()
        : base("A record with those details already exists.")
    {
    }

    public DuplicateRecordException(string message)
        : base(message)
    {
    }

    public DuplicateRecordException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
