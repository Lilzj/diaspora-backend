using Diaspora.Modules.CommunityLife.Domain.Catalog;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Policies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.CommunityLife.Persistence.Configurations;

/// <summary>
/// Maps the organization-type catalogue.
/// <para>
/// Reference data, so not soft-deletable: a type is retired with a flag rather than removed,
/// because organizations store its key and deleting the row would leave pages pointing at nothing.
/// </para>
/// </summary>
public sealed class OrganizationTypeConfiguration : IEntityTypeConfiguration<OrganizationType>
{
    public void Configure(EntityTypeBuilder<OrganizationType> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            CommunityLifePersistenceConstants.OrganizationTypesTable,
            CommunityLifeModuleMetadata.Schema);

        builder.HasKey(type => type.Id);

        builder.Property(type => type.Id)
            .HasConversion(CommunityLifeValueConverters.Id<OrganizationTypeId>());

        builder.Property(type => type.Key)
            .HasMaxLength(CommunityLifeLimits.MaxCategoryKey)
            .IsRequired();

        builder.Property(type => type.Name)
            .HasMaxLength(CommunityLifeLimits.MaxDisplayName)
            .IsRequired();

        builder.Property(type => type.Description)
            .HasMaxLength(CommunityLifeLimits.MaxNoteLength);

        builder.Property(type => type.IconName)
            .HasMaxLength(CommunityLifeLimits.MaxDisplayName);

        builder.Property(type => type.GroupName)
            .HasMaxLength(CommunityLifeLimits.MaxDisplayName);

        // The seeder is idempotent by contract, and this is what makes that enforceable rather than
        // merely intended: a second startup inserting a duplicate key fails loudly here instead of
        // producing two pickers' worth of the same entry.
        builder.HasIndex(type => type.Key)
            .IsUnique()
            .HasDatabaseName("ix_community_life_organization_types_key");

        builder.HasIndex(type => new { type.IsActive, type.Order })
            .HasDatabaseName("ix_community_life_organization_types_picker");

        builder.Ignore(type => type.DomainEvents);
    }
}
