namespace Diaspora.Domain.Abstractions;

/// <summary>
/// A write failed for a transient reason that the platform could not absorb, and the transaction
/// was rolled back. The operation did not happen; retrying the whole request is safe.
/// <para>
/// <b>Why the platform sometimes declines to retry.</b> A retrying execution strategy replays the
/// entire operation. That is only safe while the change tracker still holds the work — and once a
/// <c>SaveChanges</c> inside the transaction has been accepted, it does not: the entities are
/// <c>Unchanged</c>, so a replay would run against an empty tracker, write nothing, commit an empty
/// transaction, and report success for work that was rolled back.
/// </para>
/// <para>
/// The platform detects that situation and raises this instead. It is strictly better than the
/// alternatives: replaying would lose data silently, and retrying blindly would risk writing twice.
/// Failing here is loud, attributable, and leaves the database in exactly the state it was in
/// before the request.
/// </para>
/// <para>
/// Declared beside <see cref="IUnitOfWork"/> rather than in the persistence layer for the same
/// reason <see cref="ConcurrencyConflictException"/> is: the failure modes of an abstraction belong
/// with the abstraction, and nothing above the persistence boundary may reference EF Core. It maps
/// to <c>503 Service Unavailable</c>, because the honest instruction to the caller is "this did not
/// happen; try again".
/// </para>
/// </summary>
public sealed class TransientTransactionFailureException : Exception
{
    public TransientTransactionFailureException()
        : base("A transient failure rolled back the transaction. The operation did not happen.") { }

    public TransientTransactionFailureException(string message) : base(message) { }

    public TransientTransactionFailureException(string message, Exception innerException)
        : base(message, innerException) { }

    /// <summary>How many attempts the execution strategy made before the platform stopped it.</summary>
    public int Attempts { get; init; }
}
