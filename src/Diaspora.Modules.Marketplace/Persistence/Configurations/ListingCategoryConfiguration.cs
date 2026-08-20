using Diaspora.Modules.Marketplace.Domain.Catalog;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.Modules.Marketplace.Domain.Policies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Marketplace.Persistence.Configurations;

/// <summary>
/// Maps the category tree. One table, hierarchical by materialised path.
/// <para>
/// <b>Not soft-deletable, deliberately.</b> A category is reference data: it is retired with a flag,
/// and a global query filter that hid retired rows would hide them from the very screens that exist
/// to un-retire them. B9's catalogues make the same call.
/// </para>
/// <para>
/// <b>The path is indexed because every subtree query is a prefix match on it.</b> "Everything under
/// Electronics" is <c>LIKE '/electronics/%'</c> against an indexed column rather than a recursive
/// descent — B4's lesson for geography, applied to a tree read on every browse.
/// </para>
/// </summary>
public sealed class ListingCategoryConfiguration : IEntityTypeConfiguration<ListingCategory>
{
    public void Configure(EntityTypeBuilder<ListingCategory> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            MarketplacePersistenceConstants.CategoriesTable, MarketplaceModuleMetadata.Schema);

        builder.HasKey(category => category.Id);

        builder.Property(category => category.Id)
            .HasConversion(MarketplaceValueConverters.Id<ListingCategoryId>());

        builder.Property(category => category.Key)
            .HasMaxLength(MarketplaceLimits.MaxCategoryKey)
            .IsRequired();

        builder.Property(category => category.Name)
            .HasMaxLength(MarketplaceLimits.MaxDisplayName)
            .IsRequired();

        builder.Property(category => category.Description)
            .HasMaxLength(MarketplaceLimits.MaxNoteLength);

        builder.Property(category => category.ParentKey)
            .HasMaxLength(MarketplaceLimits.MaxCategoryKey);

        builder.Property(category => category.Path)
            .HasMaxLength(MarketplaceLimits.MaxAreaPath)
            .IsRequired();

        builder.Property(category => category.IconKey)
            .HasMaxLength(MarketplaceLimits.MaxCategoryKey);

        // The handle listings store. Unique, because two nodes with one key would make every
        // listing under it ambiguous.
        builder.HasIndex(category => category.Key)
            .IsUnique()
            .HasDatabaseName("ux_marketplace_categories_key");

        builder.HasIndex(category => category.Path)
            .HasDatabaseName("ix_marketplace_categories_path");

        builder.HasIndex(category => new { category.ParentKey, category.Order })
            .HasDatabaseName("ix_marketplace_categories_parent_order");

        builder.Ignore(category => category.DomainEvents);
    }
}
