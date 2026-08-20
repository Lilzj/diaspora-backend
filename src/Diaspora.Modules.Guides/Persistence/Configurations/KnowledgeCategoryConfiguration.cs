using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Knowledge;
using Diaspora.Modules.Guides.Domain.Policies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Guides.Persistence.Configurations;

/// <summary>
/// Maps the Knowledge Directory's navigation catalogue and the three collections it owns.
/// <para>
/// <b>Every owned collection declares its own key.</b> Left to convention EF invents a
/// store-generated shadow integer inside a composite primary key; PostgreSQL tolerates it and
/// SQLite refuses the insert with a not-null violation — but only once a row actually exists, so a
/// suite that reads before it writes stays green. B11 paid for that in four collections and the
/// platform now fails the build for it. All three here declare a natural composite key, which they
/// have: a locale, a source key and a provider key are each unique within their category.
/// </para>
/// <para>
/// <b>The unique index on the key is the contract.</b> The key is what a contributor seed declares,
/// what a URL carries and what every country directory entry points at; two rows claiming one key
/// would strand every entry between them, so the database refuses the second rather than trusting a
/// handler to have checked.
/// </para>
/// <para>
/// Archiving is a soft delete, so the shared context applies the global query filter and
/// <c>SoftDeleteConverter</c> handles the owned rows — nothing here declares either, deliberately.
/// </para>
/// </summary>
public sealed class KnowledgeCategoryConfiguration : IEntityTypeConfiguration<KnowledgeCategory>
{
    public void Configure(EntityTypeBuilder<KnowledgeCategory> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(KnowledgePersistenceConstants.CategoriesTable, GuidesModuleMetadata.Schema);

        builder.HasKey(category => category.Id);

        builder.Property(category => category.Id)
            .HasConversion(GuideValueConverters.Id<KnowledgeCategoryId>());

        builder.Property(category => category.Key)
            .HasMaxLength(KnowledgeLimits.MaxCategoryKey)
            .IsRequired();

        builder.Property(category => category.Name)
            .HasMaxLength(KnowledgeLimits.MaxCategoryName)
            .IsRequired();

        builder.Property(category => category.Description)
            .HasMaxLength(KnowledgeLimits.MaxCategoryDescription);

        builder.Property(category => category.Icon)
            .HasMaxLength(KnowledgeLimits.MaxIcon);

        // The concurrency token. Curation is the one place on this platform where simultaneous
        // editors are the norm, and without this the second save silently wins.
        builder.Property(category => category.Revision).IsConcurrencyToken();

        builder.HasIndex(category => category.Key)
            .IsUnique()
            .HasDatabaseName("ux_knowledge_categories_key");

        // The catalogue is always read in order, and always in full.
        builder.HasIndex(category => category.Order)
            .HasDatabaseName("ix_knowledge_categories_order");

        ConfigureLocalizations(builder);
        ConfigureSources(builder);
        ConfigureProviderCategories(builder);

        builder.Ignore(category => category.DomainEvents);
        builder.Ignore(category => category.IsActive);
        builder.Ignore(category => category.EffectiveSourceKeys);
    }

    private static void ConfigureLocalizations(EntityTypeBuilder<KnowledgeCategory> builder) =>
        builder.OwnsMany(category => category.Localizations, localization =>
        {
            localization.ToTable(
                KnowledgePersistenceConstants.CategoryLocalizationsTable,
                GuidesModuleMetadata.Schema);

            localization.Property(value => value.Locale)
                .HasMaxLength(KnowledgeLimits.MaxLocale)
                .IsRequired();

            localization.Property(value => value.Name)
                .HasMaxLength(KnowledgeLimits.MaxCategoryName)
                .IsRequired();

            localization.Property(value => value.Description)
                .HasMaxLength(KnowledgeLimits.MaxCategoryDescription);

            localization.WithOwner().HasForeignKey("KnowledgeCategoryId");

            // The pair is the identity: the aggregate deduplicates by locale, so (category, locale)
            // is naturally unique and no surrogate is needed.
            localization.HasKey(
                "KnowledgeCategoryId", nameof(KnowledgeCategoryLocalization.Locale));
        });

    private static void ConfigureSources(EntityTypeBuilder<KnowledgeCategory> builder) =>
        builder.OwnsMany(category => category.Sources, source =>
        {
            source.ToTable(
                KnowledgePersistenceConstants.CategorySourcesTable, GuidesModuleMetadata.Schema);

            source.Property(value => value.GuideCategoryKey)
                .HasMaxLength(GuideLimits.MaxCategoryKey)
                .IsRequired();

            source.WithOwner().HasForeignKey("KnowledgeCategoryId");

            source.HasKey(
                "KnowledgeCategoryId", nameof(KnowledgeCategorySource.GuideCategoryKey));

            // "Which navigation rows draw from this filing category" — asked whenever a guide
            // category is retired, to find the rows that will empty.
            source.HasIndex(value => value.GuideCategoryKey)
                .HasDatabaseName("ix_knowledge_category_sources_guide_category");
        });

    private static void ConfigureProviderCategories(EntityTypeBuilder<KnowledgeCategory> builder) =>
        builder.OwnsMany(category => category.ProviderCategories, provider =>
        {
            provider.ToTable(
                KnowledgePersistenceConstants.CategoryProviderCategoriesTable,
                GuidesModuleMetadata.Schema);

            provider.Property(value => value.ServiceCategoryKey)
                .HasMaxLength(KnowledgeLimits.MaxCategoryKey)
                .IsRequired();

            provider.WithOwner().HasForeignKey("KnowledgeCategoryId");

            provider.HasKey(
                "KnowledgeCategoryId", nameof(KnowledgeProviderCategory.ServiceCategoryKey));
        });
}
