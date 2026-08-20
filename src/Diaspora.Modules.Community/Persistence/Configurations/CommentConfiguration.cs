using Diaspora.Modules.Community.Domain.Comments;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Community.Persistence.Configurations;

/// <summary>
/// Maps comments.
/// <para>
/// The materialised path carries its own index, because a prefix scan on it <em>is</em> the
/// subtree query. Without the index the design costs a table scan per expansion, which is worse
/// than the parent-pointer walk it replaced.
/// </para>
/// </summary>
public sealed class CommentConfiguration : IEntityTypeConfiguration<Comment>
{
    public void Configure(EntityTypeBuilder<Comment> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(CommunityPersistenceConstants.CommentsTable, CommunityModuleMetadata.Schema);

        builder.HasKey(comment => comment.Id);

        builder.Property(comment => comment.Id)
            .HasConversion(CommunityValueConverters.Id<CommentId>());

        builder.Property(comment => comment.AuthorId)
            .HasConversion(CommunityValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(comment => comment.ParentCommentId)
            .HasConversion(CommunityValueConverters.Id<CommentId>());

        builder.Property(comment => comment.QuotedCommentId)
            .HasConversion(CommunityValueConverters.Id<CommentId>());

        builder.Property(comment => comment.AttachmentMediaId)
            .HasConversion(CommunityValueConverters.Id<MediaId>());

        builder.Property(comment => comment.Path)
            .HasMaxLength(CommunityPersistenceConstants.CommentPathLength)
            .IsRequired();

        builder.Property(comment => comment.Status)
            .HasConversion<string>()
            .HasMaxLength(CommunityPersistenceConstants.EnumLength)
            .IsRequired();

        builder.OwnsOne(comment => comment.Body, body =>
        {
            body.Property(value => value.Value)
                .HasColumnName("body")
                .HasMaxLength(CommentBody.MaxLength)
                .IsRequired();
        });

        builder.Navigation(comment => comment.Body).IsRequired();

        builder.OwnsOne(comment => comment.Content, content =>
        {
            content.Property(value => value.Type)
                .HasColumnName("content_type")
                .HasConversion<string>()
                .HasMaxLength(CommunityPersistenceConstants.EnumLength)
                .IsRequired();

            content.Property(value => value.Id).HasColumnName("content_id").IsRequired();

            content.HasIndex(value => new { value.Type, value.Id })
                .HasDatabaseName("ix_comments_content");

            content.Ignore(value => value.Key);
        });

        builder.Navigation(comment => comment.Content).IsRequired();

        builder.OwnsMany(comment => comment.Mentions, mention =>
        {
            mention.ToTable(
                CommunityPersistenceConstants.CommentMentionsTable, CommunityModuleMetadata.Schema);

            mention.HasKey(value => value.Id);

            mention.Property(value => value.Id)
                .HasConversion(CommunityValueConverters.Id<MentionId>());

            mention.OwnsOne(value => value.Target, target =>
            {
                target.Property(inner => inner.Type)
                    .HasColumnName("target_type")
                    .HasConversion<string>()
                    .HasMaxLength(CommunityPersistenceConstants.EnumLength)
                    .IsRequired();

                target.Property(inner => inner.Handle)
                    .HasColumnName("handle")
                    .HasMaxLength(CommunityPersistenceConstants.HandleLength)
                    .IsRequired();

                target.Property(inner => inner.TargetId).HasColumnName("target_id");

                target.Property(inner => inner.DisplayName)
                    .HasColumnName("display_name")
                    .HasMaxLength(CommunityPersistenceConstants.NameLength);
            });

            mention.Navigation(value => value.Target).IsRequired();

            mention.WithOwner().HasForeignKey("CommentId");
            mention.Ignore(value => value.DomainEvents);
        });

        builder.HasIndex(comment => comment.Path).HasDatabaseName("ix_comments_path");

        builder.HasIndex(comment => comment.ParentCommentId)
            .HasDatabaseName("ix_comments_parent");

        builder.HasIndex(comment => comment.AuthorId).HasDatabaseName("ix_comments_author");

        builder.Ignore(comment => comment.DomainEvents);
        builder.Ignore(comment => comment.Reference);
        builder.Ignore(comment => comment.IsTombstoned);
        builder.Ignore(comment => comment.IsReadableByOthers);
    }
}
