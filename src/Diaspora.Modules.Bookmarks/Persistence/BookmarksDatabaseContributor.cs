using Diaspora.Persistence.Modules;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Bookmarks.Persistence;

/// <summary>
/// Adds the module's entities to the shared <c>DiasporaDbContext</c>.
/// <para>
/// The whole of the module's coupling to persistence: it declares its schema and applies
/// its configurations, and the context's startup check confirms it stayed inside that
/// schema. It never sees the context type, the connection string, or any other module's
/// tables.
/// </para>
/// </summary>
public sealed class BookmarksDatabaseContributor : IDatabaseContributor
{
    public string Module => BookmarksModuleMetadata.Name;

    public string Schema => BookmarksModuleMetadata.Schema;

    public void Configure(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfiguration(new BookmarkEntityConfiguration());
    }
}
