using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Diaspora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GuidesModuleInitial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "guides");

            migrationBuilder.CreateTable(
                name: "guide_analytics",
                schema: "guides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GuideId = table.Column<Guid>(type: "uuid", nullable: false),
                    Views = table.Column<long>(type: "bigint", nullable: false),
                    UniqueViews = table.Column<long>(type: "bigint", nullable: false),
                    Completions = table.Column<long>(type: "bigint", nullable: false),
                    TotalReadSeconds = table.Column<long>(type: "bigint", nullable: false),
                    Bookmarks = table.Column<int>(type: "integer", nullable: false),
                    Shares = table.Column<int>(type: "integer", nullable: false),
                    HelpfulVotes = table.Column<int>(type: "integer", nullable: false),
                    NotHelpfulVotes = table.Column<int>(type: "integer", nullable: false),
                    SearchImpressions = table.Column<long>(type: "bigint", nullable: false),
                    SearchClicks = table.Column<long>(type: "bigint", nullable: false),
                    VersionAdoptions = table.Column<int>(type: "integer", nullable: false),
                    LastViewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guide_analytics", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "guide_author_standings",
                schema: "guides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsApprovedContributor = table.Column<bool>(type: "boolean", nullable: false),
                    IsCommunityGuide = table.Column<bool>(type: "boolean", nullable: false),
                    TrustLevel = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ContributorChangedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CommunityGuideChangedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TrustLevelChangedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guide_author_standings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "guide_categories",
                schema: "guides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Icon = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ParentKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guide_categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "guide_draft_snapshots",
                schema: "guides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GuideId = table.Column<Guid>(type: "uuid", nullable: false),
                    Snapshot = table.Column<string>(type: "text", nullable: false),
                    SavedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    SavedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guide_draft_snapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "guide_suggestions",
                schema: "guides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GuideId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetSectionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProposedContent = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    Rationale = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DecidedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DecisionNote = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guide_suggestions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "guide_versions",
                schema: "guides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GuideId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Summary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Language = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    CategoryKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ContentSnapshot = table.Column<string>(type: "text", nullable: false),
                    RevisionNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    EditorId = table.Column<Guid>(type: "uuid", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    WordCount = table.Column<int>(type: "integer", nullable: false),
                    ReadingTimeMinutes = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guide_versions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "guide_votes",
                schema: "guides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GuideId = table.Column<Guid>(type: "uuid", nullable: false),
                    VoterId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    VoterKey = table.Column<string>(type: "character varying(65)", maxLength: 65, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guide_votes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "guides",
                schema: "guides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    slug = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false),
                    summary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    excerpt = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CategoryKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    language = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    TranslationGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    country_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    region_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    city_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    area_id = table.Column<Guid>(type: "uuid", nullable: true),
                    area_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Visibility = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ModerationState = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IsFeatured = table.Column<bool>(type: "boolean", nullable: false),
                    FeaturedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FirstPublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ScheduledPublishAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ArchivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    meta_title = table.Column<string>(type: "character varying(70)", maxLength: 70, nullable: true),
                    meta_description = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    canonical_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    seo_keywords = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    difficulty = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    reading_time_minutes = table.Column<int>(type: "integer", nullable: false),
                    estimated_completion_minutes = table.Column<int>(type: "integer", nullable: true),
                    verification_state = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    verification_id = table.Column<Guid>(type: "uuid", nullable: true),
                    verification_badge = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    verification_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    verification_changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    view_count = table.Column<long>(type: "bigint", nullable: false),
                    unique_view_count = table.Column<long>(type: "bigint", nullable: false),
                    helpful_count = table.Column<int>(type: "integer", nullable: false),
                    not_helpful_count = table.Column<int>(type: "integer", nullable: false),
                    bookmark_count = table.Column<int>(type: "integer", nullable: false),
                    share_count = table.Column<int>(type: "integer", nullable: false),
                    comment_count = table.Column<int>(type: "integer", nullable: false),
                    CurrentVersion = table.Column<int>(type: "integer", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guides", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "guide_author_badges",
                schema: "guides",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Badge = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    AwardedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StandingId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guide_author_badges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_guide_author_badges_guide_author_standings_StandingId",
                        column: x => x.StandingId,
                        principalSchema: "guides",
                        principalTable: "guide_author_standings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "guide_media_references",
                schema: "guides",
                columns: table => new
                {
                    MediaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    GuideId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    Caption = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guide_media_references", x => new { x.GuideId, x.MediaId, x.Role });
                    table.ForeignKey(
                        name: "FK_guide_media_references_guides_GuideId",
                        column: x => x.GuideId,
                        principalSchema: "guides",
                        principalTable: "guides",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "guide_moderation_notes",
                schema: "guides",
                columns: table => new
                {
                    NoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    GuideId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guide_moderation_notes", x => x.NoteId);
                    table.ForeignKey(
                        name: "FK_guide_moderation_notes_guides_GuideId",
                        column: x => x.GuideId,
                        principalSchema: "guides",
                        principalTable: "guides",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "guide_provider_references",
                schema: "guides",
                columns: table => new
                {
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: false),
                    GuideId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guide_provider_references", x => new { x.GuideId, x.ProviderId });
                    table.ForeignKey(
                        name: "FK_guide_provider_references_guides_GuideId",
                        column: x => x.GuideId,
                        principalSchema: "guides",
                        principalTable: "guides",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "guide_sections",
                schema: "guides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    GuideId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guide_sections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_guide_sections_guides_GuideId",
                        column: x => x.GuideId,
                        principalSchema: "guides",
                        principalTable: "guides",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "guide_tags",
                schema: "guides",
                columns: table => new
                {
                    Slug = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    GuideId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guide_tags", x => new { x.GuideId, x.Slug });
                    table.ForeignKey(
                        name: "FK_guide_tags_guides_GuideId",
                        column: x => x.GuideId,
                        principalSchema: "guides",
                        principalTable: "guides",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "guide_section_blocks",
                schema: "guides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: true),
                    MediaId = table.Column<Guid>(type: "uuid", nullable: true),
                    Metadata = table.Column<string>(type: "text", nullable: true),
                    SectionId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guide_section_blocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_guide_section_blocks_guide_sections_SectionId",
                        column: x => x.SectionId,
                        principalSchema: "guides",
                        principalTable: "guide_sections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_guide_analytics_guide",
                schema: "guides",
                table: "guide_analytics",
                column: "GuideId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_guide_author_badges_StandingId",
                schema: "guides",
                table: "guide_author_badges",
                column: "StandingId");

            migrationBuilder.CreateIndex(
                name: "ux_guide_author_standing_user",
                schema: "guides",
                table: "guide_author_standings",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_guide_categories_key",
                schema: "guides",
                table: "guide_categories",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_guide_drafts_guide_saved",
                schema: "guides",
                table: "guide_draft_snapshots",
                columns: new[] { "GuideId", "SavedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_guide_media_references_media",
                schema: "guides",
                table: "guide_media_references",
                column: "MediaId");

            migrationBuilder.CreateIndex(
                name: "IX_guide_moderation_notes_GuideId",
                schema: "guides",
                table: "guide_moderation_notes",
                column: "GuideId");

            migrationBuilder.CreateIndex(
                name: "ix_guide_provider_references_provider",
                schema: "guides",
                table: "guide_provider_references",
                column: "ProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_guide_section_blocks_SectionId",
                schema: "guides",
                table: "guide_section_blocks",
                column: "SectionId");

            migrationBuilder.CreateIndex(
                name: "IX_guide_sections_GuideId",
                schema: "guides",
                table: "guide_sections",
                column: "GuideId");

            migrationBuilder.CreateIndex(
                name: "ix_guide_suggestions_guide_status",
                schema: "guides",
                table: "guide_suggestions",
                columns: new[] { "GuideId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_guide_tags_slug",
                schema: "guides",
                table: "guide_tags",
                column: "Slug");

            migrationBuilder.CreateIndex(
                name: "ux_guide_versions_guide_version",
                schema: "guides",
                table: "guide_versions",
                columns: new[] { "GuideId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_guide_votes_voter_key",
                schema: "guides",
                table: "guide_votes",
                column: "VoterKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_guides_author",
                schema: "guides",
                table: "guides",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "ix_guides_category",
                schema: "guides",
                table: "guides",
                column: "CategoryKey");

            migrationBuilder.CreateIndex(
                name: "ix_guides_country",
                schema: "guides",
                table: "guides",
                column: "country_code");

            migrationBuilder.CreateIndex(
                name: "ix_guides_language",
                schema: "guides",
                table: "guides",
                column: "language");

            migrationBuilder.CreateIndex(
                name: "ix_guides_scheduled",
                schema: "guides",
                table: "guides",
                column: "ScheduledPublishAt");

            migrationBuilder.CreateIndex(
                name: "ix_guides_status_published_at",
                schema: "guides",
                table: "guides",
                columns: new[] { "Status", "PublishedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_guides_translation_group",
                schema: "guides",
                table: "guides",
                column: "TranslationGroupId");

            migrationBuilder.CreateIndex(
                name: "ix_guides_verification_state",
                schema: "guides",
                table: "guides",
                column: "verification_state");

            migrationBuilder.CreateIndex(
                name: "ux_guides_slug",
                schema: "guides",
                table: "guides",
                column: "slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "guide_analytics",
                schema: "guides");

            migrationBuilder.DropTable(
                name: "guide_author_badges",
                schema: "guides");

            migrationBuilder.DropTable(
                name: "guide_categories",
                schema: "guides");

            migrationBuilder.DropTable(
                name: "guide_draft_snapshots",
                schema: "guides");

            migrationBuilder.DropTable(
                name: "guide_media_references",
                schema: "guides");

            migrationBuilder.DropTable(
                name: "guide_moderation_notes",
                schema: "guides");

            migrationBuilder.DropTable(
                name: "guide_provider_references",
                schema: "guides");

            migrationBuilder.DropTable(
                name: "guide_section_blocks",
                schema: "guides");

            migrationBuilder.DropTable(
                name: "guide_suggestions",
                schema: "guides");

            migrationBuilder.DropTable(
                name: "guide_tags",
                schema: "guides");

            migrationBuilder.DropTable(
                name: "guide_versions",
                schema: "guides");

            migrationBuilder.DropTable(
                name: "guide_votes",
                schema: "guides");

            migrationBuilder.DropTable(
                name: "guide_author_standings",
                schema: "guides");

            migrationBuilder.DropTable(
                name: "guide_sections",
                schema: "guides");

            migrationBuilder.DropTable(
                name: "guides",
                schema: "guides");
        }
    }
}
