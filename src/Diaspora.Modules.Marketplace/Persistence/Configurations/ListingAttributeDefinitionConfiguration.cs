using Diaspora.Modules.Marketplace.Domain.Attributes;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.Modules.Marketplace.Domain.Policies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Marketplace.Persistence.Configurations;

/// <summary>
/// Maps the category attribute catalogue and its choices — two tables.
/// <para>
/// <b>Unique on (key, category path), not on key alone.</b> "Size" means something different under
/// clothing and under furniture, and a platform that allowed only one definition per key would have
/// to invent <c>clothing-size</c> and <c>furniture-size</c> — which is the schema leaking into the
/// vocabulary sellers read.
/// </para>
/// </summary>
public sealed class ListingAttributeDefinitionConfiguration
    : IEntityTypeConfiguration<ListingAttributeDefinition>
{
    public void Configure(EntityTypeBuilder<ListingAttributeDefinition> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            MarketplacePersistenceConstants.AttributeDefinitionsTable,
            MarketplaceModuleMetadata.Schema);

        builder.HasKey(definition => definition.Id);

        builder.Property(definition => definition.Id)
            .HasConversion(MarketplaceValueConverters.Id<ListingAttributeDefinitionId>());

        builder.Property(definition => definition.Key)
            .HasMaxLength(MarketplaceLimits.MaxAttributeKey)
            .IsRequired();

        builder.Property(definition => definition.Label)
            .HasMaxLength(MarketplaceLimits.MaxDisplayName)
            .IsRequired();

        builder.Property(definition => definition.CategoryPath)
            .HasMaxLength(MarketplaceLimits.MaxAreaPath)
            .IsRequired();

        builder.Property(definition => definition.ValueKind)
            .HasConversion<string>()
            .HasMaxLength(MarketplacePersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(definition => definition.Unit)
            .HasMaxLength(MarketplaceLimits.MaxCategoryKey);

        builder.HasIndex(definition => new { definition.Key, definition.CategoryPath })
            .IsUnique()
            .HasDatabaseName("ux_marketplace_attribute_definitions_key_path");

        // The read the create form and the publish check both make: every definition scoped to a
        // category or to any ancestor of it.
        builder.HasIndex(definition => definition.CategoryPath)
            .HasDatabaseName("ix_marketplace_attribute_definitions_path");

        ConfigureChoices(builder);

        builder.Ignore(definition => definition.DomainEvents);
    }

    private static void ConfigureChoices(
        EntityTypeBuilder<ListingAttributeDefinition> builder) =>
        builder.OwnsMany(definition => definition.Choices, choice =>
        {
            choice.ToTable(
                MarketplacePersistenceConstants.AttributeChoicesTable,
                MarketplaceModuleMetadata.Schema);

            // (definition, value) is the identity: a definition cannot offer the same option twice.
            choice.WithOwner().HasForeignKey("definition_id");
            choice.HasKey("definition_id", nameof(AttributeChoice.Value));

            choice.Property(value => value.Value)
                .HasMaxLength(MarketplaceLimits.MaxAttributeValue)
                .IsRequired();

            choice.Property(value => value.Label)
                .HasMaxLength(MarketplaceLimits.MaxDisplayName)
                .IsRequired();

            choice.Property(value => value.Order).IsRequired();
        });
}
