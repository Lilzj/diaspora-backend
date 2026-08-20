using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diaspora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CommunityProgrammeModuleInitial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "community_programme");

            migrationBuilder.CreateTable(
                name: "community_announcements",
                schema: "community_programme",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Scope = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ScopeId = table.Column<Guid>(type: "uuid", nullable: true),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    PublishedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    IsPinned = table.Column<bool>(type: "boolean", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_community_announcements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "community_poll_responses",
                schema: "community_programme",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PollId = table.Column<Guid>(type: "uuid", nullable: false),
                    VoterUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    VoterFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OpenResponse = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    RespondedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_poll_responses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "community_polls",
                schema: "community_programme",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Question = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    VotingType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Audience = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    NeighborhoodAreaId = table.Column<Guid>(type: "uuid", nullable: true),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    MaxSelections = table.Column<int>(type: "integer", nullable: true),
                    IsAnonymous = table.Column<bool>(type: "boolean", nullable: false),
                    AllowsChange = table.Column<bool>(type: "boolean", nullable: false),
                    ShowResultsBeforeClose = table.Column<bool>(type: "boolean", nullable: false),
                    OpensAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClosesAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TotalResponses = table.Column<int>(type: "integer", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_community_polls", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "membership_programmes",
                schema: "community_programme",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Summary = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    Visibility = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IsOpen = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresApproval = table.Column<bool>(type: "boolean", nullable: false),
                    MemberCount = table.Column<int>(type: "integer", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_membership_programmes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "programme_memberships",
                schema: "community_programme",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    TierId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DecidedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    DecisionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    JoinedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RenewalDueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PaymentIntentId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApplicationIdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RenewalIdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_programme_memberships", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "volunteer_applications",
                schema: "community_programme",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OpportunityId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    VolunteerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShiftId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DecidedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DecisionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    WaitlistedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_volunteer_applications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "volunteer_assignments",
                schema: "community_programme",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OpportunityId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    VolunteerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShiftId = table.Column<Guid>(type: "uuid", nullable: true),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CheckedInAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CheckedOutAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConfirmedHours = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    ConfirmedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConfirmedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_volunteer_assignments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "volunteer_certificates",
                schema: "community_programme",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VolunteerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OpportunityId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Hours = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    IssuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_volunteer_certificates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "volunteer_opportunities",
                schema: "community_programme",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Cause = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    CityName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    AreaName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    AddressLine = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: true),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ApplicationDeadline = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    MinimumAge = table.Column<int>(type: "integer", nullable: true),
                    Capacity = table.Column<int>(type: "integer", nullable: true),
                    FilledCount = table.Column<int>(type: "integer", nullable: false),
                    WaitlistCount = table.Column<int>(type: "integer", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_volunteer_opportunities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "community_announcement_media",
                schema: "community_programme",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MediaId = table.Column<Guid>(type: "uuid", nullable: false),
                    CommunityAnnouncementId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_announcement_media", x => x.Id);
                    table.ForeignKey(
                        name: "FK_community_announcement_media_community_announcements_Commun~",
                        column: x => x.CommunityAnnouncementId,
                        principalSchema: "community_programme",
                        principalTable: "community_announcements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "community_poll_response_selections",
                schema: "community_programme",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PollResponseId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_poll_response_selections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_community_poll_response_selections_community_poll_responses~",
                        column: x => x.PollResponseId,
                        principalSchema: "community_programme",
                        principalTable: "community_poll_responses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "community_poll_invitees",
                schema: "community_programme",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CommunityPollId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_poll_invitees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_community_poll_invitees_community_polls_CommunityPollId",
                        column: x => x.CommunityPollId,
                        principalSchema: "community_programme",
                        principalTable: "community_polls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "community_poll_options",
                schema: "community_programme",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CommunityPollId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_poll_options", x => x.Id);
                    table.ForeignKey(
                        name: "FK_community_poll_options_community_polls_CommunityPollId",
                        column: x => x.CommunityPollId,
                        principalSchema: "community_programme",
                        principalTable: "community_polls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "membership_tiers",
                schema: "community_programme",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    price_minor_units = table.Column<long>(type: "bigint", nullable: true),
                    price_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    price_exponent = table.Column<int>(type: "integer", nullable: true),
                    BillingPeriod = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Capacity = table.Column<int>(type: "integer", nullable: true),
                    FilledCount = table.Column<int>(type: "integer", nullable: false),
                    IsRecommended = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    MembershipProgrammeId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_membership_tiers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_membership_tiers_membership_programmes_MembershipProgrammeId",
                        column: x => x.MembershipProgrammeId,
                        principalSchema: "community_programme",
                        principalTable: "membership_programmes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "programme_membership_history",
                schema: "community_programme",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Actor = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProgrammeMembershipId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_programme_membership_history", x => x.Id);
                    table.ForeignKey(
                        name: "FK_programme_membership_history_programme_memberships_Programm~",
                        column: x => x.ProgrammeMembershipId,
                        principalSchema: "community_programme",
                        principalTable: "programme_memberships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "volunteer_application_answers",
                schema: "community_programme",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Question = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Answer = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    VolunteerApplicationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_volunteer_application_answers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_volunteer_application_answers_volunteer_applications_Volunt~",
                        column: x => x.VolunteerApplicationId,
                        principalSchema: "community_programme",
                        principalTable: "volunteer_applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "volunteer_opportunity_skills",
                schema: "community_programme",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    VolunteerOpportunityId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_volunteer_opportunity_skills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_volunteer_opportunity_skills_volunteer_opportunities_Volunt~",
                        column: x => x.VolunteerOpportunityId,
                        principalSchema: "community_programme",
                        principalTable: "volunteer_opportunities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "volunteer_shifts",
                schema: "community_programme",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Capacity = table.Column<int>(type: "integer", nullable: false),
                    FilledCount = table.Column<int>(type: "integer", nullable: false),
                    VolunteerOpportunityId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_volunteer_shifts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_volunteer_shifts_volunteer_opportunities_VolunteerOpportuni~",
                        column: x => x.VolunteerOpportunityId,
                        principalSchema: "community_programme",
                        principalTable: "volunteer_opportunities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "membership_tier_benefits",
                schema: "community_programme",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Icon = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    MembershipTierId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_membership_tier_benefits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_membership_tier_benefits_membership_tiers_MembershipTierId",
                        column: x => x.MembershipTierId,
                        principalSchema: "community_programme",
                        principalTable: "membership_tiers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "membership_tier_requirements",
                schema: "community_programme",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ActionRoute = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    MinimumAge = table.Column<int>(type: "integer", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    MembershipTierId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_membership_tier_requirements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_membership_tier_requirements_membership_tiers_MembershipTie~",
                        column: x => x.MembershipTierId,
                        principalSchema: "community_programme",
                        principalTable: "membership_tiers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_community_announcement_media_CommunityAnnouncementId",
                schema: "community_programme",
                table: "community_announcement_media",
                column: "CommunityAnnouncementId");

            migrationBuilder.CreateIndex(
                name: "ix_community_programme_announcements_organization",
                schema: "community_programme",
                table: "community_announcements",
                columns: new[] { "OrganizationId", "PublishedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_community_programme_announcements_scope",
                schema: "community_programme",
                table: "community_announcements",
                columns: new[] { "Scope", "ScopeId", "PublishedAt" });

            migrationBuilder.CreateIndex(
                name: "ux_community_programme_announcements_key",
                schema: "community_programme",
                table: "community_announcements",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_community_poll_invitees_CommunityPollId",
                schema: "community_programme",
                table: "community_poll_invitees",
                column: "CommunityPollId");

            migrationBuilder.CreateIndex(
                name: "ix_community_programme_poll_invitees_user",
                schema: "community_programme",
                table: "community_poll_invitees",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_community_poll_options_CommunityPollId",
                schema: "community_programme",
                table: "community_poll_options",
                column: "CommunityPollId");

            migrationBuilder.CreateIndex(
                name: "IX_community_poll_response_selections_PollResponseId",
                schema: "community_programme",
                table: "community_poll_response_selections",
                column: "PollResponseId");

            migrationBuilder.CreateIndex(
                name: "ix_community_programme_poll_selections_option",
                schema: "community_programme",
                table: "community_poll_response_selections",
                column: "OptionId");

            migrationBuilder.CreateIndex(
                name: "ix_community_programme_poll_responses_poll",
                schema: "community_programme",
                table: "community_poll_responses",
                columns: new[] { "PollId", "RespondedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_community_programme_poll_responses_voter_history",
                schema: "community_programme",
                table: "community_poll_responses",
                columns: new[] { "VoterUserId", "RespondedAt" });

            migrationBuilder.CreateIndex(
                name: "ux_community_programme_poll_responses_key",
                schema: "community_programme",
                table: "community_poll_responses",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_community_programme_poll_responses_voter",
                schema: "community_programme",
                table: "community_poll_responses",
                columns: new[] { "PollId", "VoterFingerprint" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_community_programme_polls_closes",
                schema: "community_programme",
                table: "community_polls",
                columns: new[] { "Status", "ClosesAt" });

            migrationBuilder.CreateIndex(
                name: "ix_community_programme_polls_discovery",
                schema: "community_programme",
                table: "community_polls",
                columns: new[] { "Status", "Audience", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_community_programme_polls_opens",
                schema: "community_programme",
                table: "community_polls",
                columns: new[] { "Status", "OpensAt" });

            migrationBuilder.CreateIndex(
                name: "ix_community_programme_polls_organization",
                schema: "community_programme",
                table: "community_polls",
                columns: new[] { "OrganizationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ux_community_programme_programmes_organization",
                schema: "community_programme",
                table: "membership_programmes",
                column: "OrganizationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_membership_tier_benefits_MembershipTierId",
                schema: "community_programme",
                table: "membership_tier_benefits",
                column: "MembershipTierId");

            migrationBuilder.CreateIndex(
                name: "IX_membership_tier_requirements_MembershipTierId",
                schema: "community_programme",
                table: "membership_tier_requirements",
                column: "MembershipTierId");

            migrationBuilder.CreateIndex(
                name: "IX_membership_tiers_MembershipProgrammeId",
                schema: "community_programme",
                table: "membership_tiers",
                column: "MembershipProgrammeId");

            migrationBuilder.CreateIndex(
                name: "IX_programme_membership_history_ProgrammeMembershipId",
                schema: "community_programme",
                table: "programme_membership_history",
                column: "ProgrammeMembershipId");

            migrationBuilder.CreateIndex(
                name: "ix_community_programme_memberships_expires",
                schema: "community_programme",
                table: "programme_memberships",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "ix_community_programme_memberships_member_state",
                schema: "community_programme",
                table: "programme_memberships",
                columns: new[] { "MemberUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_community_programme_memberships_renewal_due",
                schema: "community_programme",
                table: "programme_memberships",
                column: "RenewalDueAt");

            migrationBuilder.CreateIndex(
                name: "ix_community_programme_memberships_roll",
                schema: "community_programme",
                table: "programme_memberships",
                columns: new[] { "OrganizationId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "ux_community_programme_memberships_application_key",
                schema: "community_programme",
                table: "programme_memberships",
                column: "ApplicationIdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_community_programme_memberships_member",
                schema: "community_programme",
                table: "programme_memberships",
                columns: new[] { "OrganizationId", "MemberUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_community_programme_memberships_renewal_key",
                schema: "community_programme",
                table: "programme_memberships",
                column: "RenewalIdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_volunteer_application_answers_VolunteerApplicationId",
                schema: "community_programme",
                table: "volunteer_application_answers",
                column: "VolunteerApplicationId");

            migrationBuilder.CreateIndex(
                name: "ix_community_programme_applications_mine",
                schema: "community_programme",
                table: "volunteer_applications",
                columns: new[] { "VolunteerUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_community_programme_applications_organization",
                schema: "community_programme",
                table: "volunteer_applications",
                columns: new[] { "OrganizationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_community_programme_applications_queue",
                schema: "community_programme",
                table: "volunteer_applications",
                columns: new[] { "OpportunityId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "ux_community_programme_applications_key",
                schema: "community_programme",
                table: "volunteer_applications",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_community_programme_applications_volunteer",
                schema: "community_programme",
                table: "volunteer_applications",
                columns: new[] { "OpportunityId", "VolunteerUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_community_programme_assignments_confirmed",
                schema: "community_programme",
                table: "volunteer_assignments",
                columns: new[] { "OrganizationId", "ConfirmedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_community_programme_assignments_opportunity",
                schema: "community_programme",
                table: "volunteer_assignments",
                columns: new[] { "OpportunityId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_community_programme_assignments_volunteer",
                schema: "community_programme",
                table: "volunteer_assignments",
                columns: new[] { "VolunteerUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ux_community_programme_assignments_application",
                schema: "community_programme",
                table: "volunteer_assignments",
                column: "ApplicationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_community_programme_certificates_volunteer",
                schema: "community_programme",
                table: "volunteer_certificates",
                columns: new[] { "VolunteerUserId", "IssuedAt" });

            migrationBuilder.CreateIndex(
                name: "ux_community_programme_certificates_assignment",
                schema: "community_programme",
                table: "volunteer_certificates",
                column: "AssignmentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_community_programme_opportunities_deadline",
                schema: "community_programme",
                table: "volunteer_opportunities",
                column: "ApplicationDeadline");

            migrationBuilder.CreateIndex(
                name: "ix_community_programme_opportunities_discovery",
                schema: "community_programme",
                table: "volunteer_opportunities",
                columns: new[] { "Status", "CountryCode", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_community_programme_opportunities_organization",
                schema: "community_programme",
                table: "volunteer_opportunities",
                columns: new[] { "OrganizationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ux_community_programme_opportunities_key",
                schema: "community_programme",
                table: "volunteer_opportunities",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_volunteer_opportunity_skills_VolunteerOpportunityId",
                schema: "community_programme",
                table: "volunteer_opportunity_skills",
                column: "VolunteerOpportunityId");

            migrationBuilder.CreateIndex(
                name: "IX_volunteer_shifts_VolunteerOpportunityId",
                schema: "community_programme",
                table: "volunteer_shifts",
                column: "VolunteerOpportunityId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "community_announcement_media",
                schema: "community_programme");

            migrationBuilder.DropTable(
                name: "community_poll_invitees",
                schema: "community_programme");

            migrationBuilder.DropTable(
                name: "community_poll_options",
                schema: "community_programme");

            migrationBuilder.DropTable(
                name: "community_poll_response_selections",
                schema: "community_programme");

            migrationBuilder.DropTable(
                name: "membership_tier_benefits",
                schema: "community_programme");

            migrationBuilder.DropTable(
                name: "membership_tier_requirements",
                schema: "community_programme");

            migrationBuilder.DropTable(
                name: "programme_membership_history",
                schema: "community_programme");

            migrationBuilder.DropTable(
                name: "volunteer_application_answers",
                schema: "community_programme");

            migrationBuilder.DropTable(
                name: "volunteer_assignments",
                schema: "community_programme");

            migrationBuilder.DropTable(
                name: "volunteer_certificates",
                schema: "community_programme");

            migrationBuilder.DropTable(
                name: "volunteer_opportunity_skills",
                schema: "community_programme");

            migrationBuilder.DropTable(
                name: "volunteer_shifts",
                schema: "community_programme");

            migrationBuilder.DropTable(
                name: "community_announcements",
                schema: "community_programme");

            migrationBuilder.DropTable(
                name: "community_polls",
                schema: "community_programme");

            migrationBuilder.DropTable(
                name: "community_poll_responses",
                schema: "community_programme");

            migrationBuilder.DropTable(
                name: "membership_tiers",
                schema: "community_programme");

            migrationBuilder.DropTable(
                name: "programme_memberships",
                schema: "community_programme");

            migrationBuilder.DropTable(
                name: "volunteer_applications",
                schema: "community_programme");

            migrationBuilder.DropTable(
                name: "volunteer_opportunities",
                schema: "community_programme");

            migrationBuilder.DropTable(
                name: "membership_programmes",
                schema: "community_programme");
        }
    }
}
