using Microsoft.EntityFrameworkCore;

namespace Diaspora.Persistence.Identity.Repositories;

/// <summary>
/// Marks an aggregate as modified without fighting the change tracker.
/// <para>
/// Handlers call <c>Update</c> to say "this aggregate changed", which reads well and keeps
/// the intent explicit. Passing that straight to <c>DbSet.Update</c> is wrong for an entity
/// the tracker already holds: an aggregate added earlier in the same unit of work is in the
/// <see cref="EntityState.Added"/> state, and <c>Update</c> flips it to
/// <see cref="EntityState.Modified"/> — EF then issues an UPDATE against a row that has not
/// been inserted yet and fails with a concurrency error. Registration hits this exactly:
/// the account is added, then updated to record that phone verification has begun.
/// </para>
/// <para>
/// For anything the tracker already knows about, EF detects the changes on its own, so the
/// correct action is none at all.
/// </para>
/// </summary>
internal static class TrackedEntityUpdater
{
    public static void MarkModified<TEntity>(DbContext dbContext, DbSet<TEntity> set, TEntity entity)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(set);
        ArgumentNullException.ThrowIfNull(entity);

        if (dbContext.Entry(entity).State == EntityState.Detached)
        {
            set.Update(entity);
        }
    }
}
