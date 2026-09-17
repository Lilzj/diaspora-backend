using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.Posts;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Community.Persistence.Configurations;

/// <summary>
/// Maps posts and everything they own.
/// <para>
/// The location context is a <b>required</b> owned reference. Every one of its properties is
/// nullable, and an <em>optional</em> owned reference in that shape is the EF Core model that
/// cannot distinguish an absent instance from a present-but-empty one and fails at model-build
/// time. Declaring the navigation required is what makes an all-null location legal.
/// </para>
/// <para>
/// The indexes are chosen from the feed queries rather than from the columns. Every feed filters
/// on status and orders by publication, so that pair is the primary one; country and area carry
/// their own because the country and neighbourhood feeds are the two that run on every home
/// screen.
/// </para>
/// </summary>
public sealed class CommunityPostConfiguration : IEntityTypeConfiguration<CommunityPost>
{
    public void Configure(EntityTypeBuilder<CommunityPost> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(CommunityPersistenceConstants.PostsTable, CommunityModuleMetadata.Schema);

        builder.HasKey(post => post.Id);

        builder.Property(post => post.Id)
            .HasConversion(CommunityValueConverters.Id<CommunityPostId>());

        builder.Property(post => post.AuthorId)
            .HasConversion(CommunityValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(post => post.Title)
            .HasMaxLength(Domain.ValueObjects.PostTitle.MaxLength);

        builder.Property(post => post.Category)
            .HasConversion<string>()
            .HasMaxLength(CommunityPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(post => post.Urgency)
            .HasConversion<string>()
            .HasMaxLength(CommunityPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(post => post.Status)
            .HasConversion<string>()
            .HasMaxLength(CommunityPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(post => post.ContentType)
            .HasConversion<string>()
            .HasMaxLength(CommunityPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(post => post.VerificationStatus)
            .HasConversion<string>()
            .HasMaxLength(CommunityPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(post => post.PinnedCommentId)
            .HasConversion(CommunityValueConverters.Id<CommentId>());

        builder.OwnsOne(post => post.Body, body =>
        {
            body.Property(value => value.Value)
                .HasColumnName("body")
                .HasMaxLength(Domain.ValueObjects.PostBody.MaxLongFormLength)
                .IsRequired();
        });

        builder.Navigation(post => post.Body).IsRequired();

        builder.OwnsOne(post => post.Publisher, publisher =>
        {
            publisher.Property(value => value.Type)
                .HasColumnName("publisher_type")
                .HasConversion<string>()
                .HasMaxLength(CommunityPersistenceConstants.EnumLength)
                .IsRequired();

            publisher.Property(value => value.OrganizationId)
                .HasColumnName("publisher_organization_id");

            publisher.Property(value => value.DisplayName)
                .HasColumnName("publisher_display_name")
                .HasMaxLength(CommunityPersistenceConstants.NameLength);
        });

        builder.Navigation(post => post.Publisher).IsRequired();

        builder.OwnsOne(post => post.Audience, audience =>
        {
            audience.Property(value => value.Level)
                .HasColumnName("visibility_level")
                .HasConversion<string>()
                .HasMaxLength(CommunityPersistenceConstants.EnumLength)
                .IsRequired();

            audience.Property(value => value.CountryCode)
                .HasColumnName("audience_country_code")
                .HasMaxLength(CommunityPersistenceConstants.CountryCodeLength);

            audience.Property(value => value.AreaId).HasColumnName("audience_area_id");

            audience.Property(value => value.RuleId)
                .HasColumnName("audience_rule_id")
                .HasConversion(CommunityValueConverters.Id<VisibilityRuleId>());
        });

        builder.Navigation(post => post.Audience).IsRequired();

        builder.OwnsOne(post => post.Location, location =>
        {
            location.Property(value => value.CountryCode)
                .HasColumnName("country_code")
                .HasMaxLength(CommunityPersistenceConstants.CountryCodeLength);

            location.Property(value => value.AreaId).HasColumnName("area_id");

            location.Property(value => value.AreaName)
                .HasColumnName("area_name")
                .HasMaxLength(CommunityPersistenceConstants.NameLength);

            location.Property(value => value.CityName)
                .HasColumnName("city_name")
                .HasMaxLength(CommunityPersistenceConstants.NameLength);

            location.Property(value => value.Latitude).HasColumnName("latitude");
            location.Property(value => value.Longitude).HasColumnName("longitude");
            location.Property(value => value.PointIsExact).HasColumnName("point_is_exact");

            location.Property(value => value.HierarchyPath)
                .HasColumnName("hierarchy_path")
                .HasMaxLength(CommunityPersistenceConstants.PathLength);

            location.HasIndex(value => value.CountryCode)
                .HasDatabaseName("ix_community_posts_country");

            location.HasIndex(value => value.HierarchyPath)
                .HasDatabaseName("ix_community_posts_path");
        });

        builder.Navigation(post => post.Location).IsRequired();

        builder.OwnsOne(post => post.Counters, counters =>
        {
            counters.Property(value => value.ViewCount).HasColumnName("view_count");
            counters.Property(value => value.CommentCount).HasColumnName("comment_count");
            counters.Property(value => value.ReactionCount).HasColumnName("reaction_count");
            counters.Property(value => value.ShareCount).HasColumnName("share_count");
            counters.Property(value => value.BookmarkCount).HasColumnName("bookmark_count");
            counters.Property(value => value.ReportCount).HasColumnName("report_count");
        });

        builder.Navigation(post => post.Counters).IsRequired();

        ConfigureAttachments(builder);
        ConfigureHashtags(builder);
        ConfigureMentions(builder);
        ConfigureRevisions(builder);
        ConfigurePoll(builder);

        // The pair every feed filters and orders on.
        builder.HasIndex(post => new { post.Status, post.PublishedAt })
            .HasDatabaseName("ix_community_posts_status_published");

        builder.HasIndex(post => post.AuthorId)
            .HasDatabaseName("ix_community_posts_author");

        builder.HasIndex(post => post.ExpiresAt)
            .HasDatabaseName("ix_community_posts_expires");

        builder.Ignore(post => post.DomainEvents);
        builder.Ignore(post => post.Reference);
        builder.Ignore(post => post.IsPublished);
        builder.Ignore(post => post.IsVisibleToOthers);
    }

    private static void ConfigureAttachments(EntityTypeBuilder<CommunityPost> builder) =>
        builder.OwnsMany(post => post.Attachments, attachment =>
        {
            attachment.ToTable(
                CommunityPersistenceConstants.PostAttachmentsTable, CommunityModuleMetadata.Schema);

            attachment.HasKey(value => value.Id);

            attachment.Property(value => value.Id)
                .HasConversion(CommunityValueConverters.Id<CommunityAttachmentId>());

            attachment.Property(value => value.Kind)
                .HasConversion<string>()
                .HasMaxLength(CommunityPersistenceConstants.EnumLength)
                .IsRequired();

            attachment.Property(value => value.MediaId)
                .HasConversion(CommunityValueConverters.Id<MediaId>());

            attachment.Property(value => value.Role)
                .HasMaxLength(CommunityPersistenceConstants.SlugLength)
                .IsRequired();

            attachment.Property(value => value.Caption).HasMaxLength(1_000);
            attachment.Property(value => value.LinkUrl).HasMaxLength(2_048);
            attachment.Property(value => value.LinkTitle).HasMaxLength(300);
            attachment.Property(value => value.LinkDescription).HasMaxLength(1_000);
            attachment.Property(value => value.LinkSiteName).HasMaxLength(300);

            attachment.WithOwner().HasForeignKey("PostId");
            attachment.Ignore(value => value.DomainEvents);
            attachment.Ignore(value => value.Preview);
        });

    private static void ConfigureHashtags(EntityTypeBuilder<CommunityPost> builder) =>
        builder.OwnsMany(post => post.Hashtags, hashtag =>
        {
            hashtag.ToTable(
                CommunityPersistenceConstants.PostHashtagsTable, CommunityModuleMetadata.Schema);

            hashtag.HasKey(value => value.Id);

            hashtag.Property(value => value.Id)
                .HasConversion(CommunityValueConverters.Id<PostHashtagId>());

            hashtag.Property(value => value.HashtagId)
                .HasConversion(CommunityValueConverters.Id<HashtagId>())
                .IsRequired();

            hashtag.Property(value => value.Slug)
                .HasMaxLength(CommunityPersistenceConstants.SlugLength)
                .IsRequired();

            hashtag.WithOwner().HasForeignKey("PostId");

            // The hashtag feed's whole query.
            hashtag.HasIndex(value => value.Slug).HasDatabaseName("ix_post_hashtags_slug");

            hashtag.Ignore(value => value.DomainEvents);
        });

    private static void ConfigureMentions(EntityTypeBuilder<CommunityPost> builder) =>
        builder.OwnsMany(post => post.Mentions, mention =>
        {
            mention.ToTable(
                CommunityPersistenceConstants.PostMentionsTable, CommunityModuleMetadata.Schema);

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

            mention.WithOwner().HasForeignKey("PostId");
            mention.Ignore(value => value.DomainEvents);
        });

    private static void ConfigureRevisions(EntityTypeBuilder<CommunityPost> builder) =>
        builder.OwnsMany(post => post.Revisions, revision =>
        {
            revision.ToTable(
                CommunityPersistenceConstants.PostRevisionsTable, CommunityModuleMetadata.Schema);

            revision.HasKey(value => value.Id);

            revision.Property(value => value.Id)
                .HasConversion(CommunityValueConverters.Id<PostRevisionId>());

            revision.Property(value => value.PostId)
                .HasConversion(CommunityValueConverters.Id<CommunityPostId>())
                .IsRequired();

            revision.Property(value => value.EditedBy)
                .HasConversion(CommunityValueConverters.Id<UserId>())
                .IsRequired();

            revision.Property(value => value.Title)
                .HasMaxLength(Domain.ValueObjects.PostTitle.MaxLength);

            revision.Property(value => value.Body)
                .HasMaxLength(Domain.ValueObjects.PostBody.MaxLongFormLength)
                .IsRequired();

            revision.Property(value => value.ChangeSummary).HasMaxLength(1_000);

            revision.WithOwner().HasForeignKey(value => value.PostId);

            revision.HasIndex(value => new { value.PostId, value.RevisionNumber })
                .IsUnique()
                .HasDatabaseName("ix_post_revisions_post_number");

            revision.Ignore(value => value.DomainEvents);
        });

    private static void ConfigurePoll(EntityTypeBuilder<CommunityPost> builder) =>
        builder.OwnsOne(post => post.Poll, poll =>
        {
            poll.ToTable(CommunityPersistenceConstants.PollsTable, CommunityModuleMetadata.Schema);

            poll.HasKey(value => value.Id);

            poll.Property(value => value.Id)
                .HasConversion(CommunityValueConverters.Id<PollId>());

            poll.Property(value => value.PostId)
                .HasConversion(CommunityValueConverters.Id<CommunityPostId>())
                .IsRequired();

            poll.Property(value => value.Question)
                .HasMaxLength(Domain.Polls.Poll.MaxQuestionLength)
                .IsRequired();

            poll.Property(value => value.ResultVisibility)
                .HasConversion<string>()
                .HasMaxLength(CommunityPersistenceConstants.EnumLength)
                .IsRequired();

            poll.Property(value => value.Status)
                .HasConversion<string>()
                .HasMaxLength(CommunityPersistenceConstants.EnumLength)
                .IsRequired();

            poll.WithOwner().HasForeignKey(value => value.PostId);

            poll.OwnsMany(value => value.Options, option =>
            {
                option.ToTable(
                    CommunityPersistenceConstants.PollOptionsTable, CommunityModuleMetadata.Schema);

                option.HasKey(inner => inner.Id);

                option.Property(inner => inner.Id)
                    .HasConversion(CommunityValueConverters.Id<PollOptionId>());

                option.Property(inner => inner.PollId)
                    .HasConversion(CommunityValueConverters.Id<PollId>())
                    .IsRequired();

                option.Property(inner => inner.Label)
                    .HasMaxLength(Domain.Polls.PollOption.MaxLabelLength)
                    .IsRequired();

                option.Property(inner => inner.ImageMediaId)
                    .HasConversion(CommunityValueConverters.Id<MediaId>());

                option.WithOwner().HasForeignKey(inner => inner.PollId);
                option.Ignore(inner => inner.DomainEvents);
            });

            poll.Ignore(value => value.DomainEvents);
        });
}
