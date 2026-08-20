namespace Diaspora.Domain.Abstractions;

/// <summary>
/// The persistence boundary a handler writes through.
/// </summary>
public interface IUnitOfWork : IDisposable
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether a transaction opened by this unit of work is currently in flight.
    /// <para>
    /// Read by the transaction behavior so a command dispatched from inside another
    /// command joins the outer transaction instead of opening a nested one. Nested
    /// transactions are not what anyone means when they write that code, and on most
    /// providers they are not what they get either.
    /// </para>
    /// </summary>
    bool HasActiveTransaction { get; }

    /// <summary>
    /// Runs <paramref name="operation"/> inside a database transaction, committing on
    /// success and rolling back on any exception.
    /// <para>
    /// Deliberately a callback rather than the begin/commit/rollback triple it replaces.
    /// The provider is configured to retry transient failures, and a retry must replay the
    /// <em>whole</em> unit of work, not resume a transaction whose connection has gone. Only
    /// the implementation knows how to do that, so only the implementation owns the loop —
    /// which also means a handler cannot forget the rollback.
    /// </para>
    /// <para>
    /// Re-entrant: when a transaction is already in flight the operation runs inside it,
    /// and the caller does not own the commit.
    /// </para>
    /// </summary>
    Task<TResult> ExecuteTransactionalAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default);
}
