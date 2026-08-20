using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diaspora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CommunityModuleInitial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "community");

            migrationBuilder.CreateTable(
                name: "comments",
                schema: "community",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    content_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    content_id = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ParentCommentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Path = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Depth = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IsPinned = table.Column<bool>(type: "boolean", nullable: false),
                    IsEdited = table.Column<bool>(type: "boolean", nullable: false),
                    ReplyCount = table.Column<int>(type: "integer", nullable: false),
                    ReactionCount = table.Column<int>(type: "integer", nullable: false),
                    LikeCount = table.Column<int>(type: "integer", nullable: false),
                    ReportCount = table.Column<int>(type: "integer", nullable: false),
                    QuotedCommentId = table.Column<Guid>(type: "uuid", nullable: true),
                    AttachmentMediaId = table.Column<Guid>(type: "uuid", nullable: true),
                    EditedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_comments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "community_bookmark_folders",
                schema: "community",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Glyph = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    ItemCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_bookmark_folders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "community_bookmarks",
                schema: "community",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    content_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    content_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentKey = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                    FolderId = table.Column<Guid>(type: "uuid", nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SavedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_bookmarks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "community_moderation_decisions",
                schema: "community",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    content_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    content_id = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ModeratorId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    PreviousStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    NewStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ReportId = table.Column<Guid>(type: "uuid", nullable: true),
                    ContentAuthorId = table.Column<Guid>(type: "uuid", nullable: true),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_moderation_decisions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "community_posts",
                schema: "community",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    publisher_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    publisher_organization_id = table.Column<Guid>(type: "uuid", nullable: true),
                    publisher_display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    body = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Category = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Urgency = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    visibility_level = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    audience_country_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    audience_area_id = table.Column<Guid>(type: "uuid", nullable: true),
                    audience_rule_id = table.Column<Guid>(type: "uuid", nullable: true),
                    country_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    area_id = table.Column<Guid>(type: "uuid", nullable: true),
                    area_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    city_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    latitude = table.Column<double>(type: "double precision", nullable: true),
                    longitude = table.Column<double>(type: "double precision", nullable: true),
                    hierarchy_path = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    view_count = table.Column<int>(type: "integer", nullable: false),
                    comment_count = table.Column<int>(type: "integer", nullable: false),
                    reaction_count = table.Column<int>(type: "integer", nullable: false),
                    share_count = table.Column<int>(type: "integer", nullable: false),
                    bookmark_count = table.Column<int>(type: "integer", nullable: false),
                    report_count = table.Column<int>(type: "integer", nullable: false),
                    VerificationStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ConfirmationCount = table.Column<int>(type: "integer", nullable: false),
                    VerifiedConfirmationCount = table.Column<int>(type: "integer", nullable: false),
                    DisputeCount = table.Column<int>(type: "integer", nullable: false),
                    IsPinned = table.Column<bool>(type: "boolean", nullable: false),
                    PinnedCommentId = table.Column<Guid>(type: "uuid", nullable: true),
                    CommentsClosed = table.Column<bool>(type: "boolean", nullable: false),
                    LifetimeHours = table.Column<int>(type: "integer", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EditedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsEdited = table.Column<bool>(type: "boolean", nullable: false),
                    RevisionCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_posts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "content_reports",
                schema: "community",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    content_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    content_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReporterKey = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                    ReporterId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    EvidenceUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ResolvedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolutionNote = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ReportedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ContentAuthorId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_content_reports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "content_reviews",
                schema: "community",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    content_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    content_id = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Provider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProviderVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Confidence = table.Column<double>(type: "double precision", nullable: false),
                    Flags = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_content_reviews", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "feed_subscriptions",
                schema: "community",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FeedKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RankingKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Filters = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    NotificationsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    LastReadAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_feed_subscriptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "follows",
                schema: "community",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FollowerId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    EdgeKey = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                    IsMuted = table.Column<bool>(type: "boolean", nullable: false),
                    FollowedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_follows", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "hashtags",
                schema: "community",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DisplayText = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    UsageCount = table.Column<int>(type: "integer", nullable: false),
                    RecentUsageCount = table.Column<int>(type: "integer", nullable: false),
                    PrimaryCountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    FirstUsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastUsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hashtags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "mentions",
                schema: "community",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    content_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    content_id = table.Column<Guid>(type: "uuid", nullable: false),
                    MentionedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    MentionedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mentions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "poll_votes",
                schema: "community",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PollId = table.Column<Guid>(type: "uuid", nullable: false),
                    OptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    VoterId = table.Column<Guid>(type: "uuid", nullable: false),
                    CastAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_poll_votes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "reaction_definitions",
                schema: "community",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Glyph = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    Sentiment = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Weight = table.Column<double>(type: "double precision", nullable: false),
                    AppliesTo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    IsBuiltIn = table.Column<bool>(type: "boolean", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reaction_definitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "reaction_summaries",
                schema: "community",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    content_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    content_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentKey = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    TotalCount = table.Column<int>(type: "integer", nullable: false),
                    WeightedScore = table.Column<double>(type: "double precision", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reaction_summaries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "reactions",
                schema: "community",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    content_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    content_id = table.Column<Guid>(type: "uuid", nullable: false),
                    UniquenessKey = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReactionKey = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Sentiment = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ByVerifiedResident = table.Column<bool>(type: "boolean", nullable: false),
                    ReactedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reactions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "shares",
                schema: "community",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    content_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    content_id = table.Column<Guid>(type: "uuid", nullable: false),
                    SharedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    destination = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    recipient_id = table.Column<Guid>(type: "uuid", nullable: true),
                    QuotePostId = table.Column<Guid>(type: "uuid", nullable: true),
                    Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SharedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shares", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "visibility_rules",
                schema: "community",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    BaseLevel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IsShared = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_visibility_rules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "comment_mentions",
                schema: "community",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    handle = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: true),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    StartOffset = table.Column<int>(type: "integer", nullable: false),
                    Length = table.Column<int>(type: "integer", nullable: false),
                    CommentId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_comment_mentions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_comment_mentions_comments_CommentId",
                        column: x => x.CommentId,
                        principalSchema: "community",
                        principalTable: "comments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "polls",
                schema: "community",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PostId = table.Column<Guid>(type: "uuid", nullable: false),
                    Question = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    AllowsMultiple = table.Column<bool>(type: "boolean", nullable: false),
                    ResultVisibility = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ClosesAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TotalVotes = table.Column<int>(type: "integer", nullable: false),
                    VoterCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_polls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_polls_community_posts_PostId",
                        column: x => x.PostId,
                        principalSchema: "community",
                        principalTable: "community_posts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "post_attachments",
                schema: "community",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    MediaId = table.Column<Guid>(type: "uuid", nullable: true),
                    Role = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    Caption = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    LinkUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    LinkTitle = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    LinkDescription = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    LinkSiteName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    LinkImageMediaId = table.Column<Guid>(type: "uuid", nullable: true),
                    PostId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_post_attachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_post_attachments_community_posts_PostId",
                        column: x => x.PostId,
                        principalSchema: "community",
                        principalTable: "community_posts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "post_hashtags",
                schema: "community",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HashtagId = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    PostId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_post_hashtags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_post_hashtags_community_posts_PostId",
                        column: x => x.PostId,
                        principalSchema: "community",
                        principalTable: "community_posts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "post_mentions",
                schema: "community",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    handle = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: true),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    StartOffset = table.Column<int>(type: "integer", nullable: false),
                    Length = table.Column<int>(type: "integer", nullable: false),
                    PostId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_post_mentions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_post_mentions_community_posts_PostId",
                        column: x => x.PostId,
                        principalSchema: "community",
                        principalTable: "community_posts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "post_revisions",
                schema: "community",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PostId = table.Column<Guid>(type: "uuid", nullable: false),
                    RevisionNumber = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Body = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    EditedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ChangeSummary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_post_revisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_post_revisions_community_posts_PostId",
                        column: x => x.PostId,
                        principalSchema: "community",
                        principalTable: "community_posts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "reaction_tallies",
                schema: "community",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReactionKey = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Sentiment = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Count = table.Column<int>(type: "integer", nullable: false),
                    SummaryId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reaction_tallies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_reaction_tallies_reaction_summaries_SummaryId",
                        column: x => x.SummaryId,
                        principalSchema: "community",
                        principalTable: "reaction_summaries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "visibility_clauses",
                schema: "community",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Value = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    RuleId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_visibility_clauses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_visibility_clauses_visibility_rules_RuleId",
                        column: x => x.RuleId,
                        principalSchema: "community",
                        principalTable: "visibility_rules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "poll_options",
                schema: "community",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PollId = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    ImageMediaId = table.Column<Guid>(type: "uuid", nullable: true),
                    VoteCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_poll_options", x => x.Id);
                    table.ForeignKey(
                        name: "FK_poll_options_polls_PollId",
                        column: x => x.PollId,
                        principalSchema: "community",
                        principalTable: "polls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_comment_mentions_CommentId",
                schema: "community",
                table: "comment_mentions",
                column: "CommentId");

            migrationBuilder.CreateIndex(
                name: "ix_comments_author",
                schema: "community",
                table: "comments",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "ix_comments_content",
                schema: "community",
                table: "comments",
                columns: new[] { "content_type", "content_id" });

            migrationBuilder.CreateIndex(
                name: "ix_comments_parent",
                schema: "community",
                table: "comments",
                column: "ParentCommentId");

            migrationBuilder.CreateIndex(
                name: "ix_comments_path",
                schema: "community",
                table: "comments",
                column: "Path");

            migrationBuilder.CreateIndex(
                name: "ix_bookmark_folders_owner",
                schema: "community",
                table: "community_bookmark_folders",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "ix_bookmarks_content",
                schema: "community",
                table: "community_bookmarks",
                columns: new[] { "content_type", "content_id" });

            migrationBuilder.CreateIndex(
                name: "ix_bookmarks_user_content",
                schema: "community",
                table: "community_bookmarks",
                column: "ContentKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_bookmarks_user_saved",
                schema: "community",
                table: "community_bookmarks",
                columns: new[] { "UserId", "SavedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_moderation_decisions_content",
                schema: "community",
                table: "community_moderation_decisions",
                columns: new[] { "content_type", "content_id" });

            migrationBuilder.CreateIndex(
                name: "ix_moderation_decisions_decided",
                schema: "community",
                table: "community_moderation_decisions",
                column: "DecidedAt");

            migrationBuilder.CreateIndex(
                name: "ix_community_posts_author",
                schema: "community",
                table: "community_posts",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "ix_community_posts_country",
                schema: "community",
                table: "community_posts",
                column: "country_code");

            migrationBuilder.CreateIndex(
                name: "ix_community_posts_expires",
                schema: "community",
                table: "community_posts",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "ix_community_posts_path",
                schema: "community",
                table: "community_posts",
                column: "hierarchy_path");

            migrationBuilder.CreateIndex(
                name: "ix_community_posts_status_published",
                schema: "community",
                table: "community_posts",
                columns: new[] { "Status", "PublishedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_content_reports_content",
                schema: "community",
                table: "content_reports",
                columns: new[] { "content_type", "content_id" });

            migrationBuilder.CreateIndex(
                name: "ix_content_reports_reporter_content",
                schema: "community",
                table: "content_reports",
                column: "ReporterKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_content_reports_status",
                schema: "community",
                table: "content_reports",
                columns: new[] { "Status", "ReportedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_content_reviews_content",
                schema: "community",
                table: "content_reviews",
                columns: new[] { "content_type", "content_id" });

            migrationBuilder.CreateIndex(
                name: "ix_feed_subscriptions_user",
                schema: "community",
                table: "feed_subscriptions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "ix_follows_edge",
                schema: "community",
                table: "follows",
                column: "EdgeKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_follows_follower_type",
                schema: "community",
                table: "follows",
                columns: new[] { "FollowerId", "TargetType" });

            migrationBuilder.CreateIndex(
                name: "ix_follows_target",
                schema: "community",
                table: "follows",
                columns: new[] { "TargetType", "TargetId" });

            migrationBuilder.CreateIndex(
                name: "ix_hashtags_slug",
                schema: "community",
                table: "hashtags",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_hashtags_trending",
                schema: "community",
                table: "hashtags",
                column: "RecentUsageCount");

            migrationBuilder.CreateIndex(
                name: "ix_mentions_content",
                schema: "community",
                table: "mentions",
                columns: new[] { "content_type", "content_id" });

            migrationBuilder.CreateIndex(
                name: "ix_mentions_target",
                schema: "community",
                table: "mentions",
                columns: new[] { "TargetType", "TargetId", "MentionedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_poll_options_PollId",
                schema: "community",
                table: "poll_options",
                column: "PollId");

            migrationBuilder.CreateIndex(
                name: "ix_poll_votes_poll_option_voter",
                schema: "community",
                table: "poll_votes",
                columns: new[] { "PollId", "OptionId", "VoterId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_poll_votes_poll_voter",
                schema: "community",
                table: "poll_votes",
                columns: new[] { "PollId", "VoterId" });

            migrationBuilder.CreateIndex(
                name: "IX_polls_PostId",
                schema: "community",
                table: "polls",
                column: "PostId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_post_attachments_PostId",
                schema: "community",
                table: "post_attachments",
                column: "PostId");

            migrationBuilder.CreateIndex(
                name: "IX_post_hashtags_PostId",
                schema: "community",
                table: "post_hashtags",
                column: "PostId");

            migrationBuilder.CreateIndex(
                name: "ix_post_hashtags_slug",
                schema: "community",
                table: "post_hashtags",
                column: "Slug");

            migrationBuilder.CreateIndex(
                name: "IX_post_mentions_PostId",
                schema: "community",
                table: "post_mentions",
                column: "PostId");

            migrationBuilder.CreateIndex(
                name: "ix_post_revisions_post_number",
                schema: "community",
                table: "post_revisions",
                columns: new[] { "PostId", "RevisionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_reaction_definitions_key",
                schema: "community",
                table: "reaction_definitions",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_reaction_summaries_content",
                schema: "community",
                table: "reaction_summaries",
                column: "ContentKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_reaction_tallies_SummaryId",
                schema: "community",
                table: "reaction_tallies",
                column: "SummaryId");

            migrationBuilder.CreateIndex(
                name: "ix_reactions_content",
                schema: "community",
                table: "reactions",
                columns: new[] { "content_type", "content_id" });

            migrationBuilder.CreateIndex(
                name: "ix_reactions_user_content_kind",
                schema: "community",
                table: "reactions",
                column: "UniquenessKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_shares_content",
                schema: "community",
                table: "shares",
                columns: new[] { "content_type", "content_id" });

            migrationBuilder.CreateIndex(
                name: "IX_visibility_clauses_RuleId",
                schema: "community",
                table: "visibility_clauses",
                column: "RuleId");

            migrationBuilder.CreateIndex(
                name: "ix_visibility_rules_owner",
                schema: "community",
                table: "visibility_rules",
                column: "OwnerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "comment_mentions",
                schema: "community");

            migrationBuilder.DropTable(
                name: "community_bookmark_folders",
                schema: "community");

            migrationBuilder.DropTable(
                name: "community_bookmarks",
                schema: "community");

            migrationBuilder.DropTable(
                name: "community_moderation_decisions",
                schema: "community");

            migrationBuilder.DropTable(
                name: "content_reports",
                schema: "community");

            migrationBuilder.DropTable(
                name: "content_reviews",
                schema: "community");

            migrationBuilder.DropTable(
                name: "feed_subscriptions",
                schema: "community");

            migrationBuilder.DropTable(
                name: "follows",
                schema: "community");

            migrationBuilder.DropTable(
                name: "hashtags",
                schema: "community");

            migrationBuilder.DropTable(
                name: "mentions",
                schema: "community");

            migrationBuilder.DropTable(
                name: "poll_options",
                schema: "community");

            migrationBuilder.DropTable(
                name: "poll_votes",
                schema: "community");

            migrationBuilder.DropTable(
                name: "post_attachments",
                schema: "community");

            migrationBuilder.DropTable(
                name: "post_hashtags",
                schema: "community");

            migrationBuilder.DropTable(
                name: "post_mentions",
                schema: "community");

            migrationBuilder.DropTable(
                name: "post_revisions",
                schema: "community");

            migrationBuilder.DropTable(
                name: "reaction_definitions",
                schema: "community");

            migrationBuilder.DropTable(
                name: "reaction_tallies",
                schema: "community");

            migrationBuilder.DropTable(
                name: "reactions",
                schema: "community");

            migrationBuilder.DropTable(
                name: "shares",
                schema: "community");

            migrationBuilder.DropTable(
                name: "visibility_clauses",
                schema: "community");

            migrationBuilder.DropTable(
                name: "comments",
                schema: "community");

            migrationBuilder.DropTable(
                name: "polls",
                schema: "community");

            migrationBuilder.DropTable(
                name: "reaction_summaries",
                schema: "community");

            migrationBuilder.DropTable(
                name: "visibility_rules",
                schema: "community");

            migrationBuilder.DropTable(
                name: "community_posts",
                schema: "community");
        }
    }
}
