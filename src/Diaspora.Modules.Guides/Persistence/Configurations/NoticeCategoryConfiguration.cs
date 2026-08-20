using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Intelligence;
using Diaspora.Modules.Guides.Domain.Policies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Guides.Persistence.Configurations;

/// <summary>
/// Maps the notice category catalogue and its translations.
/// <para>
/// <b>The unique index on the key is the contract.</b> The key is what a seed declares, what a URL
/// carries, what every notice is filed under and what a member's notification preference stores; two
/// rows claiming one key would strand every notice between them, so the database refuses the second
/// rather than trusting a handler to have checked.
/// </para>
/// <para>
/// The localization collection declares its own composite key for the reason every owned collection
/// on this platform does — see <see cref="OfficialNoticeConfiguration"/> for the failure mode.
/// </para>
/// </summary>
public sealed class NoticeCategoryConfiguration : IEntityTypeConfiguration<NoticeCategory>
{
    public void Configure(EntityTypeBuilder<NoticeCategory> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            IntelligencePersistenceConstants.NoticeCategoriesTable, GuidesModuleMetadata.Schema);

        builder.HasKey(category => category.Id);

        builder.Property(category => category.Id)
            .HasConversion(GuideValueConverters.Id<NoticeCategoryId>());

        builder.Property(category => category.Key)
            .HasMaxLength(IntelligenceLimits.MaxCategoryKey)
            .IsRequired();

        builder.Property(category => category.Name)
            .HasMaxLength(IntelligenceLimits.MaxCategoryName)
            .IsRequired();

        builder.Property(category => category.Description)
            .HasMaxLength(IntelligenceLimits.MaxCategoryDescription);

        builder.Property(category => category.Icon)
            .HasMaxLength(KnowledgeLimits.MaxIcon);

        builder.Property(category => category.DefaultSeverity)
            .HasConversion<string>()
            .HasMaxLength(IntelligencePersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(category => category.AlertFamily)
            .HasConversion<string>()
            .HasMaxLength(IntelligencePersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(category => category.Revision).IsConcurrencyToken();

        builder.HasIndex(category => category.Key)
            .IsUnique()
            .HasDatabaseName("ux_intel_notice_categories_key");

        // The catalogue is always read in order, and always in full.
        builder.HasIndex(category => category.Order)
            .HasDatabaseName("ix_intel_notice_categories_order");

        ConfigureLocalizations(builder);

        builder.Ignore(category => category.DomainEvents);
        builder.Ignore(category => category.IsActive);
    }

    private static void ConfigureLocalizations(EntityTypeBuilder<NoticeCategory> builder) =>
        builder.OwnsMany(category => category.Localizations, localization =>
        {
            localization.ToTable(
                IntelligencePersistenceConstants.NoticeCategoryLocalizationsTable,
                GuidesModuleMetadata.Schema);

            localization.Property(value => value.Locale)
                .HasMaxLength(KnowledgeLimits.MaxLocale)
                .IsRequired();

            localization.Property(value => value.Name)
                .HasMaxLength(IntelligenceLimits.MaxCategoryName)
                .IsRequired();

            localization.Property(value => value.Description)
                .HasMaxLength(IntelligenceLimits.MaxCategoryDescription);

            localization.WithOwner().HasForeignKey("NoticeCategoryId");

            localization.HasKey("NoticeCategoryId", nameof(NoticeCategoryLocalization.Locale));
        });
}
