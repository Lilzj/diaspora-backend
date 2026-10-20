using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityProgramme.Domain.Errors;
using Diaspora.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.CommunityProgramme.Application.Services;

/// <summary>
/// B15's idempotency primitive, applied to community writes.
/// <para>
/// <b>The same mechanism, not a second one.</b> B15 uniquely indexes an <c>IdempotencyKey</c> column
/// on payment intents, purchases, subscriptions, donations, contributions, withdrawals and reward
/// conversions; every one of its handlers looks the key up before writing and returns the existing
/// record on a repeat. This module does exactly that, on the aggregate each write creates. There is
/// no idempotency table, no key store, no TTL sweeper and no second uniqueness rule — the register
/// asked for the pattern to be copied rather than invented, and copying it is what that means.
/// </para>
/// <para>
/// <b>What this class adds is the concurrent case, which B15's handlers do not cover and B18 needs.</b>
/// A lookup-then-write is not atomic: two requests carrying one key can both find nothing and both
/// proceed, and the unique index — which is what makes the guarantee real — refuses the second at
/// commit. B15 lets that surface as a 500, which is defensible for a payment a member is watching and
/// is not for a poll vote a phone retried on a flaky train. Here the violation is caught, the key is
/// re-read, and one of two things is true:
/// </para>
/// <list type="bullet">
///   <item>
///     The first request has committed. Its record is found and <b>replayed</b> — the same response
///     the first caller got, which is the whole point of the key.
///   </item>
///   <item>
///     The first request has not committed yet. Nothing is found, and the answer is <b>409
///     <c>communities.idempotency.in-flight</c></b> — the shape the register asks for, and one the
///     client's retry policy already treats as retryable.
///   </item>
/// </list>
/// <para>
/// <b>What it deliberately does not do is retry the write.</b> A retry inside the handler would be a
/// second attempt at a body the first attempt may have half-applied, and the aggregate it staged is
/// still in the change tracker. Answering 409 and letting the client retry is a whole new request
/// against a clean context, which is the only version of this that is safe.
/// </para>
/// </summary>
public sealed class ProgrammeIdempotency
{
    /// <summary>
    /// Normalises a key from the header or the body.
    /// <para>
    /// The header wins where both are present. The register asks for "an <c>Idempotency-Key</c>
    /// header (or body field)"; the client sends the body field on every Phase 12 mutation, so
    /// accepting only the header would have meant a client release before a single application could
    /// be submitted.
    /// </para>
    /// <para>
    /// <b>Scoped by operation, and that is not decoration.</b> Two different operations can
    /// legitimately be retried with the same client-generated key — the client scopes its keys by
    /// operation and subject, but nothing stops a caller sending "abc" to two routes — and a bare key
    /// would make the second replay the first's record. The prefix makes each operation's key space
    /// its own.
    /// </para>
    /// </summary>
    public static string? Normalise(string operation, string? header, string? body)
    {
        var raw = !string.IsNullOrWhiteSpace(header) ? header : body;

        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var trimmed = raw.Trim();

        return trimmed.Length > 150
            ? $"{operation}:{trimmed[..150]}"
            : $"{operation}:{trimmed}";
    }

    /// <summary>
    /// Guards a keyed write, resolving a concurrent duplicate.
    /// </summary>
    /// <param name="key">The normalised key, or null when the caller sent none.</param>
    /// <param name="save">
    /// The handler's own save.
    /// <para>
    /// <b>Passed in rather than performed here, and that is not indirection for its own sake.</b>
    /// <c>DSP4003</c> reports a command handler that stages a repository write and never calls
    /// <c>SaveChangesAsync</c> — the rule that catches a handler which returns success and changes
    /// nothing. An earlier version of this class held the unit of work and saved on the handler's
    /// behalf, which made every one of B18's write handlers look to the analyzer exactly like the
    /// defect the rule exists to find. Keeping the save in the handler keeps the rule able to see it,
    /// and this class becomes what it should always have been: a policy, with no persistence
    /// dependency of its own.
    /// </para>
    /// </param>
    /// <param name="lookup">Finds the aggregate by key. Called only after a violation.</param>
    /// <returns>
    /// <see cref="IdempotentCommit{T}.Committed"/> when the write landed — the caller renders what it
    /// staged. <see cref="IdempotentCommit{T}.Replay"/> when the index refused a duplicate and the
    /// first request's record was found — the caller renders that instead. A failure when the first
    /// request is still in flight.
    /// </returns>
    public static async Task<Result<IdempotentCommit<TAggregate>>> GuardAsync<TAggregate>(
        string? key,
        Func<CancellationToken, Task> save,
        Func<CancellationToken, Task<TAggregate?>> lookup,
        CancellationToken cancellationToken)
        where TAggregate : class
    {
        ArgumentNullException.ThrowIfNull(save);
        ArgumentNullException.ThrowIfNull(lookup);

        try
        {
            await save(cancellationToken);

            return Result.Success(IdempotentCommit<TAggregate>.Committed());
        }
        catch (DbUpdateException exception) when (key is not null && IsUniqueViolation(exception))
        {
            // The index refused a duplicate. Either the first request has landed — in which case its
            // record is the answer — or it has not, in which case saying so is better than a 500.
            var existing = await lookup(cancellationToken);

            return existing is null
                ? Result.Failure<IdempotentCommit<TAggregate>>(
                    CommunityProgrammeErrors.IdempotencyKeyInFlight)
                : Result.Success(IdempotentCommit<TAggregate>.Replay(existing));
        }
    }

    /// <summary>
    /// Whether a failed save was a unique-index violation rather than something else.
    /// <para>
    /// <b>Matched on the message rather than on a provider error code, and the reason is honest
    /// rather than lazy.</b> The platform runs on Npgsql in production and SQLite in the test suite,
    /// and the two report this differently — <c>23505</c> against <c>SQLITE_CONSTRAINT_UNIQUE</c>.
    /// Referencing either provider's exception type from a module would put a database driver in the
    /// application layer, which is the dependency the persistence abstraction exists to prevent.
    /// The substrings below are the ones both providers put in the inner exception's message, and the
    /// consequence of a miss is a 500 where a 409 was meant — the same answer as not catching at all,
    /// rather than a wrong success.
    /// </para>
    /// </summary>
    internal static bool IsUniqueViolation(Exception exception)
    {
        if (exception is not DbUpdateException)
        {
            return false;
        }

        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            var message = current.Message;

            if (message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase)
                || message.Contains("duplicate key value", StringComparison.OrdinalIgnoreCase)
                || message.Contains("23505", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>
/// What a keyed commit did.
/// <para>
/// Two outcomes and no third, so a call site cannot forget one: either the write landed, or the
/// index refused it and the first request's record is here to render instead.
/// </para>
/// </summary>
public sealed class IdempotentCommit<T> where T : class
{
    private IdempotentCommit(T? replayed) => Replayed = replayed;

    /// <summary>The first request's record, when this was a duplicate. Null when the write landed.</summary>
    public T? Replayed { get; }

    public bool IsReplay => Replayed is not null;

    public static IdempotentCommit<T> Committed() => new(null);

    public static IdempotentCommit<T> Replay(T existing) => new(existing);
}
