using Diaspora.Modules.Media.Persistence.Configurations;
using Diaspora.Persistence.Modules;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Media.Persistence;

/// <summary>
/// Adds the Media module's tables to the shared <c>DiasporaDbContext</c>.
/// <para>
/// Nine tables in the module's own <c>media</c> schema, which the context verifies at startup.
/// The schema boundary matters here for a reason specific to media: this is the module most
/// likely to be extracted into its own service, because storage and processing scale on a
/// different curve from everything else on the platform. An extraction is a schema move only
/// if nothing else lives in the schema.
/// </para>
/// </summary>
public sealed class MediaDatabaseContributor : IDatabaseContributor
{
    public string Module => MediaModuleMetadata.Name;

    public string Schema => MediaModuleMetadata.Schema;

    public void Configure(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        // Assets — owns media_variants and media_grants
        modelBuilder.ApplyConfiguration(new MediaAssetConfiguration());

        // Uploads in flight — owns upload_chunks
        modelBuilder.ApplyConfiguration(new UploadSessionConfiguration());

        // Galleries and attachment sets — owns media_collection_items
        modelBuilder.ApplyConfiguration(new MediaCollectionConfiguration());

        // Post-upload work — owns processing_tasks
        modelBuilder.ApplyConfiguration(new ProcessingJobConfiguration());
    }
}
