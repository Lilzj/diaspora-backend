using Diaspora.Modules.CommunityLife.Domain.Catalog;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Policies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.CommunityLife.Persistence.Configurations;

/// <summary>
/// Maps the event-category catalogue.
/// <para>
/// <b>The facets column is indexed and that is what makes the brief's search requirements
/// answerable.</b> "Religious events", "business events" and "cultural events" are groupings across
/// categories, and resolving one to its category keys has to be a single indexed read or every
/// faceted browse pays for a scan of the catalogue.
/// </para>
/// </summary>
public sealed class EventCategoryConfiguration : IEntityTypeConfiguration<EventCategory>
{
    public void Configure(EntityTypeBuilder<EventCategory> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            CommunityLifePersistenceConstants.EventCategoriesTable,
            CommunityLifeModuleMetadata.Schema);

        builder.HasKey(category => category.Id);

        builder.Property(category => category.Id)
            .HasConversion(CommunityLifeValueConverters.Id<EventCategoryId>());

        builder.Property(category => category.Key)
            .HasMaxLength(CommunityLifeLimits.MaxCategoryKey)
            .IsRequired();

        builder.Property(category => category.Name)
            .HasMaxLength(CommunityLifeLimits.MaxDisplayName)
            .IsRequired();

        builder.Property(category => category.Description)
            .HasMaxLength(CommunityLifeLimits.MaxNoteLength);

        builder.Property(category => category.IconName)
            .HasMaxLength(CommunityLifeLimits.MaxDisplayName);

        builder.Property(category => category.GroupName)
            .HasMaxLength(CommunityLifeLimits.MaxDisplayName);

        builder.Property(category => category.Facets)
            .HasMaxLength(CommunityLifePersistenceConstants.FacetsLength)
            .IsRequired();

        builder.HasIndex(category => category.Key)
            .IsUnique()
            .HasDatabaseName("ix_community_life_event_categories_key");

        builder.HasIndex(category => new { category.IsActive, category.Order })
            .HasDatabaseName("ix_community_life_event_categories_picker");

        builder.HasIndex(category => category.Facets)
            .HasDatabaseName("ix_community_life_event_categories_facets");

        builder.Ignore(category => category.DomainEvents);
    }
}
