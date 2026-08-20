using Diaspora.Modules.Creator.Domain.Errors;
using Diaspora.SharedKernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Creator.Application.Services;

/// <summary>
/// B15's idempotency primitive, applied to creator writes.
/// <para>
/// <b>The same mechanism, not a second one.</b> §24 of the phase brief is explicit: "Do NOT create
/// <c>CreatorIdempotencyKey</c> or another creator-specific implementation. Use the established
/// primitive." B15 uniquely indexes a nullable key column on the aggregate each write creates and
/// looks it up before writing; this module does exactly that on nine aggregates. There is no
/// idempotency table, no key store, no TTL sweeper and no second uniqueness rule.
/// </para>
/// <para>
/// <b>What this class adds is the concurrent case</b>, exactly as B18's <c>ProgrammeIdempotency</c>
/// does and for the same reason: a lookup-then-write is not atomic, two requests carrying one key can
/// both find nothing, and the unique index refuses the second at commit. Catching that turns a 500
/// into either a replay or a retryable 409.
/// </para>
/// <para>
/// <b>It deliberately does not retry the write.</b> A retry inside the handler would be a second
/// attempt at a body the first attempt may have half-applied, against a change tracker that still
/// holds it. Answering 409 and letting the client retry is a whole new request against a clean
/// context, which is the only version of this that is safe.
/// </para>
/// </summary>
public sealed class CreatorIdempotency
{
    /// <summary>
    /// Normalises a key from the header or the body.
    /// <para>
    /// The header wins where both are present. <b>Scoped by operation</b>, because two different
    /// operations can legitimately be retried with the same client-generated key and a bare key
    /// would make the second replay the first's record.
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
    /// Passed in rather than performed here so <c>DSP4003</c> — "a command handler that stages a
    /// repository write must save it" — can still see the handler saving. B18 learned that the
    /// expensive way: a version of this class that held the unit of work made every write handler
    /// look to the analyzer exactly like the defect the rule exists to find.
    /// </para>
    /// </param>
    /// <param name="lookup">
    /// Finds the already-committed aggregate. Called only after a violation.
    /// <para>
    /// <b>Not necessarily by the idempotency key.</b> A write can collide on a <em>natural</em>
    /// uniqueness rule as well — one enrolment per creator per challenge, one application per
    /// creator per grant — and those collide whether or not the caller sent a key. A lookup that
    /// resolves the natural identity turns a keyless double-tap into the first request's answer;
    /// one that can only resolve a key returns null and the caller gets a retryable 409, which is
    /// still better than the 500 an uncaught violation produces.
    /// </para>
    /// </param>
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

        // **Every unique violation, not only a keyed one.** An earlier version filtered on
        // `key is not null`, which let a keyless concurrent join escape as a 500 — the natural
        // (challenge, creator) index refused the second insert and nothing caught it. A concurrency
        // test found it; a suite that only raced keyed writes would not have.
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            // The index refused a duplicate. Either the first request has landed — in which case its
            // record is the answer — or it has not, in which case saying so beats a 500.
            var existing = await lookup(cancellationToken);

            return existing is null
                ? Result.Failure<IdempotentCommit<TAggregate>>(CreatorErrors.IdempotencyKeyInFlight)
                : Result.Success(IdempotentCommit<TAggregate>.Replay(existing));
        }
    }

    /// <summary>
    /// Whether a failed save was a unique-index violation rather than something else.
    /// <para>
    /// Matched on the message rather than a provider error code, for the reason B18 records: the
    /// platform runs on Npgsql in production and SQLite in the test suite, the two report this
    /// differently, and referencing either provider's exception type from a module would put a
    /// database driver in the application layer. A miss costs a 500 where a 409 was meant — the same
    /// answer as not catching at all, rather than a wrong success.
    /// </para>
    /// </summary>
    private static bool IsUniqueViolation(DbUpdateException exception)
    {
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
