using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Recommendations.Persistence.Repositories;

/// <summary>
/// Marks an aggregate dirty without undoing an insert.
/// <para>
/// <b>The trap this exists to close has cost this platform an afternoon before, and this module is
/// unusually exposed to it.</b> Four of its aggregates are get-or-created — the profile, the
/// preference record, the candidate projection, the analytics rollup — so a handler routinely holds
/// one the unit of work has just <c>Added</c> and then, quite reasonably, calls <c>Update</c> on it.
/// <c>DbSet.Update</c> flips an <c>Added</c> entity to <c>Modified</c>. EF then skips the parent
/// INSERT, still inserts the rows of its owned collections, and the database refuses the orphans
/// with a foreign-key violation that names a child table and says nothing at all about the cause.
/// </para>
/// <para>
/// The guard is one line and the alternative was a rule every handler had to remember. Entities that
/// are already tracked need no call either — the change tracker has them — so the only case that
/// genuinely needs <c>Update</c> is an aggregate that arrived detached.
/// </para>
/// </summary>
internal static class RecommendationAggregateUpdater
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
