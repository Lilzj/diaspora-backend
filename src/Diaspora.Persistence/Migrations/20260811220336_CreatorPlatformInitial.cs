using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diaspora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CreatorPlatformInitial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "creator");

            migrationBuilder.AddColumn<bool>(
                name: "IsSpend",
                schema: "financial",
                table: "financial_reward_entries",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "LifetimeSpent",
                schema: "financial",
                table: "financial_reward_accounts",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "academy_courses",
                schema: "creator",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Summary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    SectionKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Level = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    EstimatedMinutes = table.Column<int>(type: "integer", nullable: false),
                    CoverImageUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    Icon = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    LessonCount = table.Column<int>(type: "integer", nullable: false),
                    AwardsCertificate = table.Column<bool>(type: "boolean", nullable: false),
                    AwardsBadgeKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CompletionPoints = table.Column<int>(type: "integer", nullable: false),
                    ContentVersion = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_academy_courses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "academy_lessons",
                schema: "creator",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    EstimatedMinutes = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    QuizId = table.Column<Guid>(type: "uuid", nullable: true),
                    ContentVersion = table.Column<int>(type: "integer", nullable: false),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_academy_lessons", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "academy_quizzes",
                schema: "creator",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LessonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PassMark = table.Column<int>(type: "integer", nullable: false),
                    ContentVersion = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_academy_quizzes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "academy_sections",
                schema: "creator",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Icon = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_academy_sections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "community_grants",
                schema: "creator",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Summary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Category = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    FundingMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Exponent = table.Column<int>(type: "integer", nullable: false),
                    FundingMinimumMinorUnits = table.Column<long>(type: "bigint", nullable: true),
                    FundingMaximumMinorUnits = table.Column<long>(type: "bigint", nullable: true),
                    OpensAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeadlineAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                    AllowsAmountRequest = table.Column<bool>(type: "boolean", nullable: false),
                    AllowsAttachments = table.Column<bool>(type: "boolean", nullable: false),
                    ApplicantCount = table.Column<int>(type: "integer", nullable: false),
                    MinimumStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RequiresVerifiedCreator = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_grants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "creator_activity_entries",
                schema: "creator",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActionType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Weight = table.Column<int>(type: "integer", nullable: false),
                    SubjectType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    CountryCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    CommunityId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceReference = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_creator_activity_entries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "creator_certificates",
                schema: "creator",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    CourseTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RecipientName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    VerificationReference = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    IssuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_creator_certificates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "creator_challenge_enrollments",
                schema: "creator",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChallengeId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    JoinedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClaimedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Score = table.Column<long>(type: "bigint", nullable: false),
                    JoinIdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ClaimIdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RewardGrantReference = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    RewardPointsGranted = table.Column<int>(type: "integer", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_creator_challenge_enrollments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "creator_challenges",
                schema: "creator",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Icon = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Category = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                    IsCancelled = table.Column<bool>(type: "boolean", nullable: false),
                    HasLeaderboard = table.Column<bool>(type: "boolean", nullable: false),
                    MinimumStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ParticipantCount = table.Column<int>(type: "integer", nullable: false),
                    RewardKind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RewardPoints = table.Column<int>(type: "integer", nullable: false),
                    RewardBadgeKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    RewardLabel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_creator_challenges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "creator_learning_progress",
                schema: "creator",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    CourseSlug = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsSaved = table.Column<bool>(type: "boolean", nullable: false),
                    CertificateId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompletionGrantReference = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_creator_learning_progress", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "creator_milestone_progress",
                schema: "creator",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    MilestoneId = table.Column<Guid>(type: "uuid", nullable: false),
                    MilestoneKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Current = table.Column<long>(type: "bigint", nullable: false),
                    Target = table.Column<long>(type: "bigint", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClaimedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ExpiredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClaimIdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RewardGrantReference = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    RewardPointsGranted = table.Column<int>(type: "integer", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_creator_milestone_progress", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "creator_milestones",
                schema: "creator",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Icon = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Category = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Target = table.Column<long>(type: "bigint", nullable: false),
                    Unit = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    AlmostCompletePercentage = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RewardKind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RewardPoints = table.Column<int>(type: "integer", nullable: false),
                    RewardBadgeKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    RewardEntitlementKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    RewardLabel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_creator_milestones", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "creator_point_rules",
                schema: "creator",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ActionType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Points = table.Column<int>(type: "integer", nullable: false),
                    ActionRoute = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    LimitLabel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Category = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    MaxOccurrences = table.Column<int>(type: "integer", nullable: true),
                    CooldownHours = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_creator_point_rules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "creator_profiles",
                schema: "creator",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Handle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AvatarUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    CoverImageUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    Bio = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CountryCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    EarnedStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    StatusChangedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsVerifiedCreator = table.Column<bool>(type: "boolean", nullable: false),
                    HasVerificationInReview = table.Column<bool>(type: "boolean", nullable: false),
                    IsRestricted = table.Column<bool>(type: "boolean", nullable: false),
                    IsSuspended = table.Column<bool>(type: "boolean", nullable: false),
                    StandingReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsDiscoverable = table.Column<bool>(type: "boolean", nullable: false),
                    Followers = table.Column<long>(type: "bigint", nullable: false),
                    Following = table.Column<long>(type: "bigint", nullable: false),
                    TotalContentPublished = table.Column<long>(type: "bigint", nullable: false),
                    TotalHelpfulVotes = table.Column<long>(type: "bigint", nullable: false),
                    RecognisedActions = table.Column<long>(type: "bigint", nullable: false),
                    WeightedContributionScore = table.Column<long>(type: "bigint", nullable: false),
                    JoinedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_creator_profiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "creator_referral_codes",
                schema: "creator",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IssuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_creator_referral_codes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "creator_referrals",
                schema: "creator",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReferralCodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReferrerCreatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReferrerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    InviteeUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    InviteeLabel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    StateReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    InvitedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SignedUpAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    QualifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RewardedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RewardPoints = table.Column<int>(type: "integer", nullable: false),
                    RewardGrantReference = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_creator_referrals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "creator_reward_redemptions",
                schema: "creator",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RewardId = table.Column<Guid>(type: "uuid", nullable: false),
                    RewardKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RewardTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PointsSpent = table.Column<int>(type: "integer", nullable: false),
                    CatalogueVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    GrantReference = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    PointsDebitReference = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_creator_reward_redemptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "creator_rewards",
                schema: "creator",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    PointsCost = table.Column<int>(type: "integer", nullable: false),
                    Category = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Icon = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    GrantKind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    MinimumStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    MaxPerCreator = table.Column<int>(type: "integer", nullable: true),
                    CooldownHours = table.Column<int>(type: "integer", nullable: true),
                    AvailableFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AvailableUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_creator_rewards", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "creator_signup_bonuses",
                schema: "creator",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Points = table.Column<int>(type: "integer", nullable: false),
                    MemberSafeReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    AcknowledgedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AwardedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AcknowledgeIdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    GrantReference = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_creator_signup_bonuses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "creator_verification_applications",
                schema: "creator",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReviewStartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RespondByAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DecisionReason = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Note = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    HasAppealed = table.Column<bool>(type: "boolean", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_creator_verification_applications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "grant_applications",
                schema: "creator",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GrantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicantUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DecisionReason = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    RespondByAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RequestedMinorUnits = table.Column<long>(type: "bigint", nullable: true),
                    AwardedMinorUnits = table.Column<long>(type: "bigint", nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    SubmitIdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    FundingReference = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_grant_applications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "academy_course_prerequisites",
                schema: "creator",
                columns: table => new
                {
                    CourseSlug = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    AcademyCourseId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_academy_course_prerequisites", x => new { x.AcademyCourseId, x.CourseSlug });
                    table.ForeignKey(
                        name: "FK_academy_course_prerequisites_academy_courses_AcademyCourseId",
                        column: x => x.AcademyCourseId,
                        principalSchema: "creator",
                        principalTable: "academy_courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "academy_course_tags",
                schema: "creator",
                columns: table => new
                {
                    Value = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AcademyCourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_academy_course_tags", x => new { x.AcademyCourseId, x.Value });
                    table.ForeignKey(
                        name: "FK_academy_course_tags_academy_courses_AcademyCourseId",
                        column: x => x.AcademyCourseId,
                        principalSchema: "creator",
                        principalTable: "academy_courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "academy_lesson_block_items",
                schema: "creator",
                columns: table => new
                {
                    BlockSortOrder = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    AcademyLessonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_academy_lesson_block_items", x => new { x.AcademyLessonId, x.BlockSortOrder, x.SortOrder });
                    table.ForeignKey(
                        name: "FK_academy_lesson_block_items_academy_lessons_AcademyLessonId",
                        column: x => x.AcademyLessonId,
                        principalSchema: "creator",
                        principalTable: "academy_lessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "academy_lesson_blocks",
                schema: "creator",
                columns: table => new
                {
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    AcademyLessonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Tone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    IsOrdered = table.Column<bool>(type: "boolean", nullable: false),
                    Route = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_academy_lesson_blocks", x => new { x.AcademyLessonId, x.SortOrder });
                    table.ForeignKey(
                        name: "FK_academy_lesson_blocks_academy_lessons_AcademyLessonId",
                        column: x => x.AcademyLessonId,
                        principalSchema: "creator",
                        principalTable: "academy_lessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "academy_quiz_options",
                schema: "creator",
                columns: table => new
                {
                    OptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    IsCorrect = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    AcademyQuizId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_academy_quiz_options", x => x.OptionId);
                    table.ForeignKey(
                        name: "FK_academy_quiz_options_academy_quizzes_AcademyQuizId",
                        column: x => x.AcademyQuizId,
                        principalSchema: "creator",
                        principalTable: "academy_quizzes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "academy_quiz_questions",
                schema: "creator",
                columns: table => new
                {
                    QuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Prompt = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Explanation = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    AcademyQuizId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_academy_quiz_questions", x => x.QuestionId);
                    table.ForeignKey(
                        name: "FK_academy_quiz_questions_academy_quizzes_AcademyQuizId",
                        column: x => x.AcademyQuizId,
                        principalSchema: "creator",
                        principalTable: "academy_quizzes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "community_grant_countries",
                schema: "creator",
                columns: table => new
                {
                    CountryCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    CommunityGrantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_grant_countries", x => new { x.CommunityGrantId, x.CountryCode });
                    table.ForeignKey(
                        name: "FK_community_grant_countries_community_grants_CommunityGrantId",
                        column: x => x.CommunityGrantId,
                        principalSchema: "creator",
                        principalTable: "community_grants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "community_grant_declarations",
                schema: "creator",
                columns: table => new
                {
                    Key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CommunityGrantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_grant_declarations", x => new { x.CommunityGrantId, x.Key });
                    table.ForeignKey(
                        name: "FK_community_grant_declarations_community_grants_CommunityGran~",
                        column: x => x.CommunityGrantId,
                        principalSchema: "creator",
                        principalTable: "community_grants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "community_grant_deliverables",
                schema: "creator",
                columns: table => new
                {
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CommunityGrantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_grant_deliverables", x => new { x.CommunityGrantId, x.SortOrder });
                    table.ForeignKey(
                        name: "FK_community_grant_deliverables_community_grants_CommunityGran~",
                        column: x => x.CommunityGrantId,
                        principalSchema: "creator",
                        principalTable: "community_grants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "community_grant_field_options",
                schema: "creator",
                columns: table => new
                {
                    FieldKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Value = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CommunityGrantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_grant_field_options", x => new { x.CommunityGrantId, x.FieldKey, x.Value });
                    table.ForeignKey(
                        name: "FK_community_grant_field_options_community_grants_CommunityGra~",
                        column: x => x.CommunityGrantId,
                        principalSchema: "creator",
                        principalTable: "community_grants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "community_grant_fields",
                schema: "creator",
                columns: table => new
                {
                    Key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CommunityGrantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    HelpText = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    MaxLength = table.Column<int>(type: "integer", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_grant_fields", x => new { x.CommunityGrantId, x.Key });
                    table.ForeignKey(
                        name: "FK_community_grant_fields_community_grants_CommunityGrantId",
                        column: x => x.CommunityGrantId,
                        principalSchema: "creator",
                        principalTable: "community_grants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "creator_challenge_task_progress",
                schema: "creator",
                columns: table => new
                {
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatorChallengeEnrollmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    RawCount = table.Column<long>(type: "bigint", nullable: false),
                    WeightedCount = table.Column<long>(type: "bigint", nullable: false),
                    Target = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_creator_challenge_task_progress", x => new { x.CreatorChallengeEnrollmentId, x.TaskId });
                    table.ForeignKey(
                        name: "FK_creator_challenge_task_progress_creator_challenge_enrollmen~",
                        column: x => x.CreatorChallengeEnrollmentId,
                        principalSchema: "creator",
                        principalTable: "creator_challenge_enrollments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "creator_challenge_countries",
                schema: "creator",
                columns: table => new
                {
                    CountryCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    CreatorChallengeId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_creator_challenge_countries", x => new { x.CreatorChallengeId, x.CountryCode });
                    table.ForeignKey(
                        name: "FK_creator_challenge_countries_creator_challenges_CreatorChall~",
                        column: x => x.CreatorChallengeId,
                        principalSchema: "creator",
                        principalTable: "creator_challenges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "creator_challenge_rules",
                schema: "creator",
                columns: table => new
                {
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatorChallengeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_creator_challenge_rules", x => new { x.CreatorChallengeId, x.SortOrder });
                    table.ForeignKey(
                        name: "FK_creator_challenge_rules_creator_challenges_CreatorChallenge~",
                        column: x => x.CreatorChallengeId,
                        principalSchema: "creator",
                        principalTable: "creator_challenges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "creator_challenge_tasks",
                schema: "creator",
                columns: table => new
                {
                    TaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ActionType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Target = table.Column<long>(type: "bigint", nullable: false),
                    Weight = table.Column<int>(type: "integer", nullable: false),
                    Window = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ActionRoute = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    ActionLabel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatorChallengeId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_creator_challenge_tasks", x => x.TaskId);
                    table.ForeignKey(
                        name: "FK_creator_challenge_tasks_creator_challenges_CreatorChallenge~",
                        column: x => x.CreatorChallengeId,
                        principalSchema: "creator",
                        principalTable: "creator_challenges",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "creator_completed_lessons",
                schema: "creator",
                columns: table => new
                {
                    LessonId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatorLearningProgressId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentVersion = table.Column<int>(type: "integer", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_creator_completed_lessons", x => new { x.CreatorLearningProgressId, x.LessonId });
                    table.ForeignKey(
                        name: "FK_creator_completed_lessons_creator_learning_progress_Creator~",
                        column: x => x.CreatorLearningProgressId,
                        principalSchema: "creator",
                        principalTable: "creator_learning_progress",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "creator_quiz_attempts",
                schema: "creator",
                columns: table => new
                {
                    QuizId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatorLearningProgressId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    Passed = table.Column<bool>(type: "boolean", nullable: false),
                    LastScore = table.Column<int>(type: "integer", nullable: false),
                    LastAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_creator_quiz_attempts", x => new { x.CreatorLearningProgressId, x.QuizId });
                    table.ForeignKey(
                        name: "FK_creator_quiz_attempts_creator_learning_progress_CreatorLear~",
                        column: x => x.CreatorLearningProgressId,
                        principalSchema: "creator",
                        principalTable: "creator_learning_progress",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "creator_milestone_progress_rules",
                schema: "creator",
                columns: table => new
                {
                    ActionType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatorMilestoneProgressId = table.Column<Guid>(type: "uuid", nullable: false),
                    RawCount = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_creator_milestone_progress_rules", x => new { x.CreatorMilestoneProgressId, x.ActionType });
                    table.ForeignKey(
                        name: "FK_creator_milestone_progress_rules_creator_milestone_progress~",
                        column: x => x.CreatorMilestoneProgressId,
                        principalSchema: "creator",
                        principalTable: "creator_milestone_progress",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "creator_milestone_rules",
                schema: "creator",
                columns: table => new
                {
                    ActionType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatorMilestoneDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Weight = table.Column<int>(type: "integer", nullable: false),
                    Cap = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_creator_milestone_rules", x => new { x.CreatorMilestoneDefinitionId, x.ActionType });
                    table.ForeignKey(
                        name: "FK_creator_milestone_rules_creator_milestones_CreatorMilestone~",
                        column: x => x.CreatorMilestoneDefinitionId,
                        principalSchema: "creator",
                        principalTable: "creator_milestones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "creator_profile_tags",
                schema: "creator",
                columns: table => new
                {
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    NormalisedValue = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatorProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_creator_profile_tags", x => new { x.CreatorProfileId, x.Kind, x.NormalisedValue });
                    table.ForeignKey(
                        name: "FK_creator_profile_tags_creator_profiles_CreatorProfileId",
                        column: x => x.CreatorProfileId,
                        principalSchema: "creator",
                        principalTable: "creator_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "creator_verification_acknowledged",
                schema: "creator",
                columns: table => new
                {
                    Key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatorVerificationApplicationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_creator_verification_acknowledged", x => new { x.CreatorVerificationApplicationId, x.Key });
                    table.ForeignKey(
                        name: "FK_creator_verification_acknowledged_creator_verification_appl~",
                        column: x => x.CreatorVerificationApplicationId,
                        principalSchema: "creator",
                        principalTable: "creator_verification_applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "creator_verification_decisions",
                schema: "creator",
                columns: table => new
                {
                    RecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreviousState = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CurrentState = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatorVerificationApplicationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_creator_verification_decisions", x => x.RecordId);
                    table.ForeignKey(
                        name: "FK_creator_verification_decisions_creator_verification_applica~",
                        column: x => x.CreatorVerificationApplicationId,
                        principalSchema: "creator",
                        principalTable: "creator_verification_applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "creator_verification_evidence",
                schema: "creator",
                columns: table => new
                {
                    EvidenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RejectionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    UploadedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatorVerificationApplicationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_creator_verification_evidence", x => x.EvidenceId);
                    table.ForeignKey(
                        name: "FK_creator_verification_evidence_creator_verification_applicat~",
                        column: x => x.CreatorVerificationApplicationId,
                        principalSchema: "creator",
                        principalTable: "creator_verification_applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "creator_verification_information_requests",
                schema: "creator",
                columns: table => new
                {
                    Text = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatorVerificationApplicationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_creator_verification_information_requests", x => new { x.CreatorVerificationApplicationId, x.RequestedAt, x.Text });
                    table.ForeignKey(
                        name: "FK_creator_verification_information_requests_creator_verificat~",
                        column: x => x.CreatorVerificationApplicationId,
                        principalSchema: "creator",
                        principalTable: "creator_verification_applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "grant_application_answers",
                schema: "creator",
                columns: table => new
                {
                    FieldKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    GrantApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_grant_application_answers", x => new { x.GrantApplicationId, x.FieldKey });
                    table.ForeignKey(
                        name: "FK_grant_application_answers_grant_applications_GrantApplicati~",
                        column: x => x.GrantApplicationId,
                        principalSchema: "creator",
                        principalTable: "grant_applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "grant_application_attachments",
                schema: "creator",
                columns: table => new
                {
                    Reference = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    GrantApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttachedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_grant_application_attachments", x => new { x.GrantApplicationId, x.Reference });
                    table.ForeignKey(
                        name: "FK_grant_application_attachments_grant_applications_GrantAppli~",
                        column: x => x.GrantApplicationId,
                        principalSchema: "creator",
                        principalTable: "grant_applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "grant_application_decisions",
                schema: "creator",
                columns: table => new
                {
                    RecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    PreviousState = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CurrentState = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    GrantApplicationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_grant_application_decisions", x => x.RecordId);
                    table.ForeignKey(
                        name: "FK_grant_application_decisions_grant_applications_GrantApplica~",
                        column: x => x.GrantApplicationId,
                        principalSchema: "creator",
                        principalTable: "grant_applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "grant_application_declarations",
                schema: "creator",
                columns: table => new
                {
                    Key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    GrantApplicationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_grant_application_declarations", x => new { x.GrantApplicationId, x.Key });
                    table.ForeignKey(
                        name: "FK_grant_application_declarations_grant_applications_GrantAppl~",
                        column: x => x.GrantApplicationId,
                        principalSchema: "creator",
                        principalTable: "grant_applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "grant_application_information_requests",
                schema: "creator",
                columns: table => new
                {
                    Text = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    GrantApplicationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_grant_application_information_requests", x => new { x.GrantApplicationId, x.RequestedAt, x.Text });
                    table.ForeignKey(
                        name: "FK_grant_application_information_requests_grant_applications_G~",
                        column: x => x.GrantApplicationId,
                        principalSchema: "creator",
                        principalTable: "grant_applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "grant_application_reports",
                schema: "creator",
                columns: table => new
                {
                    ReportId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Summary = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    GrantApplicationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_grant_application_reports", x => x.ReportId);
                    table.ForeignKey(
                        name: "FK_grant_application_reports_grant_applications_GrantApplicati~",
                        column: x => x.GrantApplicationId,
                        principalSchema: "creator",
                        principalTable: "grant_applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_academy_courses_catalogue",
                schema: "creator",
                table: "academy_courses",
                columns: new[] { "IsPublished", "SectionKey", "Level", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "ux_academy_courses_slug",
                schema: "creator",
                table: "academy_courses",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_academy_lessons_order",
                schema: "creator",
                table: "academy_lessons",
                columns: new[] { "CourseId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "ux_academy_lessons_key",
                schema: "creator",
                table: "academy_lessons",
                columns: new[] { "CourseId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_academy_quiz_options_AcademyQuizId",
                schema: "creator",
                table: "academy_quiz_options",
                column: "AcademyQuizId");

            migrationBuilder.CreateIndex(
                name: "ix_academy_quiz_options_question",
                schema: "creator",
                table: "academy_quiz_options",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_academy_quiz_questions_AcademyQuizId",
                schema: "creator",
                table: "academy_quiz_questions",
                column: "AcademyQuizId");

            migrationBuilder.CreateIndex(
                name: "ix_academy_quizzes_lesson",
                schema: "creator",
                table: "academy_quizzes",
                column: "LessonId");

            migrationBuilder.CreateIndex(
                name: "ux_academy_quizzes_key",
                schema: "creator",
                table: "academy_quizzes",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_academy_sections_order",
                schema: "creator",
                table: "academy_sections",
                columns: new[] { "IsPublished", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "ux_academy_sections_key",
                schema: "creator",
                table: "academy_sections",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_community_grants_discovery",
                schema: "creator",
                table: "community_grants",
                columns: new[] { "IsPublished", "Category", "DeadlineAt" });

            migrationBuilder.CreateIndex(
                name: "ux_community_grants_key",
                schema: "creator",
                table: "community_grants",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_creator_activity_action",
                schema: "creator",
                table: "creator_activity_entries",
                columns: new[] { "CreatorId", "ActionType", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "ix_creator_activity_community",
                schema: "creator",
                table: "creator_activity_entries",
                columns: new[] { "CommunityId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "ix_creator_activity_period",
                schema: "creator",
                table: "creator_activity_entries",
                columns: new[] { "OccurredAt", "CountryCode" });

            migrationBuilder.CreateIndex(
                name: "ux_creator_activity_source",
                schema: "creator",
                table: "creator_activity_entries",
                columns: new[] { "CreatorId", "SourceReference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_creator_certificates_course",
                schema: "creator",
                table: "creator_certificates",
                columns: new[] { "CreatorId", "CourseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_creator_certificates_reference",
                schema: "creator",
                table: "creator_certificates",
                column: "VerificationReference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_creator_enrollments_leaderboard",
                schema: "creator",
                table: "creator_challenge_enrollments",
                columns: new[] { "ChallengeId", "Score" });

            migrationBuilder.CreateIndex(
                name: "ix_creator_enrollments_state",
                schema: "creator",
                table: "creator_challenge_enrollments",
                columns: new[] { "CreatorId", "State" });

            migrationBuilder.CreateIndex(
                name: "ux_creator_enrollments_claim_key",
                schema: "creator",
                table: "creator_challenge_enrollments",
                column: "ClaimIdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_creator_enrollments_creator",
                schema: "creator",
                table: "creator_challenge_enrollments",
                columns: new[] { "ChallengeId", "CreatorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_creator_enrollments_join_key",
                schema: "creator",
                table: "creator_challenge_enrollments",
                column: "JoinIdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_creator_challenge_tasks_action",
                schema: "creator",
                table: "creator_challenge_tasks",
                column: "ActionType");

            migrationBuilder.CreateIndex(
                name: "IX_creator_challenge_tasks_CreatorChallengeId",
                schema: "creator",
                table: "creator_challenge_tasks",
                column: "CreatorChallengeId");

            migrationBuilder.CreateIndex(
                name: "ix_creator_challenges_discovery",
                schema: "creator",
                table: "creator_challenges",
                columns: new[] { "IsPublished", "IsCancelled", "EndsAt" });

            migrationBuilder.CreateIndex(
                name: "ux_creator_challenges_key",
                schema: "creator",
                table: "creator_challenges",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_creator_learning_progress_saved",
                schema: "creator",
                table: "creator_learning_progress",
                columns: new[] { "CreatorId", "IsSaved" });

            migrationBuilder.CreateIndex(
                name: "ix_creator_learning_progress_state",
                schema: "creator",
                table: "creator_learning_progress",
                columns: new[] { "CreatorId", "State" });

            migrationBuilder.CreateIndex(
                name: "ux_creator_learning_progress_course",
                schema: "creator",
                table: "creator_learning_progress",
                columns: new[] { "CreatorId", "CourseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_creator_milestone_progress_state",
                schema: "creator",
                table: "creator_milestone_progress",
                columns: new[] { "CreatorId", "State" });

            migrationBuilder.CreateIndex(
                name: "ux_creator_milestone_progress_claim_key",
                schema: "creator",
                table: "creator_milestone_progress",
                column: "ClaimIdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_creator_milestone_progress_creator",
                schema: "creator",
                table: "creator_milestone_progress",
                columns: new[] { "CreatorId", "MilestoneId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_creator_milestone_rules_action",
                schema: "creator",
                table: "creator_milestone_rules",
                column: "ActionType");

            migrationBuilder.CreateIndex(
                name: "ix_creator_milestones_catalogue",
                schema: "creator",
                table: "creator_milestones",
                columns: new[] { "IsActive", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "ux_creator_milestones_key",
                schema: "creator",
                table: "creator_milestones",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_creator_point_rules_action",
                schema: "creator",
                table: "creator_point_rules",
                columns: new[] { "IsActive", "ActionType" });

            migrationBuilder.CreateIndex(
                name: "ux_creator_point_rules_key",
                schema: "creator",
                table: "creator_point_rules",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_creator_profile_tags_lookup",
                schema: "creator",
                table: "creator_profile_tags",
                columns: new[] { "Kind", "NormalisedValue" });

            migrationBuilder.CreateIndex(
                name: "ix_creator_profiles_discovery",
                schema: "creator",
                table: "creator_profiles",
                columns: new[] { "IsDiscoverable", "CountryCode", "WeightedContributionScore" });

            migrationBuilder.CreateIndex(
                name: "ix_creator_profiles_score",
                schema: "creator",
                table: "creator_profiles",
                column: "WeightedContributionScore");

            migrationBuilder.CreateIndex(
                name: "ix_creator_profiles_status",
                schema: "creator",
                table: "creator_profiles",
                columns: new[] { "IsDiscoverable", "IsVerifiedCreator", "Status" });

            migrationBuilder.CreateIndex(
                name: "ux_creator_profiles_user",
                schema: "creator",
                table: "creator_profiles",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_creator_referral_codes_code",
                schema: "creator",
                table: "creator_referral_codes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_creator_referral_codes_creator",
                schema: "creator",
                table: "creator_referral_codes",
                column: "CreatorId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_creator_referrals_expiry",
                schema: "creator",
                table: "creator_referrals",
                columns: new[] { "State", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "ix_creator_referrals_history",
                schema: "creator",
                table: "creator_referrals",
                columns: new[] { "ReferrerCreatorId", "InvitedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_creator_referrals_state",
                schema: "creator",
                table: "creator_referrals",
                columns: new[] { "ReferrerCreatorId", "State" });

            migrationBuilder.CreateIndex(
                name: "ux_creator_referrals_invitee",
                schema: "creator",
                table: "creator_referrals",
                column: "InviteeUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_creator_redemptions_history",
                schema: "creator",
                table: "creator_reward_redemptions",
                columns: new[] { "CreatorId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_creator_redemptions_reward",
                schema: "creator",
                table: "creator_reward_redemptions",
                columns: new[] { "CreatorId", "RewardId" });

            migrationBuilder.CreateIndex(
                name: "ux_creator_redemptions_key",
                schema: "creator",
                table: "creator_reward_redemptions",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_creator_rewards_catalogue",
                schema: "creator",
                table: "creator_rewards",
                columns: new[] { "IsActive", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "ix_creator_rewards_window",
                schema: "creator",
                table: "creator_rewards",
                columns: new[] { "AvailableFrom", "AvailableUntil" });

            migrationBuilder.CreateIndex(
                name: "ux_creator_rewards_key",
                schema: "creator",
                table: "creator_rewards",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_creator_signup_bonuses_expiry",
                schema: "creator",
                table: "creator_signup_bonuses",
                columns: new[] { "State", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "ux_creator_signup_bonuses_key",
                schema: "creator",
                table: "creator_signup_bonuses",
                column: "AcknowledgeIdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_creator_signup_bonuses_user",
                schema: "creator",
                table: "creator_signup_bonuses",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_creator_verifications_expiry",
                schema: "creator",
                table: "creator_verification_applications",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "ix_creator_verifications_queue",
                schema: "creator",
                table: "creator_verification_applications",
                columns: new[] { "State", "SubmittedAt" });

            migrationBuilder.CreateIndex(
                name: "ux_creator_verifications_creator",
                schema: "creator",
                table: "creator_verification_applications",
                column: "CreatorId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_creator_verifications_key",
                schema: "creator",
                table: "creator_verification_applications",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_creator_verification_decisions_actor",
                schema: "creator",
                table: "creator_verification_decisions",
                columns: new[] { "ActorUserId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_creator_verification_decisions_CreatorVerificationApplicati~",
                schema: "creator",
                table: "creator_verification_decisions",
                column: "CreatorVerificationApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_creator_verification_evidence_CreatorVerificationApplicatio~",
                schema: "creator",
                table: "creator_verification_evidence",
                column: "CreatorVerificationApplicationId");

            migrationBuilder.CreateIndex(
                name: "ix_grant_application_decisions_actor",
                schema: "creator",
                table: "grant_application_decisions",
                columns: new[] { "ActorUserId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_grant_application_decisions_GrantApplicationId",
                schema: "creator",
                table: "grant_application_decisions",
                column: "GrantApplicationId");

            migrationBuilder.CreateIndex(
                name: "ix_grant_application_reports_due",
                schema: "creator",
                table: "grant_application_reports",
                columns: new[] { "State", "DueAt" });

            migrationBuilder.CreateIndex(
                name: "IX_grant_application_reports_GrantApplicationId",
                schema: "creator",
                table: "grant_application_reports",
                column: "GrantApplicationId");

            migrationBuilder.CreateIndex(
                name: "ix_grant_applications_creator_state",
                schema: "creator",
                table: "grant_applications",
                columns: new[] { "CreatorId", "State" });

            migrationBuilder.CreateIndex(
                name: "ix_grant_applications_queue",
                schema: "creator",
                table: "grant_applications",
                columns: new[] { "State", "SubmittedAt" });

            migrationBuilder.CreateIndex(
                name: "ux_grant_applications_creator",
                schema: "creator",
                table: "grant_applications",
                columns: new[] { "GrantId", "CreatorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_grant_applications_submit_key",
                schema: "creator",
                table: "grant_applications",
                column: "SubmitIdempotencyKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "academy_course_prerequisites",
                schema: "creator");

            migrationBuilder.DropTable(
                name: "academy_course_tags",
                schema: "creator");

            migrationBuilder.DropTable(
                name: "academy_lesson_block_items",
                schema: "creator");

            migrationBuilder.DropTable(
                name: "academy_lesson_blocks",
                schema: "creator");

            migrationBuilder.DropTable(
                name: "academy_quiz_options",
                schema: "creator");

            migrationBuilder.DropTable(
                name: "academy_quiz_questions",
                schema: "creator");

            migrationBuilder.DropTable(
                name: "academy_sections",
                schema: "creator");

            migrationBuilder.DropTable(
                name: "community_grant_countries",
                schema: "creator");

            migrationBuilder.DropTable(
                name: "community_grant_declarations",
                schema: "creator");

            migrationBuilder.DropTable(
                name: "community_grant_deliverables",
                schema: "creator");

            migrationBuilder.DropTable(
                name: "community_grant_field_options",
                schema: "creator");

            migrationBuilder.DropTable(
                name: "community_grant_fields",
                schema: "creator");

            migrationBuilder.DropTable(
                name: "creator_activity_entries",
                schema: "creator");

            migrationBuilder.DropTable(
                name: "creator_certificates",
                schema: "creator");

            migrationBuilder.DropTable(
                name: "creator_challenge_countries",
                schema: "creator");

            migrationBuilder.DropTable(
                name: "creator_challenge_rules",
                schema: "creator");

            migrationBuilder.DropTable(
                name: "creator_challenge_task_progress",
                schema: "creator");

            migrationBuilder.DropTable(
                name: "creator_challenge_tasks",
                schema: "creator");

            migrationBuilder.DropTable(
                name: "creator_completed_lessons",
                schema: "creator");

            migrationBuilder.DropTable(
                name: "creator_milestone_progress_rules",
                schema: "creator");

            migrationBuilder.DropTable(
                name: "creator_milestone_rules",
                schema: "creator");

            migrationBuilder.DropTable(
                name: "creator_point_rules",
                schema: "creator");

            migrationBuilder.DropTable(
                name: "creator_profile_tags",
                schema: "creator");

            migrationBuilder.DropTable(
                name: "creator_quiz_attempts",
                schema: "creator");

            migrationBuilder.DropTable(
                name: "creator_referral_codes",
                schema: "creator");

            migrationBuilder.DropTable(
                name: "creator_referrals",
                schema: "creator");

            migrationBuilder.DropTable(
                name: "creator_reward_redemptions",
                schema: "creator");

            migrationBuilder.DropTable(
                name: "creator_rewards",
                schema: "creator");

            migrationBuilder.DropTable(
                name: "creator_signup_bonuses",
                schema: "creator");

            migrationBuilder.DropTable(
                name: "creator_verification_acknowledged",
                schema: "creator");

            migrationBuilder.DropTable(
                name: "creator_verification_decisions",
                schema: "creator");

            migrationBuilder.DropTable(
                name: "creator_verification_evidence",
                schema: "creator");

            migrationBuilder.DropTable(
                name: "creator_verification_information_requests",
                schema: "creator");

            migrationBuilder.DropTable(
                name: "grant_application_answers",
                schema: "creator");

            migrationBuilder.DropTable(
                name: "grant_application_attachments",
                schema: "creator");

            migrationBuilder.DropTable(
                name: "grant_application_decisions",
                schema: "creator");

            migrationBuilder.DropTable(
                name: "grant_application_declarations",
                schema: "creator");

            migrationBuilder.DropTable(
                name: "grant_application_information_requests",
                schema: "creator");

            migrationBuilder.DropTable(
                name: "grant_application_reports",
                schema: "creator");

            migrationBuilder.DropTable(
                name: "academy_courses",
                schema: "creator");

            migrationBuilder.DropTable(
                name: "academy_lessons",
                schema: "creator");

            migrationBuilder.DropTable(
                name: "academy_quizzes",
                schema: "creator");

            migrationBuilder.DropTable(
                name: "community_grants",
                schema: "creator");

            migrationBuilder.DropTable(
                name: "creator_challenge_enrollments",
                schema: "creator");

            migrationBuilder.DropTable(
                name: "creator_challenges",
                schema: "creator");

            migrationBuilder.DropTable(
                name: "creator_milestone_progress",
                schema: "creator");

            migrationBuilder.DropTable(
                name: "creator_milestones",
                schema: "creator");

            migrationBuilder.DropTable(
                name: "creator_profiles",
                schema: "creator");

            migrationBuilder.DropTable(
                name: "creator_learning_progress",
                schema: "creator");

            migrationBuilder.DropTable(
                name: "creator_verification_applications",
                schema: "creator");

            migrationBuilder.DropTable(
                name: "grant_applications",
                schema: "creator");

            migrationBuilder.DropColumn(
                name: "IsSpend",
                schema: "financial",
                table: "financial_reward_entries");

            migrationBuilder.DropColumn(
                name: "LifetimeSpent",
                schema: "financial",
                table: "financial_reward_accounts");
        }
    }
}
