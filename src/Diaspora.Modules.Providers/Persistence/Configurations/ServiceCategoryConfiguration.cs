using Diaspora.Modules.Providers.Domain.Categories;
using Diaspora.Modules.Providers.Domain.Identifiers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Providers.Persistence.Configurations;

/// <summary>
/// Maps the service category catalogue and everything it owns.
/// <para>
/// <b>No navigation to the parent, only a foreign key.</b> A self-referencing navigation would
/// let a caller walk the tree lazily and turn a category chip row into a query per level; the
/// materialised <see cref="CategoryPath"/> answers every hierarchical question in one index scan
/// instead, and the parent identifier is there for the rare case that genuinely needs it.
/// </para>
/// <para>
/// The normalised alias and keyword columns are what resolution actually compares — a lookup on
/// the human-readable value would have to be case- and punctuation-insensitive, which means
/// either a function index (not portable) or a full scan (not fast). Storing the comparison form
/// alongside the display form costs one short column and makes the lookup an equality match.
/// </para>
/// </summary>
public sealed class ServiceCategoryConfiguration : IEntityTypeConfiguration<ServiceCategory>
{
    public void Configure(EntityTypeBuilder<ServiceCategory> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            ProviderPersistenceConstants.ServiceCategoriesTable, ProvidersModuleMetadata.Schema);

        builder.HasKey(category => category.Id);

        builder.Property(category => category.Id)
            .HasConversion(ProviderValueConverters.Id<ServiceCategoryId>());

        builder.Property(category => category.ParentId)
            .HasConversion(ProviderValueConverters.Id<ServiceCategoryId>());

        builder.Property(category => category.Name)
            .HasMaxLength(ServiceCategory.MaxNameLength)
            .IsRequired();

        builder.Property(category => category.Description)
            .HasMaxLength(ServiceCategory.MaxDescriptionLength);

        builder.Property(category => category.Icon)
            .HasMaxLength(ServiceCategory.MaxIconLength);

        builder.OwnsOne(category => category.Slug, slug =>
        {
            slug.Property(value => value.Value)
                .HasColumnName("slug")
                .HasMaxLength(ProviderPersistenceConstants.CategorySlugLength)
                .IsRequired();

            slug.HasIndex(value => value.Value)
                .IsUnique()
                .HasDatabaseName("ux_provider_service_categories_slug");
        });

        builder.Navigation(category => category.Slug).IsRequired();

        builder.OwnsOne(category => category.Path, path =>
        {
            path.Property(value => value.Value)
                .HasColumnName("path")
                .HasMaxLength(ProviderPersistenceConstants.CategoryPathLength)
                .IsRequired();

            path.Property(value => value.Depth).HasColumnName("depth");

            // The subtree query's whole index. Everything beneath a node is one range scan.
            path.HasIndex(value => value.Value)
                .HasDatabaseName("ix_provider_service_categories_path");

            path.Ignore(value => value.SubtreePrefix);
            path.Ignore(value => value.Segments);
        });

        builder.Navigation(category => category.Path).IsRequired();

        ConfigureAliases(builder);
        ConfigureTranslations(builder);
        ConfigureKeywords(builder);

        // The filter row's query: active nodes, in curated order.
        builder.HasIndex(category => new { category.IsActive, category.SortOrder })
            .HasDatabaseName("ix_provider_service_categories_active_sort");

        builder.HasIndex(category => category.ParentId)
            .HasDatabaseName("ix_provider_service_categories_parent");

        builder.Ignore(category => category.DomainEvents);
        builder.Ignore(category => category.IsRoot);
    }

    private static void ConfigureAliases(EntityTypeBuilder<ServiceCategory> builder) =>
        builder.OwnsMany(category => category.Aliases, alias =>
        {
            alias.ToTable(
                ProviderPersistenceConstants.ServiceCategoryAliasesTable,
                ProvidersModuleMetadata.Schema);

            alias.HasKey(value => value.Id);

            alias.Property(value => value.Id)
                .HasConversion(ProviderValueConverters.Id<ServiceCategoryAliasId>());

            alias.Property(value => value.Value)
                .HasMaxLength(ServiceCategoryAlias.MaxValueLength)
                .IsRequired();

            alias.Property(value => value.Normalized)
                .HasMaxLength(ServiceCategoryAlias.MaxValueLength)
                .IsRequired();

            alias.Property(value => value.Source).HasMaxLength(64);

            alias.WithOwner().HasForeignKey("CategoryId");

            // The whole point of aliases: resolving a legacy client string in one equality match.
            alias.HasIndex(value => value.Normalized)
                .HasDatabaseName("ix_provider_category_aliases_normalized");

            alias.Ignore(value => value.DomainEvents);
        });

    private static void ConfigureTranslations(EntityTypeBuilder<ServiceCategory> builder) =>
        builder.OwnsMany(category => category.Translations, translation =>
        {
            translation.ToTable(
                ProviderPersistenceConstants.ServiceCategoryTranslationsTable,
                ProvidersModuleMetadata.Schema);

            translation.HasKey(value => value.Id);

            translation.Property(value => value.Id)
                .HasConversion(ProviderValueConverters.Id<ServiceCategoryTranslationId>());

            translation.Property(value => value.LanguageCode)
                .HasMaxLength(ServiceCategoryTranslation.MaxLanguageLength)
                .IsRequired();

            translation.Property(value => value.Name)
                .HasMaxLength(ServiceCategoryTranslation.MaxNameLength)
                .IsRequired();

            translation.Property(value => value.Description)
                .HasMaxLength(ServiceCategoryTranslation.MaxDescriptionLength);

            translation.WithOwner().HasForeignKey("CategoryId");

            translation.HasIndex("CategoryId", "LanguageCode")
                .IsUnique()
                .HasDatabaseName("ux_provider_category_translations_language");

            translation.Ignore(value => value.DomainEvents);
        });

    private static void ConfigureKeywords(EntityTypeBuilder<ServiceCategory> builder) =>
        builder.OwnsMany(category => category.Keywords, keyword =>
        {
            keyword.ToTable(
                ProviderPersistenceConstants.ServiceCategoryKeywordsTable,
                ProvidersModuleMetadata.Schema);

            keyword.HasKey(value => value.Id);

            keyword.Property(value => value.Id)
                .HasConversion(ProviderValueConverters.Id<ServiceCategoryKeywordId>());

            keyword.Property(value => value.Value)
                .HasMaxLength(ServiceCategoryKeyword.MaxValueLength)
                .IsRequired();

            keyword.Property(value => value.Normalized)
                .HasMaxLength(ServiceCategoryKeyword.MaxValueLength)
                .IsRequired();

            keyword.Property(value => value.LanguageCode)
                .HasMaxLength(ProviderPersistenceConstants.LanguageCodeLength);

            keyword.WithOwner().HasForeignKey("CategoryId");

            keyword.HasIndex(value => value.Normalized)
                .HasDatabaseName("ix_provider_category_keywords_normalized");

            keyword.Ignore(value => value.DomainEvents);
        });
}
