using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Financial.Persistence.Repositories;

/// <summary>
/// Marks an aggregate dirty without undoing an insert.
/// <para>
/// <b>The trap this exists to close has cost the platform an afternoon before.</b>
/// <c>DbSet.Update</c> flips an <c>Added</c> entity to <c>Modified</c>. EF then skips the parent
/// INSERT, still inserts the rows of its owned collections, and the database refuses the orphans
/// with a foreign-key violation naming a child table and saying nothing at all about the cause.
/// </para>
/// <para>
/// This module is unusually exposed to it: the ledger posting service loads a wallet, applies an
/// entry and calls <c>Update</c> — and on the very first posting against a freshly opened wallet,
/// that wallet is still <c>Added</c>.
/// </para>
/// </summary>
internal static class FinancialAggregateUpdater
{
    public static void MarkDirty<TAggregate>(DiasporaDbContext dbContext, TAggregate aggregate)
        where TAggregate : class
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(aggregate);

        var entry = dbContext.Entry(aggregate);

        if (entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
        {
            return;
        }

        if (entry.State == EntityState.Detached)
        {
            dbContext.Update(aggregate);

            return;
        }

        // Tracked and Unchanged. EF will detect what actually changed on the way to SaveChanges;
        // forcing Modified here would rewrite every column of every owned row for one counter bump.
        entry.State = EntityState.Modified;
    }
}
