using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diaspora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TrustModuleInitial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "trust");

            migrationBuilder.CreateTable(
                name: "appeals",
                schema: "trust",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TargetId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Explanation = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsCorrectionRequest = table.Column<bool>(type: "boolean", nullable: false),
                    OriginalDeciderId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignedReviewerId = table.Column<Guid>(type: "uuid", nullable: true),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReviewDueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true),
                    evidence_keys = table.Column<string>(type: "character varying(5130)", maxLength: 5130, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_appeals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "community_contributions",
                schema: "trust",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConfirmedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TargetId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ConfirmedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_contributions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "moderation_items",
                schema: "trust",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ContentId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Priority = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    AiReviewClean = table.Column<bool>(type: "boolean", nullable: true),
                    AiConfidence = table.Column<decimal>(type: "numeric(4,3)", precision: 4, scale: 3, nullable: true),
                    AiReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReportCount = table.Column<int>(type: "integer", nullable: false),
                    AssignedModeratorId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true),
                    ai_flags = table.Column<string>(type: "character varying(650)", maxLength: 650, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_moderation_items", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "reputation_profiles",
                schema: "trust",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContributionScore = table.Column<int>(type: "integer", nullable: false),
                    VerificationScore = table.Column<int>(type: "integer", nullable: false),
                    TrustScore = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    GuidesPublished = table.Column<int>(type: "integer", nullable: false),
                    GuidesApproved = table.Column<int>(type: "integer", nullable: false),
                    GuidesUpdated = table.Column<int>(type: "integer", nullable: false),
                    UpdatesPublished = table.Column<int>(type: "integer", nullable: false),
                    UpdatesVerified = table.Column<int>(type: "integer", nullable: false),
                    CommentsWritten = table.Column<int>(type: "integer", nullable: false),
                    PhoneVerified = table.Column<bool>(type: "boolean", nullable: false),
                    EmailVerified = table.Column<bool>(type: "boolean", nullable: false),
                    GovernmentIdVerified = table.Column<bool>(type: "boolean", nullable: false),
                    ProfilePhotoAdded = table.Column<bool>(type: "boolean", nullable: false),
                    ProfileCompleted = table.Column<bool>(type: "boolean", nullable: false),
                    ProviderVerified = table.Column<bool>(type: "boolean", nullable: false),
                    CountryVerificationsCount = table.Column<int>(type: "integer", nullable: false),
                    HelpfulVotesReceived = table.Column<int>(type: "integer", nullable: false),
                    CommunityConfirmationsReceived = table.Column<int>(type: "integer", nullable: false),
                    ViolationCount = table.Column<int>(type: "integer", nullable: false),
                    ReportsResolved = table.Column<int>(type: "integer", nullable: false),
                    NoViolationSince = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastCalculatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LevelChangedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reputation_profiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "trust_events",
                schema: "trust",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Icon = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    IsMemberVisible = table.Column<bool>(type: "boolean", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trust_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "trust_profiles",
                schema: "trust",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tier = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Health = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ViolationCount = table.Column<int>(type: "integer", nullable: false),
                    WarningCount = table.Column<int>(type: "integer", nullable: false),
                    ActiveAppealCount = table.Column<int>(type: "integer", nullable: false),
                    SpamReportCount = table.Column<int>(type: "integer", nullable: false),
                    HealthReasons = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    risk_score = table.Column<int>(type: "integer", nullable: false),
                    risk_level = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    risk_factors = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    risk_assessed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    JoinedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TierChangedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true),
                    grants = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trust_profiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "verification_requests",
                schema: "trust",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Nationality = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    ResidenceCountry = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    TargetCountry = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    ArrivalDate = table.Column<DateOnly>(type: "date", nullable: true),
                    SupportingNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SelfieStorageKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    RejectionDetails = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AssignedReviewerId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReviewDueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true),
                    expertise_areas = table.Column<string>(type: "character varying(810)", maxLength: 810, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_verification_requests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "appeal_reviews",
                schema: "trust",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AppealId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewerId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ToStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_appeal_reviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_appeal_reviews_appeals_AppealId",
                        column: x => x.AppealId,
                        principalSchema: "trust",
                        principalTable: "appeals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "moderation_decisions",
                schema: "trust",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ModeratorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    PreviousStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    NewStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_moderation_decisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_moderation_decisions_moderation_items_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "trust",
                        principalTable: "moderation_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "reputation_events",
                schema: "trust",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActionType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Dimension = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Points = table.Column<int>(type: "integer", nullable: false),
                    SourceType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SourceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reputation_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_reputation_events_reputation_profiles_ProfileId",
                        column: x => x.ProfileId,
                        principalSchema: "trust",
                        principalTable: "reputation_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "badge_awards",
                schema: "trust",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Badge = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    AwardedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevokedReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AwardedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_badge_awards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_badge_awards_trust_profiles_ProfileId",
                        column: x => x.ProfileId,
                        principalSchema: "trust",
                        principalTable: "trust_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "country_standings",
                schema: "trust",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    Points = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IsVerified = table.Column<bool>(type: "boolean", nullable: false),
                    ArrivedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    DepartedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    Role = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_country_standings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_country_standings_trust_profiles_ProfileId",
                        column: x => x.ProfileId,
                        principalSchema: "trust",
                        principalTable: "trust_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "verification_documents",
                schema: "trust",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentType = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    StorageKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    OriginalFileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: true),
                    ContentType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    SizeInBytes = table.Column<long>(type: "bigint", nullable: false),
                    DocumentNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    IssuingCountry = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    IssuedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    ExpiresOn = table.Column<DateOnly>(type: "date", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    ReviewComment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    UploadedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_verification_documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_verification_documents_verification_requests_RequestId",
                        column: x => x.RequestId,
                        principalSchema: "trust",
                        principalTable: "verification_requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "verification_reviews",
                schema: "trust",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewerId = table.Column<Guid>(type: "uuid", nullable: true),
                    DecidedByName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    FromStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ToStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DecisionMode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RejectionReason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_verification_reviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_verification_reviews_verification_requests_RequestId",
                        column: x => x.RequestId,
                        principalSchema: "trust",
                        principalTable: "verification_requests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_appeal_reviews_appeal",
                schema: "trust",
                table: "appeal_reviews",
                column: "AppealId");

            migrationBuilder.CreateIndex(
                name: "ix_appeals_status_due",
                schema: "trust",
                table: "appeals",
                columns: new[] { "Status", "ReviewDueAt" });

            migrationBuilder.CreateIndex(
                name: "ix_appeals_user_target_status",
                schema: "trust",
                table: "appeals",
                columns: new[] { "UserId", "TargetId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_badge_awards_profile_badge_country",
                schema: "trust",
                table: "badge_awards",
                columns: new[] { "ProfileId", "Badge", "CountryCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_community_contributions_confirmer_target",
                schema: "trust",
                table: "community_contributions",
                columns: new[] { "ConfirmedByUserId", "TargetType", "TargetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_community_contributions_target",
                schema: "trust",
                table: "community_contributions",
                columns: new[] { "TargetType", "TargetId" });

            migrationBuilder.CreateIndex(
                name: "ix_country_standings_profile_country",
                schema: "trust",
                table: "country_standings",
                columns: new[] { "ProfileId", "CountryCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_moderation_decisions_item",
                schema: "trust",
                table: "moderation_decisions",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "ix_moderation_items_content_status",
                schema: "trust",
                table: "moderation_items",
                columns: new[] { "ContentType", "ContentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_moderation_items_status_priority_created",
                schema: "trust",
                table: "moderation_items",
                columns: new[] { "Status", "Priority", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_reputation_events_profile_action_created",
                schema: "trust",
                table: "reputation_events",
                columns: new[] { "ProfileId", "ActionType", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_reputation_profiles_user",
                schema: "trust",
                table: "reputation_profiles",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_trust_events_user_visible_occurred",
                schema: "trust",
                table: "trust_events",
                columns: new[] { "UserId", "IsMemberVisible", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "ix_trust_profiles_user",
                schema: "trust",
                table: "trust_profiles",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_verification_documents_request",
                schema: "trust",
                table: "verification_documents",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "ix_verification_requests_expires_at",
                schema: "trust",
                table: "verification_requests",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "ix_verification_requests_status_due",
                schema: "trust",
                table: "verification_requests",
                columns: new[] { "Status", "ReviewDueAt" });

            migrationBuilder.CreateIndex(
                name: "ix_verification_requests_user_type_status",
                schema: "trust",
                table: "verification_requests",
                columns: new[] { "UserId", "Type", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_verification_reviews_request",
                schema: "trust",
                table: "verification_reviews",
                column: "RequestId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "appeal_reviews",
                schema: "trust");

            migrationBuilder.DropTable(
                name: "badge_awards",
                schema: "trust");

            migrationBuilder.DropTable(
                name: "community_contributions",
                schema: "trust");

            migrationBuilder.DropTable(
                name: "country_standings",
                schema: "trust");

            migrationBuilder.DropTable(
                name: "moderation_decisions",
                schema: "trust");

            migrationBuilder.DropTable(
                name: "reputation_events",
                schema: "trust");

            migrationBuilder.DropTable(
                name: "trust_events",
                schema: "trust");

            migrationBuilder.DropTable(
                name: "verification_documents",
                schema: "trust");

            migrationBuilder.DropTable(
                name: "verification_reviews",
                schema: "trust");

            migrationBuilder.DropTable(
                name: "appeals",
                schema: "trust");

            migrationBuilder.DropTable(
                name: "trust_profiles",
                schema: "trust");

            migrationBuilder.DropTable(
                name: "moderation_items",
                schema: "trust");

            migrationBuilder.DropTable(
                name: "reputation_profiles",
                schema: "trust");

            migrationBuilder.DropTable(
                name: "verification_requests",
                schema: "trust");
        }
    }
}
