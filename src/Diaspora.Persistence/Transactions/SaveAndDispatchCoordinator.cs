using Diaspora.Domain.Abstractions;
using Diaspora.Persistence.ChangeTracking;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Persistence.Transactions;

/// <summary>
/// Writes, dispatches, and keeps writing until the change tracker is quiet.
/// <para>
/// <b>This is the transactional outbox.</b> B1.1 made the save path atomic: before it, the
/// sequence was save the aggregate, commit, then dispatch domain events — whose handlers publish
/// the integration events other modules consume. An exception anywhere after the commit lost
/// those events permanently, and the aggregate was already on disk. The loop here is what makes
/// the aggregate and the durable promise to publish it commit together or not at all, which is
/// why it lives beside the transactional execution it runs inside rather than in the
/// change-tracking namespace its mechanics come from.
/// </para>
/// <para>
/// <b>It cannot reach EF's own save, and that is the point of <c>saveCore</c>.</b>
/// <c>base.SaveChangesAsync</c> is callable only from inside <see cref="DiasporaDbContext"/>, so
/// the context hands this type exactly one capability — write what the tracker is holding —
/// rather than a context it could call <c>SaveChangesAsync</c> on and recurse through.
/// </para>
/// </summary>
internal sealed class SaveAndDispatchCoordinator
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

    private readonly DbContext _context;
    private readonly Func<bool, CancellationToken, Task<int>> _saveCore;
    private readonly IDomainEventDispatcher? _eventDispatcher;
    private readonly ICurrentUser? _currentUser;
    private readonly IClock? _clock;
    private readonly TransactionReplayGuard _replayGuard;

    /// <summary>
    /// Events raised by a nested <c>SaveChangesAsync</c> — a handler that saves its own work
    /// mid-dispatch — waiting to be folded into the running loop.
    /// </summary>
    private readonly List<IDomainEvent> _deferredEvents = [];

    private bool _isDispatching;

    internal SaveAndDispatchCoordinator(
        DbContext context,
        Func<bool, CancellationToken, Task<int>> saveCore,
        IDomainEventDispatcher? eventDispatcher,
        ICurrentUser? currentUser,
        IClock? clock,
        TransactionReplayGuard replayGuard)
    {
        _context = context;
        _saveCore = saveCore;
        _eventDispatcher = eventDispatcher;
        _currentUser = currentUser;
        _clock = clock;
        _replayGuard = replayGuard;
    }

    /// <summary>
    /// Whether a dispatch loop is running on this context. Guards against a handler's own save
    /// starting a second loop, which would dispatch its events out of order and, worse, open a
    /// transaction inside the one already committing.
    /// </summary>
    internal bool IsDispatching => _isDispatching;

    /// <summary>
    /// Whether this context was composed with a dispatcher at all. False for EF's design-time
    /// factory and the migration tooling, where there is no container and nothing to dispatch to.
    /// </summary>
    internal bool DispatchesEvents => _eventDispatcher is not null;

    /// <summary>
    /// The re-entrant path: a handler called back in from inside the dispatch loop. Persists its
    /// work and hands its events to the loop that is already running.
    /// </summary>
    internal async Task<int> SaveNestedAsync(
        bool acceptAllChangesOnSuccess, CancellationToken cancellationToken)
    {
        var (written, events) = await PersistAsync(acceptAllChangesOnSuccess, cancellationToken);

        _deferredEvents.AddRange(events);

        return written;
    }

    /// <summary>
    /// The path for a context composed without a dispatcher — EF's design-time factory, and the
    /// migration tooling. Writes, and raises nothing.
    /// </summary>
    internal async Task<int> PersistWithoutDispatchAsync(
        bool acceptAllChangesOnSuccess, CancellationToken cancellationToken)
    {
        var (written, _) = await PersistAsync(acceptAllChangesOnSuccess, cancellationToken);

        return written;
    }

    /// <summary>
    /// The full loop.
    /// <para>
    /// The loop is not decoration. A domain event handler may mutate another aggregate, and a
    /// relay turning a domain event into an integration event adds an outbox row — both after
    /// the first save has already run. Stopping after one round would commit the aggregate and
    /// leave the outbox row in the change tracker, unsaved, which is the original defect with
    /// an extra step.
    /// </para>
    /// </summary>
    internal async Task<int> SaveAndDispatchAsync(
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
        if (!_context.ChangeTracker.HasChanges())
        {
            return (0, domainEvents);
        }

        try
        {
            var written = await _saveCore(acceptAllChangesOnSuccess, cancellationToken);

            // The replay guard's input. Only meaningful inside a transaction this context opened,
            // and only when the save accepted its changes — an unaccepted save leaves the tracker
            // able to replay itself, which is precisely the distinction the guard turns on.
            if (acceptAllChangesOnSuccess && _context.Database.CurrentTransaction is not null)
            {
                _replayGuard.MarkWriteAccepted();
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

        // BBL-002. The platform declares 234 unique indexes outside migrations across 142
        // configuration files. Three modules translated this themselves - Financial through an
        // internal UniqueIndexViolation helper, Creator and CommunityProgramme each through a
        // private copy of the same predicate - and every other index answered a concurrent
        // duplicate with 500 Platform.Unexpected. Translating here rather than relocating that
        // helper means no module has to ask the question, which is also what makes
        // ConcurrencyConflictException's claim that EF Core does not travel upward true: six catch
        // sites in three modules' Application folders were catching DbUpdateException.
        //
        // **Second, and the order is defence rather than the guard.** DbUpdateConcurrencyException
        // derives from DbUpdateException, so a reader expects the derived arm first and gets it.
        // But swapping them changes nothing measurable today, and that was checked rather than
        // assumed: EF's concurrency message is about how many rows an operation affected and
        // carries none of the three substrings below, so the filter excludes it wherever it sits.
        // The order costs nothing and stops mattering only until somebody widens the filter.
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            // The message is deliberately generic. A constraint name is the most useful thing a
            // developer can be told here and the least safe thing to tell a caller - it names a
            // table and its columns. The inner exception carries it, and TransactionBehavior logs
            // the exception object on rollback, so it survives in logs without being published.
            throw new DuplicateRecordException(
                "A record with those details already exists.", exception);
        }
    }

    /// <summary>
    /// Whether a failed save was a unique-index violation.
    /// <para>
    /// <b>Matched on the message rather than on a provider error code, which is a decision rather
    /// than a shortcut.</b> Production runs Npgsql and the suite SQLite, and the two report this
    /// differently - <c>23505</c> against <c>SQLITE_CONSTRAINT_UNIQUE</c>. The substrings below
    /// are the ones both put in the inner exception's message. A miss costs a 500 where a 409 was
    /// meant, which is the same answer as not catching at all rather than a wrong success - so the
    /// failure mode of a message match is the status quo, never a wrong status on a different kind
    /// of <see cref="DbUpdateException"/>.
    /// </para>
    /// <para>
    /// This was three identical private copies, one per module that had noticed the problem. It is
    /// private here because nothing outside this class needs to ask any more.
    /// </para>
    /// <para>
    /// <b>A primary-key collision matches too, and that is intended.</b> Neither provider
    /// distinguishes it in the message, and a caller-supplied id that is already taken is a
    /// duplicate in exactly the sense the 409 describes. The one case it reads oddly is a platform
    /// bug that re-sends an already-inserted row, which now surfaces as a 409 rather than a 500;
    /// the inner exception still names the table.
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

    private void ApplyAuditFields()
    {
        var now = _clock?.UtcNow ?? DateTimeOffset.UtcNow;
        var userId = _currentUser?.UserId?.ToString();

        foreach (var entry in _context.ChangeTracker.Entries<IAuditableEntity>())
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
        SoftDeleteConverter.Apply(_context.ChangeTracker, now, userId);
    }

    private List<IDomainEvent> CollectDomainEvents()
    {
        // Matched through IHasDomainEvents rather than a concrete Entity<TId> closure:
        // aggregates use strongly typed identifiers, so Entity<Guid> matches nothing.
        var tracked = _context.ChangeTracker.Entries<IHasDomainEvents>()
            .Where(e => e.Entity.DomainEvents.Count > 0)
            .ToList();

        var domainEvents = tracked
            .SelectMany(e => e.Entity.DomainEvents)
            .ToList();

        tracked.ForEach(e => e.Entity.ClearDomainEvents());

        return domainEvents;
    }
}
