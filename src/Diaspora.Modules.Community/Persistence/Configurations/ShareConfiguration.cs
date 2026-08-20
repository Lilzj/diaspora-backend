using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.Shares;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Community.Persistence.Configurations;

/// <summary>Maps shares. Deliberately no unique index: a share is an act, not a state.</summary>
public sealed class ShareConfiguration : IEntityTypeConfiguration<Share>
{
    public void Configure(EntityTypeBuilder<Share> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(CommunityPersistenceConstants.SharesTable, CommunityModuleMetadata.Schema);

        builder.HasKey(entity => entity.Id);

        builder.Property(entity => entity.Id)
            .HasConversion(CommunityValueConverters.Id<ShareId>());

        builder.Property(entity => entity.SharedBy)
            .HasConversion(CommunityValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(entity => entity.QuotePostId)
            .HasConversion(CommunityValueConverters.Id<CommunityPostId>());

        builder.Property(entity => entity.Message).HasMaxLength(1_000);

        builder.OwnsOne(entity => entity.Target, target =>
        {
            target.Property(value => value.Kind)
                .HasColumnName("kind")
                .HasConversion<string>()
                .HasMaxLength(CommunityPersistenceConstants.EnumLength)
                .IsRequired();

            target.Property(value => value.Destination)
                .HasColumnName("destination")
                .HasMaxLength(64);

            target.Property(value => value.RecipientId).HasColumnName("recipient_id");
        });

        builder.Navigation(entity => entity.Target).IsRequired();

        builder.OwnsOne(entity => entity.Content, content =>
        {
            content.Property(value => value.Type)
                .HasColumnName("content_type")
                .HasConversion<string>()
                .HasMaxLength(CommunityPersistenceConstants.EnumLength)
                .IsRequired();

            content.Property(value => value.Id).HasColumnName("content_id").IsRequired();

            content.HasIndex(value => new { value.Type, value.Id })
                .HasDatabaseName("ix_shares_content");

            content.Ignore(value => value.Key);
        });

        builder.Navigation(entity => entity.Content).IsRequired();
        builder.Ignore(entity => entity.DomainEvents);
    }
}
