using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.Reactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Community.Persistence.Configurations;

/// <summary>Maps the reaction catalogue. The key is unique because it is a wire contract.</summary>
public sealed class ReactionDefinitionConfiguration : IEntityTypeConfiguration<ReactionDefinition>
{
    public void Configure(EntityTypeBuilder<ReactionDefinition> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            CommunityPersistenceConstants.ReactionDefinitionsTable, CommunityModuleMetadata.Schema);

        builder.HasKey(definition => definition.Id);

        builder.Property(definition => definition.Id)
            .HasConversion(CommunityValueConverters.Id<ReactionDefinitionId>());

        builder.Property(definition => definition.Key)
            .HasMaxLength(ReactionDefinition.MaxKeyLength)
            .IsRequired();

        builder.Property(definition => definition.Label)
            .HasMaxLength(CommunityPersistenceConstants.NameLength)
            .IsRequired();

        builder.Property(definition => definition.Glyph).HasMaxLength(16);

        builder.Property(definition => definition.Sentiment)
            .HasConversion<string>()
            .HasMaxLength(CommunityPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(definition => definition.AppliesTo)
            .HasMaxLength(CommunityPersistenceConstants.NameLength)
            .IsRequired();

        builder.HasIndex(definition => definition.Key)
            .IsUnique()
            .HasDatabaseName("ix_reaction_definitions_key");

        builder.Ignore(definition => definition.DomainEvents);
        builder.Ignore(definition => definition.IsCorroborating);
        builder.Ignore(definition => definition.IsDisputing);
    }
}
