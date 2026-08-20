using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diaspora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class JobsModuleInitial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "jobs");

            migrationBuilder.CreateTable(
                name: "job_alerts",
                schema: "jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    criteria_keywords = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    criteria_countries = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    criteria_category = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    criteria_skills = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    criteria_workplace_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    criteria_job_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    criteria_minimum_experience = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    criteria_visa_sponsorship_only = table.Column<bool>(type: "boolean", nullable: false),
                    criteria_verified_employers_only = table.Column<bool>(type: "boolean", nullable: false),
                    criteria_minimum_salary = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    criteria_salary_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    Frequency = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LastSweptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastNotifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TotalMatchCount = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_job_alerts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "job_analytics",
                schema: "jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PostingId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployerId = table.Column<Guid>(type: "uuid", nullable: false),
                    SearchImpressions = table.Column<long>(type: "bigint", nullable: false),
                    SearchClicks = table.Column<long>(type: "bigint", nullable: false),
                    ViewCount = table.Column<long>(type: "bigint", nullable: false),
                    UniqueViewCount = table.Column<long>(type: "bigint", nullable: false),
                    ApplicationStarts = table.Column<long>(type: "bigint", nullable: false),
                    ApplicationCount = table.Column<int>(type: "integer", nullable: false),
                    WithdrawnCount = table.Column<int>(type: "integer", nullable: false),
                    ShortlistedCount = table.Column<int>(type: "integer", nullable: false),
                    InterviewedCount = table.Column<int>(type: "integer", nullable: false),
                    OfferedCount = table.Column<int>(type: "integer", nullable: false),
                    HiredCount = table.Column<int>(type: "integer", nullable: false),
                    RejectedCount = table.Column<int>(type: "integer", nullable: false),
                    SaveCount = table.Column<int>(type: "integer", nullable: false),
                    ShareCount = table.Column<long>(type: "bigint", nullable: false),
                    TotalResponseMinutes = table.Column<long>(type: "bigint", nullable: false),
                    RespondedCount = table.Column<int>(type: "integer", nullable: false),
                    TotalTimeToHireHours = table.Column<long>(type: "bigint", nullable: false),
                    ApplicantCountryTally = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_analytics", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "job_applications",
                schema: "jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PostingId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployerId = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicantUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResumeId = table.Column<Guid>(type: "uuid", nullable: true),
                    CoverLetter = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    StageKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastActivityAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FirstViewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FirstRespondedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    WithdrawnAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DecisionReason = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
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
                    table.PrimaryKey("PK_job_applications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "job_candidate_profiles",
                schema: "jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    headline = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    summary = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    CityName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Visibility = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IsOpenToWork = table.Column<bool>(type: "boolean", nullable: false),
                    RemotePreference = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    expected_salary_minimum = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    expected_salary_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    expected_salary_period = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    expected_salary_flexible = table.Column<bool>(type: "boolean", nullable: false),
                    ExperienceLevel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    HighestEducation = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    NoticePeriodDays = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_job_candidate_profiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "job_categories",
                schema: "jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ParentKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    IconKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    PostingCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "job_employer_profiles",
                schema: "jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    legal_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    registration_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    organization_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    company_size = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    founded_year = table.Column<int>(type: "integer", nullable: true),
                    slug = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false),
                    PrimaryCountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    Description = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    contact_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    contact_phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    website_url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    careers_url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    logo_media_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cover_media_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tagline = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    verification_state = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    verification_id = table.Column<Guid>(type: "uuid", nullable: true),
                    verification_badge = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    verification_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    verification_changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    responded_count = table.Column<int>(type: "integer", nullable: false),
                    total_response_minutes = table.Column<long>(type: "bigint", nullable: false),
                    decided_count = table.Column<int>(type: "integer", nullable: false),
                    ignored_count = table.Column<int>(type: "integer", nullable: false),
                    offers_extended = table.Column<int>(type: "integer", nullable: false),
                    offers_accepted = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_job_employer_profiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "job_hiring_pipelines",
                schema: "jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PostingId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_hiring_pipelines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "job_interviews",
                schema: "jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PostingId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployerId = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    scheduled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    time_zone_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    mode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    location_address = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    joining_url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    instructions = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    RoundNumber = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IsCancelled = table.Column<bool>(type: "boolean", nullable: false),
                    CancelledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancellationReason = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Feedback = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CandidateFeedback = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    RescheduleCount = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_job_interviews", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "job_offers",
                schema: "jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PostingId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployerId = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    salary_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    salary_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    salary_period = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    benefits_summary = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    includes_relocation = table.Column<bool>(type: "boolean", nullable: false),
                    includes_visa_sponsorship = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ExtendedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AnsweredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AnsweredBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ResponseReason = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    SupersedesOfferId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_job_offers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "job_postings",
                schema: "jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployerId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    slug = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false),
                    description = table.Column<string>(type: "character varying(30000)", maxLength: 30000, nullable: false),
                    summary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CategoryKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    JobType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ExperienceLevel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    EducationLevel = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    country_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    region_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    city_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    area_id = table.Column<Guid>(type: "uuid", nullable: true),
                    area_path = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    workplace_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    offers_visa_sponsorship = table.Column<bool>(type: "boolean", nullable: false),
                    offers_relocation_assistance = table.Column<bool>(type: "boolean", nullable: false),
                    on_site_days_per_week = table.Column<int>(type: "integer", nullable: true),
                    remote_policy = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    salary_disclosed = table.Column<bool>(type: "boolean", nullable: false),
                    salary_minimum = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    salary_maximum = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    salary_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    salary_period = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    salary_negotiable = table.Column<bool>(type: "boolean", nullable: false),
                    VacancyCount = table.Column<int>(type: "integer", nullable: false),
                    FilledCount = table.Column<int>(type: "integer", nullable: false),
                    RequiresResume = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Visibility = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ModerationState = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IsFeatured = table.Column<bool>(type: "boolean", nullable: false),
                    FeaturedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FirstPublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClosingAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FilledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ArchivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClosureReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    verification_state = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    verification_id = table.Column<Guid>(type: "uuid", nullable: true),
                    verification_badge = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    verification_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    verification_changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    view_count = table.Column<long>(type: "bigint", nullable: false),
                    unique_view_count = table.Column<long>(type: "bigint", nullable: false),
                    application_count = table.Column<int>(type: "integer", nullable: false),
                    save_count = table.Column<int>(type: "integer", nullable: false),
                    share_count = table.Column<long>(type: "bigint", nullable: false),
                    search_impressions = table.Column<long>(type: "bigint", nullable: false),
                    search_clicks = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_job_postings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "job_resumes",
                schema: "jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    MediaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    FileName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    SizeInBytes = table.Column<long>(type: "bigint", nullable: true),
                    ContentType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    UseCount = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_job_resumes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "job_saved_jobs",
                schema: "jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PostingId = table.Column<Guid>(type: "uuid", nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RemindBeforeClosing = table.Column<bool>(type: "boolean", nullable: false),
                    ReminderSentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_saved_jobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "job_skills",
                schema: "jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CategoryKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    UsageCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_skills", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "job_application_answers",
                schema: "jobs",
                columns: table => new
                {
                    QuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Answer = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_application_answers", x => new { x.application_id, x.QuestionId });
                    table.ForeignKey(
                        name: "FK_job_application_answers_job_applications_application_id",
                        column: x => x.application_id,
                        principalSchema: "jobs",
                        principalTable: "job_applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "job_application_attachments",
                schema: "jobs",
                columns: table => new
                {
                    AttachmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    MediaId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    JobApplicationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_application_attachments", x => x.AttachmentId);
                    table.ForeignKey(
                        name: "FK_job_application_attachments_job_applications_JobApplication~",
                        column: x => x.JobApplicationId,
                        principalSchema: "jobs",
                        principalTable: "job_applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "job_application_history",
                schema: "jobs",
                columns: table => new
                {
                    EntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    FromStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ToStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: true),
                    Note = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    IsVisibleToCandidate = table.Column<bool>(type: "boolean", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    JobApplicationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_application_history", x => x.EntryId);
                    table.ForeignKey(
                        name: "FK_job_application_history_job_applications_JobApplicationId",
                        column: x => x.JobApplicationId,
                        principalSchema: "jobs",
                        principalTable: "job_applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "job_candidate_certifications",
                schema: "jobs",
                columns: table => new
                {
                    EntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Issuer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CredentialId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    IssuedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    ExpiresOn = table.Column<DateOnly>(type: "date", nullable: true),
                    MediaId = table.Column<Guid>(type: "uuid", nullable: true),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    CandidateProfileId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_candidate_certifications", x => x.EntryId);
                    table.ForeignKey(
                        name: "FK_job_candidate_certifications_job_candidate_profiles_Candida~",
                        column: x => x.CandidateProfileId,
                        principalSchema: "jobs",
                        principalTable: "job_candidate_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "job_candidate_education",
                schema: "jobs",
                columns: table => new
                {
                    EntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Institution = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Qualification = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    FieldOfStudy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Level = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    period_start = table.Column<DateOnly>(type: "date", nullable: false),
                    period_end = table.Column<DateOnly>(type: "date", nullable: true),
                    period_is_current = table.Column<bool>(type: "boolean", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    CandidateProfileId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_candidate_education", x => x.EntryId);
                    table.ForeignKey(
                        name: "FK_job_candidate_education_job_candidate_profiles_CandidatePro~",
                        column: x => x.CandidateProfileId,
                        principalSchema: "jobs",
                        principalTable: "job_candidate_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "job_candidate_experience",
                schema: "jobs",
                columns: table => new
                {
                    EntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Organization = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    CityName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    period_start = table.Column<DateOnly>(type: "date", nullable: false),
                    period_end = table.Column<DateOnly>(type: "date", nullable: true),
                    period_is_current = table.Column<bool>(type: "boolean", nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    CandidateProfileId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_candidate_experience", x => x.EntryId);
                    table.ForeignKey(
                        name: "FK_job_candidate_experience_job_candidate_profiles_CandidatePr~",
                        column: x => x.CandidateProfileId,
                        principalSchema: "jobs",
                        principalTable: "job_candidate_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "job_candidate_languages",
                schema: "jobs",
                columns: table => new
                {
                    Code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    Proficiency = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_candidate_languages", x => new { x.candidate_id, x.Code });
                    table.ForeignKey(
                        name: "FK_job_candidate_languages_job_candidate_profiles_candidate_id",
                        column: x => x.candidate_id,
                        principalSchema: "jobs",
                        principalTable: "job_candidate_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "job_candidate_portfolio",
                schema: "jobs",
                columns: table => new
                {
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    MediaId = table.Column<Guid>(type: "uuid", nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    CandidateProfileId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_candidate_portfolio", x => x.ItemId);
                    table.ForeignKey(
                        name: "FK_job_candidate_portfolio_job_candidate_profiles_CandidatePro~",
                        column: x => x.CandidateProfileId,
                        principalSchema: "jobs",
                        principalTable: "job_candidate_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "job_candidate_preferred_locations",
                schema: "jobs",
                columns: table => new
                {
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    CityName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    WillRelocate = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_candidate_preferred_locations", x => new { x.candidate_id, x.CountryCode, x.Order });
                    table.ForeignKey(
                        name: "FK_job_candidate_preferred_locations_job_candidate_profiles_ca~",
                        column: x => x.candidate_id,
                        principalSchema: "jobs",
                        principalTable: "job_candidate_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "job_candidate_skills",
                schema: "jobs",
                columns: table => new
                {
                    SkillKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Proficiency = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    YearsOfExperience = table.Column<int>(type: "integer", nullable: true),
                    Order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_candidate_skills", x => new { x.candidate_id, x.SkillKey });
                    table.ForeignKey(
                        name: "FK_job_candidate_skills_job_candidate_profiles_candidate_id",
                        column: x => x.candidate_id,
                        principalSchema: "jobs",
                        principalTable: "job_candidate_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "job_candidate_work_authorizations",
                schema: "jobs",
                columns: table => new
                {
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ExpiresOn = table.Column<DateOnly>(type: "date", nullable: true),
                    Order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_candidate_work_authorizations", x => new { x.candidate_id, x.CountryCode });
                    table.ForeignKey(
                        name: "FK_job_candidate_work_authorizations_job_candidate_profiles_ca~",
                        column: x => x.candidate_id,
                        principalSchema: "jobs",
                        principalTable: "job_candidate_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "job_employer_hiring_regions",
                schema: "jobs",
                columns: table => new
                {
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    employer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    SponsorsVisas = table.Column<bool>(type: "boolean", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_employer_hiring_regions", x => new { x.employer_id, x.CountryCode });
                    table.ForeignKey(
                        name: "FK_job_employer_hiring_regions_job_employer_profiles_employer_~",
                        column: x => x.employer_id,
                        principalSchema: "jobs",
                        principalTable: "job_employer_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "job_employer_members",
                schema: "jobs",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    employer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    JoinedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_employer_members", x => new { x.employer_id, x.UserId });
                    table.ForeignKey(
                        name: "FK_job_employer_members_job_employer_profiles_employer_id",
                        column: x => x.employer_id,
                        principalSchema: "jobs",
                        principalTable: "job_employer_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "job_hiring_stages",
                schema: "jobs",
                columns: table => new
                {
                    Key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    pipeline_id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    IsRemovable = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_hiring_stages", x => new { x.pipeline_id, x.Key });
                    table.ForeignKey(
                        name: "FK_job_hiring_stages_job_hiring_pipelines_pipeline_id",
                        column: x => x.pipeline_id,
                        principalSchema: "jobs",
                        principalTable: "job_hiring_pipelines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "job_interview_participants",
                schema: "jobs",
                columns: table => new
                {
                    ParticipantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Role = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    InterviewId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_interview_participants", x => x.ParticipantId);
                    table.ForeignKey(
                        name: "FK_job_interview_participants_job_interviews_InterviewId",
                        column: x => x.InterviewId,
                        principalSchema: "jobs",
                        principalTable: "job_interviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "job_detail_lines",
                schema: "jobs",
                columns: table => new
                {
                    LineId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    IsEssential = table.Column<bool>(type: "boolean", nullable: false),
                    JobPostingId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_detail_lines", x => x.LineId);
                    table.ForeignKey(
                        name: "FK_job_detail_lines_job_postings_JobPostingId",
                        column: x => x.JobPostingId,
                        principalSchema: "jobs",
                        principalTable: "job_postings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "job_guide_references",
                schema: "jobs",
                columns: table => new
                {
                    GuideId = table.Column<Guid>(type: "uuid", nullable: false),
                    posting_id = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    IsAutomatic = table.Column<bool>(type: "boolean", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_guide_references", x => new { x.posting_id, x.GuideId });
                    table.ForeignKey(
                        name: "FK_job_guide_references_job_postings_posting_id",
                        column: x => x.posting_id,
                        principalSchema: "jobs",
                        principalTable: "job_postings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "job_media_references",
                schema: "jobs",
                columns: table => new
                {
                    MediaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    posting_id = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    Caption = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_media_references", x => new { x.posting_id, x.MediaId, x.Role });
                    table.ForeignKey(
                        name: "FK_job_media_references_job_postings_posting_id",
                        column: x => x.posting_id,
                        principalSchema: "jobs",
                        principalTable: "job_postings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "job_moderation_notes",
                schema: "jobs",
                columns: table => new
                {
                    NoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    JobPostingId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_moderation_notes", x => x.NoteId);
                    table.ForeignKey(
                        name: "FK_job_moderation_notes_job_postings_JobPostingId",
                        column: x => x.JobPostingId,
                        principalSchema: "jobs",
                        principalTable: "job_postings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "job_provider_references",
                schema: "jobs",
                columns: table => new
                {
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: false),
                    posting_id = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_provider_references", x => new { x.posting_id, x.ProviderId });
                    table.ForeignKey(
                        name: "FK_job_provider_references_job_postings_posting_id",
                        column: x => x.posting_id,
                        principalSchema: "jobs",
                        principalTable: "job_postings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "job_screening_questions",
                schema: "jobs",
                columns: table => new
                {
                    QuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    JobPostingId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_screening_questions", x => x.QuestionId);
                    table.ForeignKey(
                        name: "FK_job_screening_questions_job_postings_JobPostingId",
                        column: x => x.JobPostingId,
                        principalSchema: "jobs",
                        principalTable: "job_postings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "job_skill_requirements",
                schema: "jobs",
                columns: table => new
                {
                    SkillKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    posting_id = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    MinimumProficiency = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IsEssential = table.Column<bool>(type: "boolean", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_skill_requirements", x => new { x.posting_id, x.SkillKey });
                    table.ForeignKey(
                        name: "FK_job_skill_requirements_job_postings_posting_id",
                        column: x => x.posting_id,
                        principalSchema: "jobs",
                        principalTable: "job_postings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "job_tags",
                schema: "jobs",
                columns: table => new
                {
                    Slug = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    posting_id = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_tags", x => new { x.posting_id, x.Slug });
                    table.ForeignKey(
                        name: "FK_job_tags_job_postings_posting_id",
                        column: x => x.posting_id,
                        principalSchema: "jobs",
                        principalTable: "job_postings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "job_skill_aliases",
                schema: "jobs",
                columns: table => new
                {
                    AliasKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    skill_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_skill_aliases", x => new { x.skill_id, x.AliasKey });
                    table.ForeignKey(
                        name: "FK_job_skill_aliases_job_skills_skill_id",
                        column: x => x.skill_id,
                        principalSchema: "jobs",
                        principalTable: "job_skills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_job_alerts_due",
                schema: "jobs",
                table: "job_alerts",
                columns: new[] { "IsActive", "LastSweptAt" });

            migrationBuilder.CreateIndex(
                name: "ix_job_alerts_user",
                schema: "jobs",
                table: "job_alerts",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "ix_job_analytics_employer",
                schema: "jobs",
                table: "job_analytics",
                column: "EmployerId");

            migrationBuilder.CreateIndex(
                name: "ux_job_analytics_posting",
                schema: "jobs",
                table: "job_analytics",
                column: "PostingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_job_application_attachments_JobApplicationId",
                schema: "jobs",
                table: "job_application_attachments",
                column: "JobApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_job_application_history_JobApplicationId",
                schema: "jobs",
                table: "job_application_history",
                column: "JobApplicationId");

            migrationBuilder.CreateIndex(
                name: "ix_job_application_history_occurred",
                schema: "jobs",
                table: "job_application_history",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "ix_job_applications_applicant",
                schema: "jobs",
                table: "job_applications",
                columns: new[] { "ApplicantUserId", "SubmittedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_job_applications_employer_status",
                schema: "jobs",
                table: "job_applications",
                columns: new[] { "EmployerId", "Status", "SubmittedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_job_applications_last_activity",
                schema: "jobs",
                table: "job_applications",
                column: "LastActivityAt");

            migrationBuilder.CreateIndex(
                name: "ix_job_applications_posting_status",
                schema: "jobs",
                table: "job_applications",
                columns: new[] { "PostingId", "Status", "SubmittedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_job_applications_unanswered",
                schema: "jobs",
                table: "job_applications",
                columns: new[] { "FirstRespondedAt", "SubmittedAt" });

            migrationBuilder.CreateIndex(
                name: "ux_job_applications_candidate_posting",
                schema: "jobs",
                table: "job_applications",
                columns: new[] { "CandidateId", "PostingId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_job_candidate_certifications_CandidateProfileId",
                schema: "jobs",
                table: "job_candidate_certifications",
                column: "CandidateProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_job_candidate_education_CandidateProfileId",
                schema: "jobs",
                table: "job_candidate_education",
                column: "CandidateProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_job_candidate_experience_CandidateProfileId",
                schema: "jobs",
                table: "job_candidate_experience",
                column: "CandidateProfileId");

            migrationBuilder.CreateIndex(
                name: "ix_job_candidate_languages_code",
                schema: "jobs",
                table: "job_candidate_languages",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_job_candidate_portfolio_CandidateProfileId",
                schema: "jobs",
                table: "job_candidate_portfolio",
                column: "CandidateProfileId");

            migrationBuilder.CreateIndex(
                name: "ix_job_candidate_preferred_country",
                schema: "jobs",
                table: "job_candidate_preferred_locations",
                column: "CountryCode");

            migrationBuilder.CreateIndex(
                name: "ix_job_candidates_country_open",
                schema: "jobs",
                table: "job_candidate_profiles",
                columns: new[] { "CountryCode", "IsOpenToWork" });

            migrationBuilder.CreateIndex(
                name: "ix_job_candidates_visibility",
                schema: "jobs",
                table: "job_candidate_profiles",
                column: "Visibility");

            migrationBuilder.CreateIndex(
                name: "ux_job_candidates_user",
                schema: "jobs",
                table: "job_candidate_profiles",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_job_candidate_skills_key",
                schema: "jobs",
                table: "job_candidate_skills",
                column: "SkillKey");

            migrationBuilder.CreateIndex(
                name: "ix_job_candidate_work_auth_country_kind",
                schema: "jobs",
                table: "job_candidate_work_authorizations",
                columns: new[] { "CountryCode", "Kind" });

            migrationBuilder.CreateIndex(
                name: "ix_job_categories_active_order",
                schema: "jobs",
                table: "job_categories",
                columns: new[] { "IsActive", "Order" });

            migrationBuilder.CreateIndex(
                name: "ux_job_categories_key",
                schema: "jobs",
                table: "job_categories",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_job_detail_lines_JobPostingId",
                schema: "jobs",
                table: "job_detail_lines",
                column: "JobPostingId");

            migrationBuilder.CreateIndex(
                name: "ix_job_employer_regions_country",
                schema: "jobs",
                table: "job_employer_hiring_regions",
                column: "CountryCode");

            migrationBuilder.CreateIndex(
                name: "ix_job_employer_members_user",
                schema: "jobs",
                table: "job_employer_members",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "ix_job_employers_country",
                schema: "jobs",
                table: "job_employer_profiles",
                column: "PrimaryCountryCode");

            migrationBuilder.CreateIndex(
                name: "ix_job_employers_display_name",
                schema: "jobs",
                table: "job_employer_profiles",
                column: "display_name");

            migrationBuilder.CreateIndex(
                name: "ix_job_employers_organization_type",
                schema: "jobs",
                table: "job_employer_profiles",
                column: "organization_type");

            migrationBuilder.CreateIndex(
                name: "ix_job_employers_status",
                schema: "jobs",
                table: "job_employer_profiles",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "ix_job_employers_verification_id",
                schema: "jobs",
                table: "job_employer_profiles",
                column: "verification_id");

            migrationBuilder.CreateIndex(
                name: "ix_job_employers_verification_state",
                schema: "jobs",
                table: "job_employer_profiles",
                column: "verification_state");

            migrationBuilder.CreateIndex(
                name: "ux_job_employers_slug",
                schema: "jobs",
                table: "job_employer_profiles",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_job_pipelines_posting",
                schema: "jobs",
                table: "job_hiring_pipelines",
                column: "PostingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_job_interview_participants_InterviewId",
                schema: "jobs",
                table: "job_interview_participants",
                column: "InterviewId");

            migrationBuilder.CreateIndex(
                name: "ix_job_interviews_candidate",
                schema: "jobs",
                table: "job_interviews",
                column: "CandidateId");

            migrationBuilder.CreateIndex(
                name: "ix_job_interviews_employer",
                schema: "jobs",
                table: "job_interviews",
                column: "EmployerId");

            migrationBuilder.CreateIndex(
                name: "ix_job_interviews_scheduled_at",
                schema: "jobs",
                table: "job_interviews",
                column: "scheduled_at");

            migrationBuilder.CreateIndex(
                name: "ux_job_interviews_application_round",
                schema: "jobs",
                table: "job_interviews",
                columns: new[] { "ApplicationId", "RoundNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_job_moderation_notes_JobPostingId",
                schema: "jobs",
                table: "job_moderation_notes",
                column: "JobPostingId");

            migrationBuilder.CreateIndex(
                name: "ix_job_offers_application_status",
                schema: "jobs",
                table: "job_offers",
                columns: new[] { "ApplicationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_job_offers_candidate_status",
                schema: "jobs",
                table: "job_offers",
                columns: new[] { "CandidateId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_job_offers_employer_status",
                schema: "jobs",
                table: "job_offers",
                columns: new[] { "EmployerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_job_offers_expires_at",
                schema: "jobs",
                table: "job_offers",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_job_postings_application_count",
                schema: "jobs",
                table: "job_postings",
                column: "application_count");

            migrationBuilder.CreateIndex(
                name: "ix_job_postings_category",
                schema: "jobs",
                table: "job_postings",
                column: "CategoryKey");

            migrationBuilder.CreateIndex(
                name: "ix_job_postings_city",
                schema: "jobs",
                table: "job_postings",
                column: "city_name");

            migrationBuilder.CreateIndex(
                name: "ix_job_postings_closing_at",
                schema: "jobs",
                table: "job_postings",
                column: "ClosingAt");

            migrationBuilder.CreateIndex(
                name: "ix_job_postings_country",
                schema: "jobs",
                table: "job_postings",
                column: "country_code");

            migrationBuilder.CreateIndex(
                name: "ix_job_postings_employer_status",
                schema: "jobs",
                table: "job_postings",
                columns: new[] { "EmployerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_job_postings_featured",
                schema: "jobs",
                table: "job_postings",
                column: "IsFeatured");

            migrationBuilder.CreateIndex(
                name: "ix_job_postings_moderation_state",
                schema: "jobs",
                table: "job_postings",
                column: "ModerationState");

            migrationBuilder.CreateIndex(
                name: "ix_job_postings_published_at",
                schema: "jobs",
                table: "job_postings",
                column: "PublishedAt");

            migrationBuilder.CreateIndex(
                name: "ix_job_postings_salary",
                schema: "jobs",
                table: "job_postings",
                columns: new[] { "salary_currency", "salary_maximum" });

            migrationBuilder.CreateIndex(
                name: "ix_job_postings_status_published",
                schema: "jobs",
                table: "job_postings",
                columns: new[] { "Status", "PublishedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_job_postings_title",
                schema: "jobs",
                table: "job_postings",
                column: "title");

            migrationBuilder.CreateIndex(
                name: "ix_job_postings_verification_id",
                schema: "jobs",
                table: "job_postings",
                column: "verification_id");

            migrationBuilder.CreateIndex(
                name: "ix_job_postings_visa_sponsorship",
                schema: "jobs",
                table: "job_postings",
                column: "offers_visa_sponsorship");

            migrationBuilder.CreateIndex(
                name: "ix_job_postings_workplace_type",
                schema: "jobs",
                table: "job_postings",
                column: "workplace_type");

            migrationBuilder.CreateIndex(
                name: "ux_job_postings_slug",
                schema: "jobs",
                table: "job_postings",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_job_resumes_candidate_default",
                schema: "jobs",
                table: "job_resumes",
                columns: new[] { "CandidateId", "IsDefault" });

            migrationBuilder.CreateIndex(
                name: "ix_job_resumes_user",
                schema: "jobs",
                table: "job_resumes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "ix_job_saved_reminders",
                schema: "jobs",
                table: "job_saved_jobs",
                columns: new[] { "RemindBeforeClosing", "ReminderSentAt" });

            migrationBuilder.CreateIndex(
                name: "ix_job_saved_user_posting",
                schema: "jobs",
                table: "job_saved_jobs",
                columns: new[] { "UserId", "PostingId" });

            migrationBuilder.CreateIndex(
                name: "ux_job_saved_candidate_posting",
                schema: "jobs",
                table: "job_saved_jobs",
                columns: new[] { "CandidateId", "PostingId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_job_screening_questions_JobPostingId",
                schema: "jobs",
                table: "job_screening_questions",
                column: "JobPostingId");

            migrationBuilder.CreateIndex(
                name: "ux_job_skill_aliases_key",
                schema: "jobs",
                table: "job_skill_aliases",
                column: "AliasKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_job_skill_requirements_key",
                schema: "jobs",
                table: "job_skill_requirements",
                column: "SkillKey");

            migrationBuilder.CreateIndex(
                name: "ix_job_skills_active_usage",
                schema: "jobs",
                table: "job_skills",
                columns: new[] { "IsActive", "UsageCount" });

            migrationBuilder.CreateIndex(
                name: "ux_job_skills_key",
                schema: "jobs",
                table: "job_skills",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_job_tags_slug",
                schema: "jobs",
                table: "job_tags",
                column: "Slug");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "job_alerts",
                schema: "jobs");

            migrationBuilder.DropTable(
                name: "job_analytics",
                schema: "jobs");

            migrationBuilder.DropTable(
                name: "job_application_answers",
                schema: "jobs");

            migrationBuilder.DropTable(
                name: "job_application_attachments",
                schema: "jobs");

            migrationBuilder.DropTable(
                name: "job_application_history",
                schema: "jobs");

            migrationBuilder.DropTable(
                name: "job_candidate_certifications",
                schema: "jobs");

            migrationBuilder.DropTable(
                name: "job_candidate_education",
                schema: "jobs");

            migrationBuilder.DropTable(
                name: "job_candidate_experience",
                schema: "jobs");

            migrationBuilder.DropTable(
                name: "job_candidate_languages",
                schema: "jobs");

            migrationBuilder.DropTable(
                name: "job_candidate_portfolio",
                schema: "jobs");

            migrationBuilder.DropTable(
                name: "job_candidate_preferred_locations",
                schema: "jobs");

            migrationBuilder.DropTable(
                name: "job_candidate_skills",
                schema: "jobs");

            migrationBuilder.DropTable(
                name: "job_candidate_work_authorizations",
                schema: "jobs");

            migrationBuilder.DropTable(
                name: "job_categories",
                schema: "jobs");

            migrationBuilder.DropTable(
                name: "job_detail_lines",
                schema: "jobs");

            migrationBuilder.DropTable(
                name: "job_employer_hiring_regions",
                schema: "jobs");

            migrationBuilder.DropTable(
                name: "job_employer_members",
                schema: "jobs");

            migrationBuilder.DropTable(
                name: "job_guide_references",
                schema: "jobs");

            migrationBuilder.DropTable(
                name: "job_hiring_stages",
                schema: "jobs");

            migrationBuilder.DropTable(
                name: "job_interview_participants",
                schema: "jobs");

            migrationBuilder.DropTable(
                name: "job_media_references",
                schema: "jobs");

            migrationBuilder.DropTable(
                name: "job_moderation_notes",
                schema: "jobs");

            migrationBuilder.DropTable(
                name: "job_offers",
                schema: "jobs");

            migrationBuilder.DropTable(
                name: "job_provider_references",
                schema: "jobs");

            migrationBuilder.DropTable(
                name: "job_resumes",
                schema: "jobs");

            migrationBuilder.DropTable(
                name: "job_saved_jobs",
                schema: "jobs");

            migrationBuilder.DropTable(
                name: "job_screening_questions",
                schema: "jobs");

            migrationBuilder.DropTable(
                name: "job_skill_aliases",
                schema: "jobs");

            migrationBuilder.DropTable(
                name: "job_skill_requirements",
                schema: "jobs");

            migrationBuilder.DropTable(
                name: "job_tags",
                schema: "jobs");

            migrationBuilder.DropTable(
                name: "job_applications",
                schema: "jobs");

            migrationBuilder.DropTable(
                name: "job_candidate_profiles",
                schema: "jobs");

            migrationBuilder.DropTable(
                name: "job_employer_profiles",
                schema: "jobs");

            migrationBuilder.DropTable(
                name: "job_hiring_pipelines",
                schema: "jobs");

            migrationBuilder.DropTable(
                name: "job_interviews",
                schema: "jobs");

            migrationBuilder.DropTable(
                name: "job_skills",
                schema: "jobs");

            migrationBuilder.DropTable(
                name: "job_postings",
                schema: "jobs");
        }
    }
}
