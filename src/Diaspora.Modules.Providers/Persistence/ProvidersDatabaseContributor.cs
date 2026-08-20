using Diaspora.Modules.Providers.Persistence.Configurations;
using Diaspora.Persistence.Modules;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Providers.Persistence;

/// <summary>
/// Adds the Providers module's tables to the shared context.
/// <para>
/// Twenty-two tables in the module's own <c>providers</c> schema, which the context verifies at
/// startup. The boundary matters here for a reason particular to this module: B7 is the one
/// every future business vertical stores a foreign key into, so a table of somebody else's
/// leaking into this schema would be a dependency nobody could see in a project reference.
/// </para>
/// <para>
/// Four configurations, not twenty-two, because eighteen of those tables are owned by the
/// provider or category aggregates and are configured from inside them. That is the aggregate
/// boundary made visible in the persistence layer: if a table can be reached without loading its
/// root, it should not be owned.
/// </para>
/// </summary>
public sealed class ProvidersDatabaseContributor : IDatabaseContributor
{
    public string Module => ProvidersModuleMetadata.Name;

    public string Schema => ProvidersModuleMetadata.Schema;

    public void Configure(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        // The provider aggregate — owns members, services and their offerings, service areas,
        // branches, languages, contact methods, gallery, certifications, tags, the weekly
        // schedule with its windows, dated exceptions, and verification claims.
        modelBuilder.ApplyConfiguration(new ProviderConfiguration());

        // The category catalogue — owns aliases, translations and keywords.
        modelBuilder.ApplyConfiguration(new ServiceCategoryConfiguration());

        // Separate aggregates, keyed by provider. See ProviderReview for why they are not owned.
        modelBuilder.ApplyConfiguration(new ProviderReviewConfiguration());
        modelBuilder.ApplyConfiguration(new ProviderRecommendationConfiguration());
    }
}
