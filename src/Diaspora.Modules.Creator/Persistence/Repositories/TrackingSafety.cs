using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Creator.Persistence.Repositories;

/// <summary>
/// Staging that cannot turn an insert into an update.
/// <para>
/// <b>This exists because of a defect the platform has already paid for.</b> Calling
/// <c>DbSet.Update</c> on an aggregate the same unit of work has just <c>Add</c>ed flips its state
/// from <c>Added</c> to <c>Modified</c>; EF then skips the parent INSERT and issues an UPDATE that
/// matches nothing, and the failure surfaces as a foreign-key violation naming a <em>child</em>
/// table — several frames away from the line that caused it.
/// </para>
/// <para>
/// It is easy to hit here because this module's flow is genuinely "find or open, then change": a
/// consumer records an action against a milestone the creator has never touched, opens the progress
/// row and immediately writes to it. Making the staging call safe is better than asking every call
/// site to remember which branch it took.
/// </para>
/// </summary>
internal static class TrackingSafety
{
    /// <summary>
    /// Marks an aggregate as changed, unless it is already staged for insert.
    /// <para>
    /// A newly added aggregate needs nothing: EF writes everything about it on the next save, and
    /// telling it the row is modified is exactly the mistake this method prevents.
    /// </para>
    /// </summary>
    public static void StageUpdate<TEntity>(this DiasporaDbContext dbContext, TEntity entity)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(entity);

        var entry = dbContext.Entry(entity);

        if (entry.State is EntityState.Added)
        {
            return;
        }

        dbContext.Set<TEntity>().Update(entity);
    }
}
