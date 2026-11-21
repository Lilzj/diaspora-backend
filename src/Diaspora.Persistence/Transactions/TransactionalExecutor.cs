using System.Diagnostics;
using Diaspora.Domain.Abstractions;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.Platform.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace Diaspora.Persistence.Transactions;

/// <summary>
/// Runs an operation inside a transaction, through the provider's execution strategy, and survives
/// the three ways that can fail.
/// <para>
/// <b>The platform's only transactional execution.</b> Everything a module sees is
/// <see cref="IUnitOfWork.ExecuteTransactionalAsync{TResult}"/>; this is what is behind it. The
/// execution strategy, the receipt, the verification delegate, the replay guard and the telemetry
/// are all here and nowhere else, which is what
/// <c>TransactionReliabilityArchitectureTests</c> exists to keep true.
/// </para>
/// <para>
/// <b>It takes a <see cref="DbContext"/> rather than <see cref="DiasporaDbContext"/>.</b> Nothing
/// here knows anything about this platform's model beyond <see cref="TransactionReceipt"/>, and
/// taking the base type is what makes that a fact rather than a claim — there is no Diaspora
/// aggregate it could reach for.
/// </para>
/// </summary>
internal sealed class TransactionalExecutor
{
    private readonly DbContext _context;
    private readonly IClock? _clock;
    private readonly TransactionReplayGuard _replayGuard;

    /// <summary>
    /// The receipt insert, built once per context from the model's own metadata. Constant for the
    /// lifetime of the model; rebuilt per context because a context is cheap and a static cache
    /// keyed on the model is not worth the lifetime question it raises.
    /// </summary>
    private string? _receiptInsertSql;

    private ILogger? _logger;
    private bool _loggerResolved;

    internal TransactionalExecutor(DbContext context, IClock? clock, TransactionReplayGuard replayGuard)
    {
        _context = context;
        _clock = clock;
        _replayGuard = replayGuard;
    }

    /// <summary>
    /// A logger, if the host configured one.
    /// <para>
    /// Resolved from EF's own service provider rather than taken as a constructor parameter,
    /// because the context's constructor is a contract the test hosts and EF's factory both bind
    /// to, and widening it to carry a logger would be a breaking change made for two log lines.
    /// Null is a perfectly good answer — the counters carry the signal, and these two messages are
    /// context for a human rather than the record itself.
    /// </para>
    /// </summary>
    private ILogger? Logger
    {
        get
        {
            if (_loggerResolved)
            {
                return _logger;
            }

            _loggerResolved = true;

            try
            {
                _logger = _context.GetService<ILoggerFactory>()
                    .CreateLogger("Diaspora.Persistence.Transactions");
            }
            catch (InvalidOperationException)
            {
                // No logger factory in this context's services. Not a reason to fail a write.
                _logger = null;
            }

            return _logger;
        }
    }

    private bool HasActiveTransaction => _context.Database.CurrentTransaction is not null;

    /// <summary>
    /// Runs an operation inside a transaction, through the provider's execution strategy.
    /// <para>
    /// The execution strategy is not optional decoration. The Npgsql provider is configured
    /// with <c>EnableRetryOnFailure</c>, and EF refuses to start a user-initiated
    /// transaction under a retrying strategy unless the whole unit of work is wrapped in
    /// one — because a retry has to replay the transaction from the beginning, and it
    /// cannot do that if the caller owns the <c>using</c> block. Calling
    /// <c>BeginTransactionAsync</c> directly throws at runtime with exactly that message.
    /// </para>
    /// <para>
    /// <b>B1.0.2 made the retry itself safe.</b> Until then this passed
    /// <c>verifySucceeded: null</c>, which told the strategy "if a transient failure happens,
    /// replay — I have no way to tell you whether it already worked". That is wrong in one
    /// specific and unrecoverable way: a transient failure raised <em>at commit time</em> may
    /// arrive after the commit has already reached the server, and the replay then writes
    /// everything a second time. Two of the three paths below exist to make that impossible.
    /// </para>
    /// </summary>
    internal async Task<TResult> ExecuteAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        // Already inside someone else's transaction: join it. The outer scope commits, and the
        // outer scope's execution strategy owns any retry.
        if (HasActiveTransaction)
        {
            return await operation(cancellationToken);
        }

        var strategy = _context.Database.CreateExecutionStrategy();

        // Keyed on the capability, never on a provider name. SQLite, PostgreSQL without
        // EnableRetryOnFailure, SQL Server without EnableRetryOnFailure and any provider that
        // does not exist yet all answer this correctly without the platform knowing about them.
        //
        // A strategy that does not retry never calls verifySucceeded and never replays, so the
        // receipt would be a write per transaction bought for nothing.
        return strategy.RetriesOnFailure
            ? await ExecuteVerifiedAsync(strategy, operation, cancellationToken)
            : await ExecuteOnceAsync(strategy, operation, cancellationToken);
    }

    /// <summary>
    /// The single-attempt path, for providers whose strategy does not retry.
    /// <para>
    /// Still routed through the strategy rather than around it, so both paths open their
    /// transaction the same way and EF's suspension bookkeeping sees the same shape.
    /// </para>
    /// </summary>
    private async Task<TResult> ExecuteOnceAsync<TResult>(
        IExecutionStrategy strategy,
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        TransactionTelemetry.Started.Add(1, RetryingTag(false));

        try
        {
            var result = await strategy.ExecuteAsync(
                operation,
                async (_, work, token) =>
                {
                    // Disposal rolls back anything not committed. Rolling back explicitly in a
                    // catch block — which this used to do — throws its own exception when the
                    // failure happened after the commit completed, and that second exception
                    // replaces the first one on the way out.
                    await using var transaction = await _context.Database.BeginTransactionAsync(token);

                    var value = await work(token);

                    await transaction.CommitAsync(token);

                    return value;
                },
                verifySucceeded: null,
                cancellationToken);

            TransactionTelemetry.Committed.Add(1, RetryingTag(false));

            return result;
        }
        catch (Exception exception)
        {
            RecordRollback(exception, retrying: false);
            throw;
        }
        finally
        {
            TransactionTelemetry.Duration.Record(stopwatch.Elapsed.TotalMilliseconds, RetryingTag(false));
        }
    }

    /// <summary>
    /// The retrying path: a receipt written inside the transaction, a verification delegate that
    /// reads it back, and a guard that refuses a replay which would run against an accepted change
    /// tracker.
    /// <para>
    /// <b>The three failure windows, and what each one does.</b>
    /// </para>
    /// <list type="number">
    /// <item>
    /// <b>Before anything is written</b> — the connection drops acquiring itself, or the first
    /// <c>SaveChanges</c> fails. The change tracker still holds every pending change, so the replay
    /// is exactly the original operation. Retried transparently; nobody notices.
    /// </item>
    /// <item>
    /// <b>At the commit</b> — the transaction may or may not have reached the server. The receipt
    /// answers it: present means the commit landed, so the strategy returns the result it captured
    /// and no replay happens. This is the window <c>verifySucceeded: null</c> could not survive,
    /// and the one that wrote everything twice.
    /// </item>
    /// <item>
    /// <b>After a save was accepted but before the commit landed</b> — the transaction rolled back,
    /// but the change tracker is now empty, so a replay would write nothing, commit nothing, and
    /// report success. The guard raises <see cref="TransientTransactionFailureException"/> instead.
    /// The request fails, loudly, having changed nothing.
    /// </item>
    /// </list>
    /// </summary>
    private async Task<TResult> ExecuteVerifiedAsync<TResult>(
        IExecutionStrategy strategy,
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        var state = new TransactionExecutionState<TResult>(operation);
        var stopwatch = Stopwatch.StartNew();

        using var activity = TransactionTelemetry.Activity.StartActivity(
            "platform transaction", ActivityKind.Internal);

        TransactionTelemetry.Started.Add(1, RetryingTag(true));

        // Reset once for the whole execution, never per attempt: the guard's entire job is to
        // notice what a *previous* attempt did.
        _replayGuard.Reset();

        try
        {
            // The DbContext the strategy hands each delegate is this executor's own context — it
            // comes from the same ICurrentDbContext the strategy was created from — so it is
            // discarded here rather than cast back. The cast was only ever how a method on the
            // context reached itself.
            var result = await strategy.ExecuteAsync(
                state,
                (_, executionState, token) => RunTransactionAttemptAsync(executionState, token),
                (_, executionState, token) => VerifyCommittedAsync(executionState, token),
                cancellationToken);

            activity?.SetTag("transaction.attempts", state.Attempts);
            TransactionTelemetry.Committed.Add(1, RetryingTag(true));

            if (state.Attempts > 1)
            {
                TransactionTelemetry.Replays.Add(state.Attempts - 1, RetryingTag(true));
            }

            return result;
        }
        catch (Exception exception)
        {
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            RecordRollback(exception, retrying: true);
            throw;
        }
        finally
        {
            _replayGuard.Reset();
            TransactionTelemetry.Duration.Record(stopwatch.Elapsed.TotalMilliseconds, RetryingTag(true));
        }
    }

    /// <summary>One attempt: guard, begin, run, receipt, commit.</summary>
    private async Task<TResult> RunTransactionAttemptAsync<TResult>(
        TransactionExecutionState<TResult> state, CancellationToken cancellationToken)
    {
        state.Attempts++;

        // The guard. Reached only on a second or later attempt, and only when an earlier attempt
        // got as far as an accepted save. Replaying from here would run the operation against a
        // change tracker holding nothing, write nothing, and commit an empty transaction — data
        // loss reported as success, which is worse than the failure that caused it.
        if (_replayGuard.BlocksReplay)
        {
            TransactionTelemetry.RefusedReplays.Add(1);

            var refusal = new TransientTransactionFailureException(
                $"A transient failure rolled back the transaction after {state.Attempts - 1} " +
                "attempt(s), and it cannot be replayed: an earlier attempt had already persisted " +
                "part of the work, so the change tracker no longer holds it. Nothing was " +
                "committed. Retry the request.",
                state.LastFailure ?? new InvalidOperationException("The original failure was not captured."))
            {
                Attempts = state.Attempts - 1
            };

            // Error rather than warning. Each one is a request that failed for an infrastructure
            // reason a user cannot act on, and a rising count means database connectivity is
            // degrading in a way that is otherwise invisible because every other request succeeds.
            Logger?.LogError(
                refusal,
                "Refused to replay a platform transaction: an earlier attempt had already " +
                "persisted work. The transaction rolled back and nothing was committed.");

            throw refusal;
        }

        state.CommitAttempted = false;

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var result = await state.Operation(cancellationToken);

            // Inside the transaction, immediately before the commit, so its presence afterwards
            // means exactly one thing: this transaction committed.
            await WriteTransactionReceiptAsync(state.ReceiptId, cancellationToken);

            // Captured before the commit is attempted, because if the commit turns out to have
            // landed we have to return this rather than replay to produce it again.
            state.Result = result;
            state.CommitAttempted = true;

            await transaction.CommitAsync(cancellationToken);

            return result;
        }
        catch (Exception exception)
        {
            state.LastFailure = exception;

            // No explicit rollback: disposal handles an uncommitted transaction, and calling
            // RollbackAsync on one whose commit already reached the server throws a second
            // exception that hides the first.
            throw;
        }
    }

    /// <summary>
    /// Asks the database whether the transaction committed, by looking for its receipt.
    /// <para>
    /// Runs on a connection the strategy has re-established, which is the whole point: the previous
    /// one is why we are here. A missing receipt is a definite "no" — the row is written inside the
    /// transaction, so it cannot survive a rollback.
    /// </para>
    /// </summary>
    private async Task<ExecutionResult<TResult>> VerifyCommittedAsync<TResult>(
        TransactionExecutionState<TResult> state, CancellationToken cancellationToken)
    {
        // The failure happened before the commit was reached, so the receipt was never written and
        // the answer is already known. Skipping the query keeps the common transient failure — a
        // connection lost mid-operation — at one round trip instead of two.
        if (!state.CommitAttempted)
        {
            return new ExecutionResult<TResult>(successful: false, default!);
        }

        TransactionTelemetry.VerificationsRun.Add(1);

        var committed = await _context.Set<TransactionReceipt>()
            .AsNoTracking()
            .AnyAsync(receipt => receipt.Id == state.ReceiptId, cancellationToken);

        if (!committed)
        {
            return new ExecutionResult<TResult>(successful: false, default!);
        }

        TransactionTelemetry.VerifiedCommits.Add(1);

        // Warning, and worth every one of them: this is a transaction that committed while telling
        // the application it had failed. Before B1.0.2 the strategy would have replayed it and
        // written everything twice.
        Logger?.LogWarning(
            "A platform transaction's commit acknowledgement was lost after {Attempts} attempt(s). " +
            "The receipt confirms the commit landed, so it was not replayed.",
            state.Attempts);

        return new ExecutionResult<TResult>(successful: true, state.Result!);
    }

    /// <summary>
    /// Writes the receipt with a raw command rather than through the change tracker.
    /// <para>
    /// <b>Through the tracker this would change behaviour.</b> Adding an entity and calling
    /// <c>SaveChanges</c> flushes <em>everything</em> the tracker is holding — and a caller that
    /// mutated an entity after its last save currently has that work discarded when the scope ends.
    /// Committing it as a side effect of a reliability patch would be a silent behavioural change
    /// in exactly the layer that must not have one.
    /// </para>
    /// <para>
    /// The identifiers come from the model's own metadata and are delimited by the provider's own
    /// SQL generation helper, so the statement is correct on PostgreSQL, SQLite and SQL Server
    /// without this class naming any of them.
    /// </para>
    /// <para>
    /// <b>Not routed through <c>ITransactionReceiptStore</c>, and that port says why.</b> Its own
    /// documentation records the insert and the lookup as living inside the unit of work because
    /// they are part of a transaction rather than services anything else may call; the port exists
    /// for the retention sweep, which runs from a hosted service that cannot reference this
    /// assembly. BSOLID-002 recommended moving the insert behind it. That would put a write that
    /// must land on this connection, inside this transaction, behind a seam whose implementations
    /// are free to do neither.
    /// </para>
    /// </summary>
    private Task<int> WriteTransactionReceiptAsync(string receiptId, CancellationToken cancellationToken)
    {
        _receiptInsertSql ??= BuildReceiptInsertSql();

        // Not user input: every identifier is model metadata run through the provider's delimiter,
        // and both values are bound as parameters.
#pragma warning disable CA2100
        return _context.Database.ExecuteSqlRawAsync(
            _receiptInsertSql,
            [receiptId, (_clock?.UtcNow ?? DateTimeOffset.UtcNow).UtcTicks],
            cancellationToken);
#pragma warning restore CA2100
    }

    private string BuildReceiptInsertSql()
    {
        var sql = _context.GetService<ISqlGenerationHelper>();
        var entityType = _context.Model.FindEntityType(typeof(TransactionReceipt))
            ?? throw new InvalidOperationException(
                "The transaction receipt is not mapped. Commit verification cannot work without " +
                "it, and a retrying execution strategy is configured.");

        var tableName = entityType.GetTableName()!;
        var schema = entityType.GetSchema();
        var table = StoreObjectIdentifier.Table(tableName, schema);

        var id = entityType.FindProperty(nameof(TransactionReceipt.Id))!.GetColumnName(table)!;
        var ticks = entityType.FindProperty(nameof(TransactionReceipt.CreatedAtTicks))!.GetColumnName(table)!;

        return $"INSERT INTO {sql.DelimitIdentifier(tableName, schema)} " +
               $"({sql.DelimitIdentifier(id)}, {sql.DelimitIdentifier(ticks)}) VALUES ({{0}}, {{1}})";
    }

    private static void RecordRollback(Exception exception, bool retrying)
    {
        TransactionTelemetry.RolledBack.Add(
            1,
            new KeyValuePair<string, object?>("retrying", retrying),
            new KeyValuePair<string, object?>("exception.type", exception.GetType().Name));
    }

    private static KeyValuePair<string, object?> RetryingTag(bool retrying) =>
        new("retrying", retrying);
}
