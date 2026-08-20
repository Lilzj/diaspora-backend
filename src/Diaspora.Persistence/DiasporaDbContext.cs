using System.Diagnostics;
using Diaspora.Domain.Abstractions;
using Diaspora.Persistence.ChangeTracking;
using Diaspora.Persistence.Modules;
using Diaspora.Persistence.Transactions;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.Platform.Transactions;
using Diaspora.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Logging;

namespace Diaspora.Persistence;

public class DiasporaDbContext : DbContext, IUnitOfWork
{
    /// <summary>
    /// How many save-and-dispatch rounds one <c>SaveChangesAsync</c> may take before it is
    /// treated as a cycle.
    /// <para>
    /// Two is the normal case: the aggregate, then the outbox rows its relays produced. Three
    /// or four happens where a handler updates a projection that raises its own event. Beyond
    /// that it is a loop, and a loop here holds a database transaction open until the request
    /// dies.
    /// </para>
    /// </summary>
    private const int MaxCascadeRounds = 10;

    private readonly ICurrentUser? _currentUser;
    private readonly IClock? _clock;
    private readonly IDomainEventDispatcher? _eventDispatcher;
    private readonly IReadOnlyList<IDatabaseContributor> _databaseContributors = [];

    /// <summary>
    /// Events raised by a nested <c>SaveChangesAsync</c> — a handler that saves its own work
    /// mid-dispatch — waiting to be folded into the running loop.
    /// </summary>
    private readonly List<IDomainEvent> _deferredEvents = [];

    /// <summary>
    /// Whether a dispatch loop is running on this context. Guards against a handler's own save
    /// starting a second loop, which would dispatch its events out of order and, worse, open a
    /// transaction inside the one already committing.
    /// </summary>
    private bool _isDispatching;

    /// <summary>
    /// Whether any save inside the current platform transaction has been accepted.
    /// <para>
    /// The one piece of state the replay guard needs. Once a <c>SaveChanges</c> inside the
    /// transaction has accepted its changes, the tracked entities are <c>Unchanged</c> and the
    /// operation is no longer replayable — a second attempt would write nothing. Set here rather
    /// than inferred, because "did anything get written" cannot be recovered from a change tracker
    /// after the fact.
    /// </para>
    /// </summary>
    private bool _acceptedWriteInTransaction;

    /// <summary>
    /// The receipt insert, built once per context from the model's own metadata. Constant for the
    /// lifetime of the model; rebuilt per context because a context is cheap and a static cache
    /// keyed on the model is not worth the lifetime question it raises.
    /// </summary>
    private string? _receiptInsertSql;

    private ILogger? _logger;
    private bool _loggerResolved;

    /// <summary>
    /// A logger, if the host configured one.
    /// <para>
    /// Resolved from EF's own service provider rather than taken as a constructor parameter,
    /// because the constructor is a contract the test hosts and EF's factory both bind to, and
    /// widening it to carry a logger would be a breaking change made for two log lines. Null is a
    /// perfectly good answer — the counters carry the signal, and these two messages are context
    /// for a human rather than the record itself.
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
                _logger = this.GetService<ILoggerFactory>()
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

    public DiasporaDbContext(DbContextOptions<DiasporaDbContext> options) : base(options) { }

    public DiasporaDbContext(
        DbContextOptions<DiasporaDbContext> options,
        ICurrentUser? currentUser,
        IClock? clock,
        IDomainEventDispatcher? eventDispatcher,
        IEnumerable<IDatabaseContributor>? databaseContributors = null) : base(options)
    {
        _currentUser = currentUser;
        _clock = clock;
        _eventDispatcher = eventDispatcher;
        _databaseContributors = databaseContributors?.ToArray() ?? [];
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DiasporaDbContext).Assembly);

        ApplyModuleConfigurations(modelBuilder);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
            {
                modelBuilder.Entity(entityType.ClrType)
                    .HasQueryFilter(
                        BuildSoftDeleteFilter(entityType.ClrType));
            }
        }
    }

    /// <summary>
    /// Lets each module add its own entities, then checks it stayed in its own schema.
    /// <para>
    /// The check is the point. One shared <c>DbContext</c> means nothing stops the Jobs
    /// module from mapping a table into the <c>identity</c> schema, and nothing would
    /// notice until an extraction attempt years later found the two tangled together. A
    /// startup failure now costs a minute; the alternative costs a migration project.
    /// </para>
    /// </summary>
    private void ApplyModuleConfigurations(ModelBuilder modelBuilder)
    {
        foreach (var contributor in _databaseContributors)
        {
            var before = modelBuilder.Model.GetEntityTypes().Select(e => e.Name).ToHashSet(StringComparer.Ordinal);

            contributor.Configure(modelBuilder);

            var trespassing = modelBuilder.Model.GetEntityTypes()
                .Where(entity => !before.Contains(entity.Name))
                .Where(entity => entity.GetSchema() is { } schema &&
                                 !string.Equals(schema, contributor.Schema, StringComparison.Ordinal))
                .Select(entity => $"{entity.ShortName()} → {entity.GetSchema()}")
                .ToList();

            if (trespassing.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Module '{contributor.Module}' declares schema '{contributor.Schema}' but mapped " +
                    $"entities into another module's schema: {string.Join(", ", trespassing)}.");
            }
        }
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        RegisterStronglyTypedIdConverters(configurationBuilder);
        RegisterSqliteDateTimeOffsetConverters(configurationBuilder);
    }

    /// <summary>
    /// Stores <see cref="DateTimeOffset"/> in an order-preserving binary form when running
    /// on SQLite.
    /// <para>
    /// SQLite has no native offset-aware date type, so EF cannot translate any comparison or
    /// ordering on a <c>DateTimeOffset</c> column — which is most of the identity module's
    /// queries: token expiry, session ceilings, "codes issued since". Without this, the
    /// integration suite could not run on SQLite at all.
    /// </para>
    /// <para>
    /// Guarded on the provider name rather than a call to <c>Database.IsSqlite()</c>, which
    /// would drag the SQLite provider package into the production persistence assembly.
    /// PostgreSQL keeps its native <c>timestamptz</c> mapping and is unaffected.
    /// </para>
    /// </summary>
    private void RegisterSqliteDateTimeOffsetConverters(ModelConfigurationBuilder configurationBuilder)
    {
        if (Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) != true)
        {
            return;
        }

        configurationBuilder.Properties<DateTimeOffset>()
            .HaveConversion<DateTimeOffsetToBinaryConverter>();

        configurationBuilder.Properties<DateTimeOffset?>()
            .HaveConversion<DateTimeOffsetToBinaryConverter>();
    }

    /// <summary>
    /// The single save path. Every other overload routes here.
    /// <para>
    /// B2 overrode only <c>SaveChangesAsync(CancellationToken)</c>, which meant the
    /// synchronous <c>SaveChanges()</c> and the two-argument async overload skipped audit
    /// stamping, soft-delete interception and domain-event dispatch entirely. Nothing
    /// called them, so nothing broke — right up until the first module that did.
    /// </para>
    /// <para>
    /// <b>B1.1 made this path atomic, and that is the whole of the transactional outbox.</b>
    /// Before it, the sequence was: save the aggregate, commit, then dispatch domain events —
    /// whose handlers publish the integration events other modules consume. An exception
    /// anywhere after the commit lost those events permanently, and the aggregate was already
    /// on disk. Now the save, the dispatch and the flush of whatever the handlers wrote all
    /// happen inside one transaction, so the aggregate and the durable promise to publish it
    /// commit together or not at all.
    /// </para>
    /// </summary>
    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        // A handler called back into us from inside the dispatch loop. Persist its work and
        // hand its events to the loop that is already running — opening a second transaction
        // here would nest, and running a second dispatch loop would reorder events.
        if (_isDispatching)
        {
            var (nested, nestedEvents) = await PersistAsync(acceptAllChangesOnSuccess, cancellationToken);
            _deferredEvents.AddRange(nestedEvents);
            return nested;
        }

        if (_eventDispatcher is null)
        {
            var (written, _) = await PersistAsync(acceptAllChangesOnSuccess, cancellationToken);
            return written;
        }

        // Joins the caller's transaction when one is open — which is the normal case for a
        // [Transactional] command — and opens one otherwise. Opening it here rather than
        // relying on the pipeline is deliberate: only a fraction of commands are marked
        // transactional, and the outbox guarantee cannot be conditional on an attribute
        // somebody remembered to apply.
        return await ExecuteTransactionalAsync(
            token => SaveAndDispatchAsync(acceptAllChangesOnSuccess, token),
            cancellationToken);
    }

    /// <summary>
    /// Synchronous save. Routes through the async path so it gets the same atomicity, at the
    /// cost of blocking — which is why nothing on a request path should use this.
    /// </summary>
    public override int SaveChanges(bool acceptAllChangesOnSuccess) =>
        SaveChangesAsync(acceptAllChangesOnSuccess, CancellationToken.None)
            .GetAwaiter()
            .GetResult();

    /// <summary>
    /// Writes, dispatches, and keeps writing until the change tracker is quiet.
    /// <para>
    /// The loop is not decoration. A domain event handler may mutate another aggregate, and a
    /// relay turning a domain event into an integration event adds an outbox row — both after
    /// the first save has already run. Stopping after one round would commit the aggregate and
    /// leave the outbox row in the change tracker, unsaved, which is the original defect with
    /// an extra step.
    /// </para>
    /// </summary>
    private async Task<int> SaveAndDispatchAsync(
        bool acceptAllChangesOnSuccess, CancellationToken cancellationToken)
    {
        var (written, domainEvents) = await PersistAsync(acceptAllChangesOnSuccess, cancellationToken);

        if (domainEvents.Count == 0)
        {
            return written;
        }

        var pending = new Queue<IDomainEvent>(domainEvents);

        _isDispatching = true;

        try
        {
            var round = 0;

            while (pending.Count > 0)
            {
                if (++round > MaxCascadeRounds)
                {
                    // A handler is raising an event that causes the same handler to run again.
                    // Left alone it saves and dispatches until the request times out, holding a
                    // transaction open the whole time; failing names the events involved while
                    // somebody can still act on it.
                    throw new InvalidOperationException(
                        $"Domain event dispatch did not settle after {MaxCascadeRounds} rounds. " +
                        "A handler is raising an event that leads back to itself. Events still " +
                        $"queued: {string.Join(", ", pending.Select(e => e.GetType().Name).Distinct())}.");
                }

                var batch = new List<IDomainEvent>(pending.Count);

                while (pending.Count > 0)
                {
                    batch.Add(pending.Dequeue());
                }

                _deferredEvents.Clear();

                await _eventDispatcher!.DispatchAsync(batch, cancellationToken);

                // Whatever the handlers left behind: outbox rows from the integration event
                // relays, projections written by cross-module handlers, anything else.
                var (extra, cascaded) = await PersistAsync(acceptAllChangesOnSuccess, cancellationToken);

                written += extra;

                foreach (var deferred in _deferredEvents)
                {
                    pending.Enqueue(deferred);
                }

                _deferredEvents.Clear();

                foreach (var cascade in cascaded)
                {
                    pending.Enqueue(cascade);
                }
            }
        }
        finally
        {
            _isDispatching = false;
            _deferredEvents.Clear();
        }

        return written;
    }

    /// <summary>
    /// One audit-stamp, collect and write. Returns the events the tracker was holding, which
    /// the caller owns from that point — they have already been cleared off their entities.
    /// </summary>
    private async Task<(int Written, List<IDomainEvent> Events)> PersistAsync(
        bool acceptAllChangesOnSuccess, CancellationToken cancellationToken)
    {
        ApplyAuditFields();

        var domainEvents = CollectDomainEvents();

        // A dispatch round whose handlers only read leaves nothing to write. Skipping the round
        // trip matters here because the loop runs at least twice for every command that
        // publishes an integration event.
        if (!ChangeTracker.HasChanges())
        {
            return (0, domainEvents);
        }

        try
        {
            var written = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);

            // The replay guard's input. Only meaningful inside a transaction this context opened,
            // and only when the save accepted its changes — an unaccepted save leaves the tracker
            // able to replay itself, which is precisely the distinction the guard turns on.
            if (acceptAllChangesOnSuccess && HasActiveTransaction)
            {
                _acceptedWriteInTransaction = true;
            }

            return (written, domainEvents);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            // Translated at the boundary so EF Core's exception type does not travel into
            // the application layer, which is not allowed to reference EF Core at all.
            throw new ConcurrencyConflictException(
                "The record was modified by another request.", exception);
        }
    }

    public bool HasActiveTransaction => Database.CurrentTransaction is not null;

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
    public async Task<TResult> ExecuteTransactionalAsync<TResult>(
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

        var strategy = Database.CreateExecutionStrategy();

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
                    await using var transaction = await Database.BeginTransactionAsync(token);

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
        _acceptedWriteInTransaction = false;

        try
        {
            var result = await strategy.ExecuteAsync(
                state,
                (context, executionState, token) =>
                    ((DiasporaDbContext)context).RunTransactionAttemptAsync(executionState, token),
                (context, executionState, token) =>
                    ((DiasporaDbContext)context).VerifyCommittedAsync(executionState, token),
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
            _acceptedWriteInTransaction = false;
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
        if (_acceptedWriteInTransaction)
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

        await using var transaction = await Database.BeginTransactionAsync(cancellationToken);

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

        var committed = await Set<TransactionReceipt>()
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
    /// </summary>
    private Task<int> WriteTransactionReceiptAsync(string receiptId, CancellationToken cancellationToken)
    {
        _receiptInsertSql ??= BuildReceiptInsertSql();

        // Not user input: every identifier is model metadata run through the provider's delimiter,
        // and both values are bound as parameters.
#pragma warning disable CA2100
        return Database.ExecuteSqlRawAsync(
            _receiptInsertSql,
            [receiptId, (_clock?.UtcNow ?? DateTimeOffset.UtcNow).UtcTicks],
            cancellationToken);
#pragma warning restore CA2100
    }

    private string BuildReceiptInsertSql()
    {
        var sql = this.GetService<ISqlGenerationHelper>();
        var entityType = Model.FindEntityType(typeof(TransactionReceipt))
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

    private void ApplyAuditFields()
    {
        var now = _clock?.UtcNow ?? DateTimeOffset.UtcNow;
        var userId = _currentUser?.UserId?.ToString();

        foreach (var entry in ChangeTracker.Entries<IAuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.SetCreated(now, userId);
                    break;
                case EntityState.Modified:
                    entry.Entity.SetModified(now, userId);
                    break;
            }
        }

        // Soft deletion lives in its own component because it is not one line of state
        // juggling — it has to repair the owned-entity graph EF cascade-deletes on the way,
        // recursively, or an aggregate with owned value objects writes NULLs into its own
        // not-null columns. See SoftDeleteConverter and B1.0.1.
        SoftDeleteConverter.Apply(ChangeTracker, now, userId);
    }

    private List<IDomainEvent> CollectDomainEvents()
    {
        // Matched through IHasDomainEvents rather than a concrete Entity<TId> closure:
        // aggregates use strongly typed identifiers, so Entity<Guid> matches nothing.
        var tracked = ChangeTracker.Entries<IHasDomainEvents>()
            .Where(e => e.Entity.DomainEvents.Count > 0)
            .ToList();

        var domainEvents = tracked
            .SelectMany(e => e.Entity.DomainEvents)
            .ToList();

        tracked.ForEach(e => e.Entity.ClearDomainEvents());

        return domainEvents;
    }

    private static System.Linq.Expressions.LambdaExpression BuildSoftDeleteFilter(Type entityType)
    {
        var parameter = System.Linq.Expressions.Expression.Parameter(entityType, "e");
        var property = System.Linq.Expressions.Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted));
        var condition = System.Linq.Expressions.Expression.Equal(
            property,
            System.Linq.Expressions.Expression.Constant(false));
        return System.Linq.Expressions.Expression.Lambda(condition, parameter);
    }

    private static void RegisterStronglyTypedIdConverters(ModelConfigurationBuilder configurationBuilder)
    {
        var idTypes = typeof(SharedKernel.Identity.UserId)
            .Assembly
            .GetTypes()
            .Where(t => t.IsAssignableTo(typeof(StronglyTypedId)) && !t.IsAbstract);

        foreach (var idType in idTypes)
        {
            var converterType = typeof(StronglyTypedIdValueConverter<>).MakeGenericType(idType);
            configurationBuilder.Properties(idType).HaveConversion(converterType);
        }
    }
}
