using Diaspora.Modules.Guides.Domain.Categories;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Policies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Guides.Persistence.Configurations;

/// <summary>
/// Maps the guide catalogue.
/// <para>
/// <b>The unique index on the key is the contract.</b> The key is what a contributor seed
/// declares, what a URL carries and what every guide row stores; two categories claiming the
/// same key would strand every guide holding it between them, so the database refuses the
/// second rather than trusting a handler to have checked.
/// </para>
/// </summary>
public sealed class GuideCategoryConfiguration : IEntityTypeConfiguration<GuideCategory>
{
    public void Configure(EntityTypeBuilder<GuideCategory> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            GuidePersistenceConstants.GuideCategoriesTable, GuidesModuleMetadata.Schema);

        builder.HasKey(category => category.Id);

        builder.Property(category => category.Id)
            .HasConversion(GuideValueConverters.Id<GuideCategoryId>());

        builder.Property(category => category.Key)
            .HasMaxLength(GuideLimits.MaxCategoryKey)
            .IsRequired();

        builder.Property(category => category.Name)
            .HasMaxLength(GuideLimits.MaxCategoryName)
            .IsRequired();

        builder.Property(category => category.Description)
            .HasMaxLength(GuideLimits.MaxCategoryDescription);

        builder.Property(category => category.Icon)
            .HasMaxLength(GuideLimits.MaxCategoryName);

        builder.Property(category => category.ParentKey)
            .HasMaxLength(GuideLimits.MaxCategoryKey);

        builder.HasIndex(category => category.Key)
            .IsUnique()
            .HasDatabaseName("ux_guide_categories_key");

        builder.Ignore(category => category.DomainEvents);
    }
}
