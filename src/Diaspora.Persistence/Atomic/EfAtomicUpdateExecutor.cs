using System.Data;
using System.Data.Common;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace Diaspora.Persistence.Atomic;

/// <summary>
/// The shipped executor: EF's model for the shape, the caller's connection for the statement, and
/// the caller's change tracker put back in step afterwards.
/// <para>
/// <b>Why raw ADO rather than the change tracker.</b> A guarded update is exactly the operation the
/// change tracker cannot express. Tracking means read-decide-write, and the decision is made against
/// a snapshot that a second transaction may already have invalidated; the whole point of this class
/// is to move the decision into the statement, where the row is locked while it is made. Going
/// through <c>ExecuteUpdate</c> would be closer to home, but it does not compose with the arithmetic
/// guard on a column split into an owned type — which is where every counter on this platform lives.
/// </para>
/// <para>
/// <b>Why it then reconciles.</b> Bypassing the change tracker leaves it wrong, and a change tracker
/// that is wrong about a counter is worse than no atomic update at all: the aggregate's own
/// <c>SaveChanges</c> would write the value it computed over the value the database arbitrated,
/// undoing the guarantee a few milliseconds after establishing it. So the executor reads the
/// columns back inside the same transaction and tells the entry they are already settled. Callers
/// that pass a tracked instance get this for free; callers that do not are on their own, and the
/// interface says so.
/// </para>
/// <para>
/// <b>What it deliberately does not do.</b> It does not commit, does not open a transaction, does
/// not respect query filters, and does not touch concurrency tokens. Not committing is what keeps
/// the hold and the reservation atomic together. Ignoring the soft-delete filter is deliberate too:
/// units held against a listing somebody has since deleted must still be returnable, or the counts
/// never reconcile. And leaving the concurrency token alone is the point of the whole exercise — a
/// buyer taking the last unit must not fail because a seller is midway through an edit.
/// </para>
/// </summary>
public sealed class EfAtomicUpdateExecutor : IAtomicUpdateExecutor
{
    private readonly DiasporaDbContext _context;

    public EfAtomicUpdateExecutor(DiasporaDbContext context)
    {
        _context = context;
    }

    public async Task<AtomicUpdateResult> ExecuteAsync<TEntity>(
        AtomicUpdate<TEntity> update, CancellationToken cancellationToken = default)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(update);

        var sqlHelper = _context.GetService<ISqlGenerationHelper>();
        var model = _context.Model;

        var statement = AtomicStatement.Update(model, sqlHelper, update);

        var opened = await EnsureConnectionAsync(cancellationToken);

        try
        {
            var rows = await ExecuteNonQueryAsync(statement, cancellationToken);

            if (rows == 0 || update.ReadBackPaths.Count == 0)
            {
                return new AtomicUpdateResult(rows);
            }

            var state = await ReadBackAsync<TEntity>(
                model, sqlHelper, update.Key, update.ReadBackPaths, cancellationToken);

            if (update.Tracked is not null)
            {
                Reconcile(_context, model, update.Tracked, state);
            }

            return new AtomicUpdateResult(rows, state);
        }
        finally
        {
            if (opened)
            {
                await _context.Database.CloseConnectionAsync();
            }
        }
    }

    /// <summary>
    /// Opens the connection when nothing else has.
    /// <para>
    /// Inside a transaction — the normal case — it is already open and this does nothing. Outside
    /// one, EF's ref-counted open and close leave the connection exactly as it was found, which
    /// matters because the context is shared with the unit of work.
    /// </para>
    /// </summary>
    private async Task<bool> EnsureConnectionAsync(CancellationToken cancellationToken)
    {
        if (_context.Database.GetDbConnection().State == ConnectionState.Open)
        {
            return false;
        }

        await _context.Database.OpenConnectionAsync(cancellationToken);

        return true;
    }

    private async Task<int> ExecuteNonQueryAsync(
        AtomicStatement statement, CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(statement);

        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<IReadOnlyDictionary<string, long>> ReadBackAsync<TEntity>(
        Microsoft.EntityFrameworkCore.Metadata.IModel model,
        ISqlGenerationHelper sqlHelper,
        object key,
        IReadOnlyList<string> paths,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        var statement = AtomicStatement.Read<TEntity>(model, sqlHelper, key, paths);

        await using var command = CreateCommand(statement);
        await using var reader = await command.ExecuteReaderAsync(
            CommandBehavior.SingleRow, cancellationToken);

        var state = new Dictionary<string, long>(paths.Count, StringComparer.Ordinal);

        if (!await reader.ReadAsync(cancellationToken))
        {
            // The row went away between the update and the read, inside our own transaction. Not
            // reachable in practice, and returning nothing is honest rather than inventing a count.
            return state;
        }

        for (var index = 0; index < paths.Count; index++)
        {
            if (!reader.IsDBNull(index))
            {
                state[paths[index]] = Convert.ToInt64(
                    reader.GetValue(index), CultureInfo.InvariantCulture);
            }
        }

        return state;
    }

    /// <summary>
    /// Builds the command on the context's own connection and enlists it in the transaction the
    /// caller opened, so the statement is part of the caller's unit of work rather than beside it.
    /// </summary>
    private DbCommand CreateCommand(AtomicStatement statement)
    {
        var connection = _context.Database.GetDbConnection();
        var command = connection.CreateCommand();

        // Every identifier in the text came from the model and through the provider's delimiter,
        // and every value is a parameter. Same justification as the transaction receipt's insert.
#pragma warning disable CA2100
        command.CommandText = statement.Sql;
#pragma warning restore CA2100

        command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();

        if (_context.Database.GetCommandTimeout() is { } timeout)
        {
            command.CommandTimeout = timeout;
        }

        foreach (var parameter in statement.Parameters)
        {
            command.Parameters.Add(CreateParameter(command, parameter));
        }

        return command;
    }

    /// <summary>
    /// Shapes a value the way the column holds it.
    /// <para>
    /// A converted property builds its parameter through its own relational type mapping, which is
    /// the only thing that knows a strongly typed identifier is a <c>uuid</c> on PostgreSQL and
    /// <c>TEXT</c> on SQLite. Plain counters are bound as integers, which every provider agrees on.
    /// </para>
    /// </summary>
    private static DbParameter CreateParameter(DbCommand command, AtomicParameter parameter)
    {
        if (parameter.Property?.GetRelationalTypeMapping() is { } mapping)
        {
            return mapping.CreateParameter(command, parameter.Name, parameter.Value);
        }

        var plain = command.CreateParameter();

        plain.ParameterName = parameter.Name;
        plain.Value = parameter.Value ?? DBNull.Value;

        return plain;
    }

    /// <summary>
    /// Puts the tracked instance back in step with the row.
    /// <para>
    /// <b>The step that stops the platform undoing its own guarantee.</b> Each reconciled column has
    /// its current and original values set to what the database now holds and is marked unmodified,
    /// so the unit of work's own <c>UPDATE</c> leaves it alone. Everything else the caller changed —
    /// a status transition, an engagement count, a timestamp — is untouched and still saves.
    /// </para>
    /// </summary>
    private static void Reconcile<TEntity>(
        DbContext context,
        Microsoft.EntityFrameworkCore.Metadata.IModel model,
        TEntity entity,
        IReadOnlyDictionary<string, long> state)
        where TEntity : class
    {
        var root = context.Entry(entity);

        if (root.State == EntityState.Detached)
        {
            return;
        }

        foreach (var (path, value) in state)
        {
            var column = AtomicColumnResolver.Column(model, typeof(TEntity), path);
            var owner = Walk(root, column.Navigations);

            if (owner is null)
            {
                continue;
            }

            var property = owner.Property(column.PropertyName);
            var target = Nullable.GetUnderlyingType(property.Metadata.ClrType)
                ?? property.Metadata.ClrType;

            var settled = Convert.ChangeType(value, target, CultureInfo.InvariantCulture);

            property.CurrentValue = settled;

            if (owner.State is EntityState.Added or EntityState.Deleted)
            {
                // A row being inserted has no original value to restate, and one being deleted is
                // not going to be written back. Either way there is nothing to clear.
                continue;
            }

            property.OriginalValue = settled;
            property.IsModified = false;
        }
    }

    /// <summary>Follows the owned reference navigations a path crossed.</summary>
    private static EntityEntry? Walk(EntityEntry entry, IReadOnlyList<string> navigations)
    {
        var current = entry;

        foreach (var navigation in navigations)
        {
            current = current.Reference(navigation).TargetEntry;

            if (current is null)
            {
                return null;
            }
        }

        return current;
    }
}
