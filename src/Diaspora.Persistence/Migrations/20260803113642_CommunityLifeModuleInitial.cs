using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diaspora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CommunityLifeModuleInitial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "community_life");

            migrationBuilder.CreateTable(
                name: "community_life_event_analytics",
                schema: "community_life",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    CategoryKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    NeighborhoodAreaId = table.Column<Guid>(type: "uuid", nullable: true),
                    OpenedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ViewCount = table.Column<int>(type: "integer", nullable: false),
                    UniqueViewCount = table.Column<int>(type: "integer", nullable: false),
                    SaveCount = table.Column<int>(type: "integer", nullable: false),
                    ShareCount = table.Column<int>(type: "integer", nullable: false),
                    RsvpCount = table.Column<int>(type: "integer", nullable: false),
                    InterestedCount = table.Column<int>(type: "integer", nullable: false),
                    GoingCount = table.Column<int>(type: "integer", nullable: false),
                    WaitlistedCount = table.Column<int>(type: "integer", nullable: false),
                    CancelledCount = table.Column<int>(type: "integer", nullable: false),
                    CheckedInCount = table.Column<int>(type: "integer", nullable: false),
                    AttendedCount = table.Column<int>(type: "integer", nullable: false),
                    NoShowCount = table.Column<int>(type: "integer", nullable: false),
                    ReportCount = table.Column<int>(type: "integer", nullable: false),
                    TimeToFill = table.Column<TimeSpan>(type: "interval", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_life_event_analytics", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "community_life_event_categories",
                schema: "community_life",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IconName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    GroupName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Facets = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    DefaultsToRsvp = table.Column<bool>(type: "boolean", nullable: false),
                    EventCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_life_event_categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "community_life_events",
                schema: "community_life",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    OrganizerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    HostName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CategoryKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    slug = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false),
                    summary = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    description = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: true),
                    starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    time_zone_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    registration_deadline = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    checkin_opens_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    country_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    region_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    city_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    neighborhood_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    neighborhood_area_id = table.Column<Guid>(type: "uuid", nullable: true),
                    area_path = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    venue_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    venue_line1 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    venue_line2 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    venue_landmark = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    venue_postal_code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    latitude = table.Column<double>(type: "numeric(9,6)", nullable: true),
                    longitude = table.Column<double>(type: "numeric(9,6)", nullable: true),
                    online_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AttendanceMode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    capacity_maximum = table.Column<int>(type: "integer", nullable: true),
                    going_count = table.Column<int>(type: "integer", nullable: false),
                    interested_count = table.Column<int>(type: "integer", nullable: false),
                    waitlist_count = table.Column<int>(type: "integer", nullable: false),
                    checked_in_count = table.Column<int>(type: "integer", nullable: false),
                    family_friendly = table.Column<bool>(type: "boolean", nullable: false),
                    child_friendly = table.Column<bool>(type: "boolean", nullable: false),
                    student_focused = table.Column<bool>(type: "boolean", nullable: false),
                    is_free = table.Column<bool>(type: "boolean", nullable: false),
                    dress_code = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    accessibility_notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    contact_phone = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    contact_whatsapp = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    contact_email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    contact_website = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    contact_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Visibility = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ModerationState = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    view_count = table.Column<int>(type: "integer", nullable: false),
                    unique_view_count = table.Column<int>(type: "integer", nullable: false),
                    save_count = table.Column<int>(type: "integer", nullable: false),
                    share_count = table.Column<int>(type: "integer", nullable: false),
                    rsvp_count = table.Column<int>(type: "integer", nullable: false),
                    RequiresRsvp = table.Column<bool>(type: "boolean", nullable: false),
                    IsAttendeeListPublic = table.Column<bool>(type: "boolean", nullable: false),
                    IsFeatured = table.Column<bool>(type: "boolean", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FirstPublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancellationReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReportCount = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_community_life_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "community_life_memberships",
                schema: "community_life",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RequestMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    InvitedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    JoinedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DecidedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    EndedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RenewsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AttendedEventCount = table.Column<int>(type: "integer", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_life_memberships", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "community_life_organization_analytics",
                schema: "community_life",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    OrganizationTypeKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    NeighborhoodAreaId = table.Column<Guid>(type: "uuid", nullable: true),
                    OpenedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ViewCount = table.Column<int>(type: "integer", nullable: false),
                    UniqueViewCount = table.Column<int>(type: "integer", nullable: false),
                    TotalJoins = table.Column<int>(type: "integer", nullable: false),
                    TotalDepartures = table.Column<int>(type: "integer", nullable: false),
                    TotalRemovals = table.Column<int>(type: "integer", nullable: false),
                    TotalRejections = table.Column<int>(type: "integer", nullable: false),
                    CurrentMemberCount = table.Column<int>(type: "integer", nullable: false),
                    PeakMemberCount = table.Column<int>(type: "integer", nullable: false),
                    EventsPublished = table.Column<int>(type: "integer", nullable: false),
                    EventsCompleted = table.Column<int>(type: "integer", nullable: false),
                    EventsCancelled = table.Column<int>(type: "integer", nullable: false),
                    TotalRsvps = table.Column<int>(type: "integer", nullable: false),
                    TotalAttendances = table.Column<int>(type: "integer", nullable: false),
                    TotalNoShows = table.Column<int>(type: "integer", nullable: false),
                    VolunteerAttendances = table.Column<int>(type: "integer", nullable: false),
                    ReportCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_life_organization_analytics", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "community_life_organization_types",
                schema: "community_life",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IconName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    GroupName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    EncouragesVerification = table.Column<bool>(type: "boolean", nullable: false),
                    OrganizationCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_life_organization_types", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "community_life_organizations",
                schema: "community_life",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationTypeKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    short_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    slug = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false),
                    summary = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    description = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: true),
                    Mission = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Vision = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    country_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    region_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    city_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    neighborhood_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    neighborhood_area_id = table.Column<Guid>(type: "uuid", nullable: true),
                    area_path = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    contact_phone = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    contact_whatsapp = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    contact_email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    contact_website = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    contact_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    social_facebook = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    social_instagram = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    social_twitter = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    social_youtube = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    social_linkedin = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    social_tiktok = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    membership_policy = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    membership_requirements = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    membership_requires_approval = table.Column<bool>(type: "boolean", nullable: false),
                    membership_renewal_months = table.Column<int>(type: "integer", nullable: true),
                    membership_list_public = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Visibility = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ModerationState = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    verification_state = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    verification_id = table.Column<Guid>(type: "uuid", nullable: true),
                    verification_badge = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    verification_decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    verification_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    member_count = table.Column<int>(type: "integer", nullable: false),
                    pending_request_count = table.Column<int>(type: "integer", nullable: false),
                    administrator_count = table.Column<int>(type: "integer", nullable: false),
                    volunteer_count = table.Column<int>(type: "integer", nullable: false),
                    published_event_count = table.Column<int>(type: "integer", nullable: false),
                    completed_event_count = table.Column<int>(type: "integer", nullable: false),
                    IsFeatured = table.Column<bool>(type: "boolean", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FirstPublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastActivityAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReportCount = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_community_life_organizations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "community_life_reports",
                schema: "community_life",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReportedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Detail = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ResolvedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResolutionNote = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    MergedIntoSubjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_life_reports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "community_life_rsvps",
                schema: "community_life",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    AttendeeUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PartySize = table.Column<int>(type: "integer", nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DeclineReason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RespondedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CheckedInAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CheckInMethod = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    CheckedInBy = table.Column<Guid>(type: "uuid", nullable: true),
                    EndedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_life_rsvps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "community_life_event_agenda",
                schema: "community_life",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    StartOffsetMinutes = table.Column<int>(type: "integer", nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: true),
                    SpeakerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    CommunityEventId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_life_event_agenda", x => x.Id);
                    table.ForeignKey(
                        name: "FK_community_life_event_agenda_community_life_events_Community~",
                        column: x => x.CommunityEventId,
                        principalSchema: "community_life",
                        principalTable: "community_life_events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "community_life_event_guides",
                schema: "community_life",
                columns: table => new
                {
                    GuideId = table.Column<Guid>(type: "uuid", nullable: false),
                    CommunityEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Slug = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_life_event_guides", x => new { x.CommunityEventId, x.GuideId });
                    table.ForeignKey(
                        name: "FK_community_life_event_guides_community_life_events_Community~",
                        column: x => x.CommunityEventId,
                        principalSchema: "community_life",
                        principalTable: "community_life_events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "community_life_event_links",
                schema: "community_life",
                columns: table => new
                {
                    ResourceType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    CommunityEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_life_event_links", x => new { x.CommunityEventId, x.ResourceType, x.ResourceId, x.Order });
                    table.ForeignKey(
                        name: "FK_community_life_event_links_community_life_events_CommunityE~",
                        column: x => x.CommunityEventId,
                        principalSchema: "community_life",
                        principalTable: "community_life_events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "community_life_event_media",
                schema: "community_life",
                columns: table => new
                {
                    MediaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CommunityEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    Caption = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_life_event_media", x => new { x.CommunityEventId, x.MediaId, x.Role });
                    table.ForeignKey(
                        name: "FK_community_life_event_media_community_life_events_CommunityE~",
                        column: x => x.CommunityEventId,
                        principalSchema: "community_life",
                        principalTable: "community_life_events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "community_life_event_notes",
                schema: "community_life",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CommunityEventId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_life_event_notes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_community_life_event_notes_community_life_events_CommunityE~",
                        column: x => x.CommunityEventId,
                        principalSchema: "community_life",
                        principalTable: "community_life_events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "community_life_event_providers",
                schema: "community_life",
                columns: table => new
                {
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: false),
                    CommunityEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Role = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_life_event_providers", x => new { x.CommunityEventId, x.ProviderId });
                    table.ForeignKey(
                        name: "FK_community_life_event_providers_community_life_events_Commun~",
                        column: x => x.CommunityEventId,
                        principalSchema: "community_life",
                        principalTable: "community_life_events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "community_life_event_speakers",
                schema: "community_life",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Organization = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Biography = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    PhotoMediaId = table.Column<Guid>(type: "uuid", nullable: true),
                    MemberUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    CommunityEventId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_life_event_speakers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_community_life_event_speakers_community_life_events_Communi~",
                        column: x => x.CommunityEventId,
                        principalSchema: "community_life",
                        principalTable: "community_life_events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "community_life_membership_history",
                schema: "community_life",
                columns: table => new
                {
                    EntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ChangedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    OrganizationMembershipId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_life_membership_history", x => x.EntryId);
                    table.ForeignKey(
                        name: "FK_community_life_membership_history_community_life_membership~",
                        column: x => x.OrganizationMembershipId,
                        principalSchema: "community_life",
                        principalTable: "community_life_memberships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "community_life_organization_guides",
                schema: "community_life",
                columns: table => new
                {
                    GuideId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Slug = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_life_organization_guides", x => new { x.OrganizationId, x.GuideId });
                    table.ForeignKey(
                        name: "FK_community_life_organization_guides_community_life_organizat~",
                        column: x => x.OrganizationId,
                        principalSchema: "community_life",
                        principalTable: "community_life_organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "community_life_organization_hours",
                schema: "community_life",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Day = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    OpensAt = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    ClosesAt = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    IsClosed = table.Column<bool>(type: "boolean", nullable: false),
                    Note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_life_organization_hours", x => x.Id);
                    table.ForeignKey(
                        name: "FK_community_life_organization_hours_community_life_organizati~",
                        column: x => x.OrganizationId,
                        principalSchema: "community_life",
                        principalTable: "community_life_organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "community_life_organization_languages",
                schema: "community_life",
                columns: table => new
                {
                    Name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_life_organization_languages", x => new { x.OrganizationId, x.Name });
                    table.ForeignKey(
                        name: "FK_community_life_organization_languages_community_life_organi~",
                        column: x => x.OrganizationId,
                        principalSchema: "community_life",
                        principalTable: "community_life_organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "community_life_organization_leaders",
                schema: "community_life",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Role = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    MemberUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    PhotoMediaId = table.Column<Guid>(type: "uuid", nullable: true),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_life_organization_leaders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_community_life_organization_leaders_community_life_organiza~",
                        column: x => x.OrganizationId,
                        principalSchema: "community_life",
                        principalTable: "community_life_organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "community_life_organization_media",
                schema: "community_life",
                columns: table => new
                {
                    MediaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    Caption = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_life_organization_media", x => new { x.OrganizationId, x.MediaId, x.Role });
                    table.ForeignKey(
                        name: "FK_community_life_organization_media_community_life_organizati~",
                        column: x => x.OrganizationId,
                        principalSchema: "community_life",
                        principalTable: "community_life_organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "community_life_organization_notes",
                schema: "community_life",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_life_organization_notes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_community_life_organization_notes_community_life_organizati~",
                        column: x => x.OrganizationId,
                        principalSchema: "community_life",
                        principalTable: "community_life_organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "community_life_organization_services",
                schema: "community_life",
                columns: table => new
                {
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_life_organization_services", x => new { x.OrganizationId, x.Name });
                    table.ForeignKey(
                        name: "FK_community_life_organization_services_community_life_organiz~",
                        column: x => x.OrganizationId,
                        principalSchema: "community_life",
                        principalTable: "community_life_organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "community_life_rsvp_history",
                schema: "community_life",
                columns: table => new
                {
                    EntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ChangedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EventRsvpId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_life_rsvp_history", x => x.EntryId);
                    table.ForeignKey(
                        name: "FK_community_life_rsvp_history_community_life_rsvps_EventRsvpId",
                        column: x => x.EventRsvpId,
                        principalSchema: "community_life",
                        principalTable: "community_life_rsvps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_community_life_event_agenda_CommunityEventId",
                schema: "community_life",
                table: "community_life_event_agenda",
                column: "CommunityEventId");

            migrationBuilder.CreateIndex(
                name: "ix_community_life_event_analytics_category",
                schema: "community_life",
                table: "community_life_event_analytics",
                columns: new[] { "CountryCode", "CategoryKey", "CompletedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_community_life_event_analytics_event",
                schema: "community_life",
                table: "community_life_event_analytics",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_community_life_event_analytics_neighborhood",
                schema: "community_life",
                table: "community_life_event_analytics",
                column: "NeighborhoodAreaId");

            migrationBuilder.CreateIndex(
                name: "ix_community_life_event_analytics_organization",
                schema: "community_life",
                table: "community_life_event_analytics",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "ix_community_life_event_categories_facets",
                schema: "community_life",
                table: "community_life_event_categories",
                column: "Facets");

            migrationBuilder.CreateIndex(
                name: "ix_community_life_event_categories_key",
                schema: "community_life",
                table: "community_life_event_categories",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_community_life_event_categories_picker",
                schema: "community_life",
                table: "community_life_event_categories",
                columns: new[] { "IsActive", "Order" });

            migrationBuilder.CreateIndex(
                name: "ix_community_life_event_links_resource",
                schema: "community_life",
                table: "community_life_event_links",
                column: "ResourceId");

            migrationBuilder.CreateIndex(
                name: "ix_community_life_event_media_asset",
                schema: "community_life",
                table: "community_life_event_media",
                column: "MediaId");

            migrationBuilder.CreateIndex(
                name: "IX_community_life_event_notes_CommunityEventId",
                schema: "community_life",
                table: "community_life_event_notes",
                column: "CommunityEventId");

            migrationBuilder.CreateIndex(
                name: "ix_community_life_event_providers_provider",
                schema: "community_life",
                table: "community_life_event_providers",
                column: "ProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_community_life_event_speakers_CommunityEventId",
                schema: "community_life",
                table: "community_life_event_speakers",
                column: "CommunityEventId");

            migrationBuilder.CreateIndex(
                name: "ix_community_life_events_area_path",
                schema: "community_life",
                table: "community_life_events",
                column: "area_path");

            migrationBuilder.CreateIndex(
                name: "ix_community_life_events_audience",
                schema: "community_life",
                table: "community_life_events",
                columns: new[] { "is_free", "family_friendly", "student_focused" });

            migrationBuilder.CreateIndex(
                name: "ix_community_life_events_category",
                schema: "community_life",
                table: "community_life_events",
                column: "CategoryKey");

            migrationBuilder.CreateIndex(
                name: "ix_community_life_events_coordinate",
                schema: "community_life",
                table: "community_life_events",
                columns: new[] { "latitude", "longitude" });

            migrationBuilder.CreateIndex(
                name: "ix_community_life_events_country_city",
                schema: "community_life",
                table: "community_life_events",
                columns: new[] { "country_code", "city_name" });

            migrationBuilder.CreateIndex(
                name: "ix_community_life_events_discovery",
                schema: "community_life",
                table: "community_life_events",
                columns: new[] { "Status", "Visibility", "ModerationState" });

            migrationBuilder.CreateIndex(
                name: "ix_community_life_events_ends_at",
                schema: "community_life",
                table: "community_life_events",
                column: "ends_at");

            migrationBuilder.CreateIndex(
                name: "ix_community_life_events_going_count",
                schema: "community_life",
                table: "community_life_events",
                column: "going_count");

            migrationBuilder.CreateIndex(
                name: "ix_community_life_events_neighborhood",
                schema: "community_life",
                table: "community_life_events",
                column: "neighborhood_area_id");

            migrationBuilder.CreateIndex(
                name: "ix_community_life_events_organization_status",
                schema: "community_life",
                table: "community_life_events",
                columns: new[] { "OrganizationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_community_life_events_organizer_status",
                schema: "community_life",
                table: "community_life_events",
                columns: new[] { "OrganizerUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_community_life_events_published_at",
                schema: "community_life",
                table: "community_life_events",
                column: "PublishedAt");

            migrationBuilder.CreateIndex(
                name: "ix_community_life_events_report_count",
                schema: "community_life",
                table: "community_life_events",
                column: "ReportCount");

            migrationBuilder.CreateIndex(
                name: "ix_community_life_events_slug",
                schema: "community_life",
                table: "community_life_events",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_community_life_events_starts_at",
                schema: "community_life",
                table: "community_life_events",
                column: "starts_at");

            migrationBuilder.CreateIndex(
                name: "ix_community_life_events_title",
                schema: "community_life",
                table: "community_life_events",
                column: "title");

            migrationBuilder.CreateIndex(
                name: "IX_community_life_membership_history_OrganizationMembershipId",
                schema: "community_life",
                table: "community_life_membership_history",
                column: "OrganizationMembershipId");

            migrationBuilder.CreateIndex(
                name: "ix_community_life_memberships_member",
                schema: "community_life",
                table: "community_life_memberships",
                columns: new[] { "MemberUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_community_life_memberships_renews_at",
                schema: "community_life",
                table: "community_life_memberships",
                column: "RenewsAt");

            migrationBuilder.CreateIndex(
                name: "ix_community_life_memberships_roster",
                schema: "community_life",
                table: "community_life_memberships",
                columns: new[] { "OrganizationId", "Status", "Role" });

            migrationBuilder.CreateIndex(
                name: "ix_community_life_memberships_unique",
                schema: "community_life",
                table: "community_life_memberships",
                columns: new[] { "OrganizationId", "MemberUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_community_life_organization_analytics_organization",
                schema: "community_life",
                table: "community_life_organization_analytics",
                column: "OrganizationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_community_life_organization_analytics_type",
                schema: "community_life",
                table: "community_life_organization_analytics",
                columns: new[] { "CountryCode", "OrganizationTypeKey" });

            migrationBuilder.CreateIndex(
                name: "IX_community_life_organization_hours_OrganizationId",
                schema: "community_life",
                table: "community_life_organization_hours",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "ix_community_life_organization_languages_name",
                schema: "community_life",
                table: "community_life_organization_languages",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_community_life_organization_leaders_OrganizationId",
                schema: "community_life",
                table: "community_life_organization_leaders",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "ix_community_life_organization_media_asset",
                schema: "community_life",
                table: "community_life_organization_media",
                column: "MediaId");

            migrationBuilder.CreateIndex(
                name: "IX_community_life_organization_notes_OrganizationId",
                schema: "community_life",
                table: "community_life_organization_notes",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "ix_community_life_organization_types_key",
                schema: "community_life",
                table: "community_life_organization_types",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_community_life_organization_types_picker",
                schema: "community_life",
                table: "community_life_organization_types",
                columns: new[] { "IsActive", "Order" });

            migrationBuilder.CreateIndex(
                name: "ix_community_life_organizations_activity",
                schema: "community_life",
                table: "community_life_organizations",
                column: "LastActivityAt");

            migrationBuilder.CreateIndex(
                name: "ix_community_life_organizations_area_path",
                schema: "community_life",
                table: "community_life_organizations",
                column: "area_path");

            migrationBuilder.CreateIndex(
                name: "ix_community_life_organizations_country_city",
                schema: "community_life",
                table: "community_life_organizations",
                columns: new[] { "country_code", "city_name" });

            migrationBuilder.CreateIndex(
                name: "ix_community_life_organizations_creator_status",
                schema: "community_life",
                table: "community_life_organizations",
                columns: new[] { "CreatedByUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_community_life_organizations_discovery",
                schema: "community_life",
                table: "community_life_organizations",
                columns: new[] { "Status", "Visibility", "ModerationState" });

            migrationBuilder.CreateIndex(
                name: "ix_community_life_organizations_member_count",
                schema: "community_life",
                table: "community_life_organizations",
                column: "member_count");

            migrationBuilder.CreateIndex(
                name: "ix_community_life_organizations_name",
                schema: "community_life",
                table: "community_life_organizations",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "ix_community_life_organizations_neighborhood",
                schema: "community_life",
                table: "community_life_organizations",
                column: "neighborhood_area_id");

            migrationBuilder.CreateIndex(
                name: "ix_community_life_organizations_provider",
                schema: "community_life",
                table: "community_life_organizations",
                column: "ProviderId");

            migrationBuilder.CreateIndex(
                name: "ix_community_life_organizations_published_at",
                schema: "community_life",
                table: "community_life_organizations",
                column: "PublishedAt");

            migrationBuilder.CreateIndex(
                name: "ix_community_life_organizations_report_count",
                schema: "community_life",
                table: "community_life_organizations",
                column: "ReportCount");

            migrationBuilder.CreateIndex(
                name: "ix_community_life_organizations_slug",
                schema: "community_life",
                table: "community_life_organizations",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_community_life_organizations_type",
                schema: "community_life",
                table: "community_life_organizations",
                column: "OrganizationTypeKey");

            migrationBuilder.CreateIndex(
                name: "ix_community_life_organizations_verification",
                schema: "community_life",
                table: "community_life_organizations",
                column: "verification_id");

            migrationBuilder.CreateIndex(
                name: "ix_community_life_reports_queue",
                schema: "community_life",
                table: "community_life_reports",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_community_life_reports_subject",
                schema: "community_life",
                table: "community_life_reports",
                columns: new[] { "SubjectType", "SubjectId" });

            migrationBuilder.CreateIndex(
                name: "ix_community_life_reports_unique",
                schema: "community_life",
                table: "community_life_reports",
                columns: new[] { "SubjectType", "SubjectId", "ReportedByUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_community_life_rsvp_history_EventRsvpId",
                schema: "community_life",
                table: "community_life_rsvp_history",
                column: "EventRsvpId");

            migrationBuilder.CreateIndex(
                name: "ix_community_life_rsvps_attendee",
                schema: "community_life",
                table: "community_life_rsvps",
                columns: new[] { "AttendeeUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_community_life_rsvps_event_status",
                schema: "community_life",
                table: "community_life_rsvps",
                columns: new[] { "EventId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_community_life_rsvps_organization",
                schema: "community_life",
                table: "community_life_rsvps",
                columns: new[] { "OrganizationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_community_life_rsvps_unique",
                schema: "community_life",
                table: "community_life_rsvps",
                columns: new[] { "EventId", "AttendeeUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_community_life_rsvps_waitlist",
                schema: "community_life",
                table: "community_life_rsvps",
                columns: new[] { "EventId", "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "community_life_event_agenda",
                schema: "community_life");

            migrationBuilder.DropTable(
                name: "community_life_event_analytics",
                schema: "community_life");

            migrationBuilder.DropTable(
                name: "community_life_event_categories",
                schema: "community_life");

            migrationBuilder.DropTable(
                name: "community_life_event_guides",
                schema: "community_life");

            migrationBuilder.DropTable(
                name: "community_life_event_links",
                schema: "community_life");

            migrationBuilder.DropTable(
                name: "community_life_event_media",
                schema: "community_life");

            migrationBuilder.DropTable(
                name: "community_life_event_notes",
                schema: "community_life");

            migrationBuilder.DropTable(
                name: "community_life_event_providers",
                schema: "community_life");

            migrationBuilder.DropTable(
                name: "community_life_event_speakers",
                schema: "community_life");

            migrationBuilder.DropTable(
                name: "community_life_membership_history",
                schema: "community_life");

            migrationBuilder.DropTable(
                name: "community_life_organization_analytics",
                schema: "community_life");

            migrationBuilder.DropTable(
                name: "community_life_organization_guides",
                schema: "community_life");

            migrationBuilder.DropTable(
                name: "community_life_organization_hours",
                schema: "community_life");

            migrationBuilder.DropTable(
                name: "community_life_organization_languages",
                schema: "community_life");

            migrationBuilder.DropTable(
                name: "community_life_organization_leaders",
                schema: "community_life");

            migrationBuilder.DropTable(
                name: "community_life_organization_media",
                schema: "community_life");

            migrationBuilder.DropTable(
                name: "community_life_organization_notes",
                schema: "community_life");

            migrationBuilder.DropTable(
                name: "community_life_organization_services",
                schema: "community_life");

            migrationBuilder.DropTable(
                name: "community_life_organization_types",
                schema: "community_life");

            migrationBuilder.DropTable(
                name: "community_life_reports",
                schema: "community_life");

            migrationBuilder.DropTable(
                name: "community_life_rsvp_history",
                schema: "community_life");

            migrationBuilder.DropTable(
                name: "community_life_events",
                schema: "community_life");

            migrationBuilder.DropTable(
                name: "community_life_memberships",
                schema: "community_life");

            migrationBuilder.DropTable(
                name: "community_life_organizations",
                schema: "community_life");

            migrationBuilder.DropTable(
                name: "community_life_rsvps",
                schema: "community_life");
        }
    }
}
