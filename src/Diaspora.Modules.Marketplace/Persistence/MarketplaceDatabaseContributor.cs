using Diaspora.Modules.Marketplace.Persistence.Configurations;
using Diaspora.Persistence.Modules;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Marketplace.Persistence;

/// <summary>
/// Adds the Marketplace module's tables to the shared context.
/// <para>
/// Twenty-one tables in the module's own <c>marketplace</c> schema, which the context verifies at
/// startup. Eleven configurations, not twenty-one, because ten of those tables are owned by an
/// aggregate and are configured from inside it — the aggregate boundary made visible in the
/// persistence layer: if a table can be reached without loading its root, it should not be owned.
/// </para>
/// <para>
/// <b>Five of the eleven aggregates are <c>ISoftDeletable</c>, so the shared context applies the
/// global query filter and <c>SoftDeleteConverter</c> handles the owned rows on delete.</b> Nothing
/// here declares either, deliberately — B1.0.1 exists so a module never has to.
/// </para>
/// <para>
/// The six that are not are reference data, member-owned rows and the measured record. A category
/// and an attribute definition are retired with a flag rather than deleted, because listings store
/// their keys. A saved listing is hard-deleted, because unsaving is a member asking the platform to
/// forget. A report and a promotion are records of decisions and are never removed. And analytics
/// outlives the listing's visibility, because a seller's own history is computed from listings they
/// archived years ago.
/// </para>
/// </summary>
public sealed class MarketplaceDatabaseContributor : IDatabaseContributor
{
    public string Module => MarketplaceModuleMetadata.Name;

    public string Schema => MarketplaceModuleMetadata.Schema;

    public void Configure(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        // The listing aggregate — owns its attributes, tags, media, provider and guide references
        // and its moderation trail.
        modelBuilder.ApplyConfiguration(new MarketplaceListingConfiguration());

        // Who is selling. Owns only value objects, so one table.
        modelBuilder.ApplyConfiguration(new SellerProfileConfiguration());

        // The commerce record. Separate aggregates keyed by listing — see each for why they are
        // not owned: a popular item attracts hundreds of both.
        modelBuilder.ApplyConfiguration(new ListingOfferConfiguration());
        modelBuilder.ApplyConfiguration(new ListingReservationConfiguration());

        // The extensible catalogues, seeded by contributors at startup.
        modelBuilder.ApplyConfiguration(new ListingCategoryConfiguration());
        modelBuilder.ApplyConfiguration(new ListingAttributeDefinitionConfiguration());

        // Member-owned rows and the safety surface.
        modelBuilder.ApplyConfiguration(new SavedListingConfiguration());
        modelBuilder.ApplyConfiguration(new ListingReportConfiguration());
        modelBuilder.ApplyConfiguration(new ListingPromotionConfiguration());

        // The messaging seam a future Communication module lands on.
        modelBuilder.ApplyConfiguration(new ListingConversationConfiguration());

        // The measured record.
        modelBuilder.ApplyConfiguration(new ListingAnalyticsConfiguration());
    }
}
