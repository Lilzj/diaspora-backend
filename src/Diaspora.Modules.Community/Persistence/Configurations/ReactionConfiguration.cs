using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.Reactions;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Community.Persistence.Configurations;

/// <summary>
/// Maps reaction instances.
/// <para>
/// The unique index across member, content and kind is what actually enforces one reaction per
/// member per kind. A handler check alone loses the race a double tap on a slow connection
/// creates, and that race is the single commonest way a reaction count drifts.
/// </para>
/// </summary>
public sealed class ReactionConfiguration : IEntityTypeConfiguration<Reaction>
{
    public void Configure(EntityTypeBuilder<Reaction> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            CommunityPersistenceConstants.ReactionsTable, CommunityModuleMetadata.Schema);

        builder.HasKey(reaction => reaction.Id);

        builder.Property(reaction => reaction.Id)
            .HasConversion(CommunityValueConverters.Id<ReactionId>());

        builder.Property(reaction => reaction.UserId)
            .HasConversion(CommunityValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(reaction => reaction.ReactionKey)
            .HasMaxLength(ReactionDefinition.MaxKeyLength)
            .IsRequired();

        builder.Property(reaction => reaction.UniquenessKey)
            .HasMaxLength(CommunityPersistenceConstants.CompositeKeyLength)
            .IsRequired();

        builder.Property(reaction => reaction.Sentiment)
            .HasConversion<string>()
            .HasMaxLength(CommunityPersistenceConstants.EnumLength)
            .IsRequired();

        builder.OwnsOne(reaction => reaction.Content, content =>
        {
            content.Property(value => value.Type)
                .HasColumnName("content_type")
                .HasConversion<string>()
                .HasMaxLength(CommunityPersistenceConstants.EnumLength)
                .IsRequired();

            content.Property(value => value.Id).HasColumnName("content_id").IsRequired();

            content.HasIndex(value => new { value.Type, value.Id })
                .HasDatabaseName("ix_reactions_content");

            content.Ignore(value => value.Key);
        });

        builder.Navigation(reaction => reaction.Content).IsRequired();

        // One member, one content item, one kind. On the denormalised column, because an index
        // cannot span an owned type's columns and the aggregate's own.
        builder.HasIndex(reaction => reaction.UniquenessKey)
            .IsUnique()
            .HasDatabaseName("ix_reactions_user_content_kind");

        builder.Ignore(reaction => reaction.DomainEvents);
    }
}
