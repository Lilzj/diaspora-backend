using Diaspora.Domain.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Diaspora.Persistence.ChangeTracking;

/// <summary>
/// Turns a requested deletion of an <see cref="ISoftDeletable"/> aggregate into a stamped
/// update, and repairs the collateral damage EF Core's cascade does on the way.
/// <para>
/// <b>The defect this exists to fix (B1.0.1, D-1).</b> Calling <c>Remove()</c> on an aggregate
/// does not only mark that aggregate <see cref="EntityState.Deleted"/>. EF immediately cascades
/// the deletion to every <em>owned</em> entry in its graph — owned value objects sharing the
/// aggregate's table, owned collections in tables of their own, and anything nested inside
/// those. Flipping only the aggregate back to <see cref="EntityState.Modified"/>, which is what
/// this platform did before B1.0.1, leaves those owned entries Deleted.
/// </para>
/// <para>
/// For an owned type sharing the aggregate's table that is not a row deletion — there is no
/// separate row to delete — so EF expresses it as an <c>UPDATE … SET owner_type = NULL,
/// storage_key = NULL, …</c>. The database refuses:
/// </para>
/// <code>
/// SQLite Error 19: NOT NULL constraint failed: media_assets.owner_type
/// </code>
/// <para>
/// For an owned collection in its own table it is worse than an error: the delete <em>succeeds</em>,
/// and a soft-deleted aggregate silently loses its children. Restoring it later returns an
/// aggregate with an empty collection and no indication anything was lost.
/// </para>
/// <para>
/// <b>Why no module hit this before B5.</b> Identity's aggregates are soft-deletable with no
/// owned types. Location's aggregates have owned types and are not soft-deletable. Trust's are
/// neither. <c>MediaAsset</c> is the platform's first aggregate that is both — four owned
/// references, two owned collections, one of them nested — which is why a defect that had been
/// latent since B1 surfaced in B5.
/// </para>
/// <para>
/// <b>What this does.</b> Restores every cascade-deleted owned entry, recursively, to the state
/// it would have been in had the deletion never been requested — then re-marks exactly those
/// properties whose value genuinely differs from the loaded original, so a caller that changed
/// an owned value object in the same unit of work does not silently lose the change.
/// </para>
/// <para>
/// Nothing here is provider-specific. It operates on <see cref="Microsoft.EntityFrameworkCore.ChangeTracking.ChangeTracker"/>
/// state and EF metadata only — no SQL, no table names, no provider capabilities — so it behaves
/// identically on SQLite, PostgreSQL, SQL Server and anything added later.
/// </para>
/// </summary>
public static class SoftDeleteConverter
{
    /// <summary>
    /// Converts every pending deletion of a soft-deletable entity into a stamped update.
    /// <para>
    /// Called from the single save path, after audit stamping, so the ordering B2.6 established
    /// is unchanged: an entity moving from Deleted to Modified is stamped by
    /// <see cref="ISoftDeletable.MarkDeleted"/> and not by the auditor, which has already run
    /// and correctly ignored it.
    /// </para>
    /// </summary>
    public static void Apply(
        Microsoft.EntityFrameworkCore.ChangeTracking.ChangeTracker changeTracker,
        DateTimeOffset deletedAt,
        string? deletedBy)
    {
        ArgumentNullException.ThrowIfNull(changeTracker);

        // Materialised before mutating anything. Changing an entry's state can move entries in
        // and out of the tracker's internal collections, and enumerating one while it is being
        // reshaped is how this loop would start throwing under a graph nobody tested.
        var deletions = changeTracker
            .Entries<ISoftDeletable>()
            .Where(entry => entry.State == EntityState.Deleted)
            .ToList();

        foreach (var entry in deletions)
        {
            // The whole aggregate is rewritten, which is the behaviour every module has relied
            // on since B1: a soft delete is an update of the whole row.
            entry.State = EntityState.Modified;
            entry.Entity.MarkDeleted(deletedAt, deletedBy);

            RestoreOwnedGraph(entry);
        }
    }

    /// <summary>
    /// Walks an entry's owned navigations and restores anything the cascade deleted.
    /// <para>
    /// Both shapes are handled because both are broken by the cascade in different ways: an
    /// owned <em>reference</em> nulls the aggregate's own columns and fails loudly, an owned
    /// <em>collection</em> deletes its rows and fails silently. Only navigations whose target is
    /// owned are followed — a genuine foreign-key child is a separate aggregate with its own
    /// lifecycle, and reverting its deletion would be this component inventing a cascade policy
    /// nobody asked for.
    /// </para>
    /// </summary>
    private static void RestoreOwnedGraph(EntityEntry owner)
    {
        foreach (var navigation in owner.Navigations)
        {
            if (!navigation.Metadata.TargetEntityType.IsOwned())
            {
                continue;
            }

            switch (navigation)
            {
                case ReferenceEntry { TargetEntry: { } target }:
                    Restore(target);
                    break;

                case CollectionEntry collection when collection.CurrentValue is not null:
                    foreach (var item in collection.CurrentValue)
                    {
                        if (collection.FindEntry(item) is { } element)
                        {
                            Restore(element);
                        }
                    }

                    break;
            }
        }
    }

    /// <summary>
    /// Un-deletes one owned entry and recurses into anything it owns.
    /// <para>
    /// <b>Why <see cref="EntityState.Unchanged"/> and not <see cref="EntityState.Modified"/>.</b>
    /// A soft delete does not change an aggregate's owned data; it hides the aggregate. Marking
    /// the owned entries Modified would rewrite every one of their columns on every delete —
    /// for an asset with sixteen variants that is sixteen pointless UPDATE statements, and it
    /// would touch rows a concurrency token might be guarding. Unchanged writes nothing, which
    /// is exactly right.
    /// </para>
    /// <para>
    /// <b>Why the property diff.</b> Unchanged on its own would discard a genuine change made in
    /// the same unit of work — mutate an owned value object, then delete the aggregate, and the
    /// mutation vanishes. So the properties whose current value differs from the loaded original
    /// are captured <em>before</em> the state transition and re-marked after it. The result is
    /// the narrowest correct write: nothing for an untouched owned type, exactly the changed
    /// columns for a touched one.
    /// </para>
    /// <para>
    /// The capture happens first because <see cref="EntityEntry.State"/> is not a passive flag —
    /// assigning it re-snapshots the entry, and reading original values afterwards would compare
    /// a value against itself.
    /// </para>
    /// </summary>
    private static void Restore(EntityEntry entry)
    {
        if (entry.State == EntityState.Deleted)
        {
            var changed = entry.Properties
                .Where(property => !Equals(property.CurrentValue, property.OriginalValue))
                .ToList();

            entry.State = EntityState.Unchanged;

            foreach (var property in changed)
            {
                property.IsModified = true;
            }
        }

        // Owned graphs are trees, so this terminates at the depth of the deepest owned type —
        // two, in the platform's largest aggregate today.
        RestoreOwnedGraph(entry);
    }
}
