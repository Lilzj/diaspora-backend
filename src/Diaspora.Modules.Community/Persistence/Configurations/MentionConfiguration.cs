using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.Mentions;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Community.Persistence.Configurations;

/// <summary>Maps mention records. Indexed by target, which is the only query that matters.</summary>
public sealed class MentionConfiguration : IEntityTypeConfiguration<Mention>
{
    public void Configure(EntityTypeBuilder<Mention> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            CommunityPersistenceConstants.MentionsTable, CommunityModuleMetadata.Schema);

        builder.HasKey(entity => entity.Id);

        builder.Property(entity => entity.Id)
            .HasConversion(CommunityValueConverters.Id<MentionId>());

        builder.Property(entity => entity.MentionedBy)
            .HasConversion(CommunityValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(entity => entity.TargetType)
            .HasConversion<string>()
            .HasMaxLength(CommunityPersistenceConstants.EnumLength)
            .IsRequired();

        builder.OwnsOne(entity => entity.Content, content =>
        {
            content.Property(value => value.Type)
                .HasColumnName("content_type")
                .HasConversion<string>()
                .HasMaxLength(CommunityPersistenceConstants.EnumLength)
                .IsRequired();

            content.Property(value => value.Id).HasColumnName("content_id").IsRequired();

            content.HasIndex(value => new { value.Type, value.Id })
                .HasDatabaseName("ix_mentions_content");

            content.Ignore(value => value.Key);
        });

        builder.Navigation(entity => entity.Content).IsRequired();
        builder.HasIndex(entity => new { entity.TargetType, entity.TargetId, entity.MentionedAt })
            .HasDatabaseName("ix_mentions_target");

        builder.Ignore(entity => entity.DomainEvents);
    }
}
