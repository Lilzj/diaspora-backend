using Diaspora.Domain.Abstractions;
using Diaspora.Persistence.Model;
using Diaspora.Persistence.Modules;
using Diaspora.Persistence.Transactions;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Persistence;

/// <summary>
/// The platform's unit of work: one model composed from every module, one save path, one
/// transaction.
/// <para>
/// <b>What it owns is the model and the routing.</b> It composes the model from the deployment's
/// <see cref="IDatabaseContributor"/> set, checks each module stayed inside its own schema, and
/// decides which of three save paths a <c>SaveChangesAsync</c> takes. Everything each path then
/// does belongs to a collaborator:
/// </para>
/// <list type="bullet">
/// <item>
/// <see cref="SaveAndDispatchCoordinator"/> — audit stamping, soft deletion, domain-event
/// collection and the save-and-dispatch loop that makes an aggregate and its outbox row commit
/// together.
/// </item>
/// <item>
/// <see cref="TransactionalExecutor"/> — the execution strategy, the commit receipt, the
/// verification delegate and the telemetry.
/// </item>
/// <item>
/// <see cref="TransactionReplayGuard"/> — the one piece of state the two share, and the third
/// transient-failure window.
/// </item>
/// </list>
/// <para>
/// <b>Split on 21 November 2026 for BSOLID-002</b>, which found eight responsibilities in 833
/// lines. The two moved blocks were 76% of the file between them, and the larger of them —
/// transactional execution — was the one the finding's own remediation list never named.
/// </para>
/// <para>
/// <b>One context for 21 modules is a decision, not an accident (BARCH-004, ADR-022).</b> It buys
/// the cross-module transaction that makes a CommunityProgramme → Financial settlement atomic, and
/// it costs the structural boundary: holding this type, a module could read any schema. Two rules
/// replace that structure. <see cref="ApplyModuleConfigurations"/> refuses at startup a module that
/// <em>maps</em> into another module's schema, and <c>DatabaseBoundaryArchitectureTests</c> refuses
/// a module that <em>queries</em> one — every <c>Set&lt;T&gt;</c> outside this project must name an
/// entity the calling project declares. The second matters most for the seven identity aggregates
/// in <c>Diaspora.Domain</c>: identity is not a module, so every module may import them and no
/// module-boundary rule would see the reach.
/// </para>
/// <para>
/// <b>EF caches one model per <c>DbContext</c> CLR type.</b> A test host building this context from
/// its own contributor set shares that cache with every other host, and whichever runs first wins —
/// which only fails when the whole suite runs. Any such host replaces
/// <c>IModelCacheKeyFactory</c> with a key of its own.
/// </para>
/// </summary>
public class DiasporaDbContext : DbContext, IUnitOfWork
{
    private readonly IReadOnlyList<IDatabaseContributor> _databaseContributors = [];
    private readonly SaveAndDispatchCoordinator _saves;
    private readonly TransactionalExecutor _transactions;

    public DiasporaDbContext(DbContextOptions<DiasporaDbContext> options)
        : this(options, null, null, null)
    {
    }

    public DiasporaDbContext(
        DbContextOptions<DiasporaDbContext> options,
        ICurrentUser? currentUser,
        IClock? clock,
        IDomainEventDispatcher? eventDispatcher,
        IEnumerable<IDatabaseContributor>? databaseContributors = null) : base(options)
    {
        _databaseContributors = databaseContributors?.ToArray() ?? [];

        // Both collaborators take this context, and the coordinator takes one bound method of it.
        // Safe from here because neither calls anything during construction — they store the
        // reference and wait to be asked.
        var replayGuard = new TransactionReplayGuard();

        _transactions = new TransactionalExecutor(this, clock, replayGuard);
        _saves = new SaveAndDispatchCoordinator(
            this, SaveCoreAsync, eventDispatcher, currentUser, clock, replayGuard);
    }

    /// <summary>The schema the identity tables live in.</summary>
    public const string IdentitySchema = "identity";

    /// <summary>
    /// The sequence that issues the placeholder phone number an erased account keeps. See
    /// <see cref="OnModelCreating"/> for why a sequence and not a hash.
    /// </summary>
    public const string AnonymizedPhoneSequence = "anonymized_phone_seq";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DiasporaDbContext).Assembly);

        // The placeholder numbers an erased account is left holding.
        //
        // `ix_users_phone_number` is unconditionally unique and does not exclude soft-deleted rows, so
        // two members who both ask to be erased cannot be given the same tombstone. A hash of the user
        // id would not do: E.164 permits fifteen digits, and no injective mapping of a 128-bit
        // identifier fits in them — a birthday collision at a million erased accounts is about even.
        //
        // A sequence is provably unique with no retry loop and no reasoning about probability, which is
        // the right shape for a constraint that would otherwise throw in the middle of an irreversible
        // operation. Declared here rather than only in the migration so the model snapshot carries it
        // and `has-pending-model-changes` stays quiet.
        // PostgreSQL only, and that guard is not defensive coding — it is a hard requirement.
        // `EnsureCreated()` builds its schema from the model rather than from the migrations, and the
        // unit and integration suites build theirs on SQLite, which throws
        // `NotSupportedException: SQLite does not support sequences` on any model that declares one.
        // Declared unconditionally, this single line failed 116 tests that have nothing to do with
        // erasure.
        if (Database.IsNpgsql())
        {
            modelBuilder.HasSequence<long>(AnonymizedPhoneSequence, IdentitySchema).StartsAt(1).IncrementsBy(1);
        }

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

        ModelConventions.Apply(configurationBuilder, Database.ProviderName);
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
    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        // A handler called back into us from inside the dispatch loop. Persist its work and
        // hand its events to the loop that is already running — opening a second transaction
        // here would nest, and running a second dispatch loop would reorder events.
        if (_saves.IsDispatching)
        {
            return _saves.SaveNestedAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        if (!_saves.DispatchesEvents)
        {
            return _saves.PersistWithoutDispatchAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        // Joins the caller's transaction when one is open — which is the normal case for a
        // [Transactional] command — and opens one otherwise. Opening it here rather than
        // relying on the pipeline is deliberate: only a fraction of commands are marked
        // transactional, and the outbox guarantee cannot be conditional on an attribute
        // somebody remembered to apply.
        return ExecuteTransactionalAsync(
            token => _saves.SaveAndDispatchAsync(acceptAllChangesOnSuccess, token),
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
    /// The write EF itself performs, and the only capability the save coordinator is given.
    /// <para>
    /// <c>base.SaveChangesAsync</c> is reachable only from inside this class, so the coordinator
    /// cannot have the context and call <c>SaveChangesAsync</c> on it — that would route straight
    /// back through the override and recurse. Handing it this one bound method instead means the
    /// thing it is allowed to do is exactly "write what the tracker is holding", and the routing
    /// above stays the only place that decides anything.
    /// </para>
    /// </summary>
    private Task<int> SaveCoreAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken) =>
        base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);

    public bool HasActiveTransaction => Database.CurrentTransaction is not null;

    /// <summary>
    /// Runs an operation inside a transaction, through the provider's execution strategy, and
    /// survives a transient failure in any of the three windows it can arrive in.
    /// <para>
    /// The whole of that is <see cref="TransactionalExecutor"/>'s. This is the member
    /// <see cref="IUnitOfWork"/> declares, and a delegation is all it is.
    /// </para>
    /// </summary>
    public Task<TResult> ExecuteTransactionalAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default) =>
        _transactions.ExecuteAsync(operation, cancellationToken);

    private static System.Linq.Expressions.LambdaExpression BuildSoftDeleteFilter(Type entityType)
    {
        var parameter = System.Linq.Expressions.Expression.Parameter(entityType, "e");
        var property = System.Linq.Expressions.Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted));
        var condition = System.Linq.Expressions.Expression.Equal(
            property,
            System.Linq.Expressions.Expression.Constant(false));
        return System.Linq.Expressions.Expression.Lambda(condition, parameter);
    }
}
