using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diaspora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CountryIntelligenceInitial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "intel_authorities",
                schema: "guides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ShortName = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    AuthorityType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Website = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ContactEmail = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    ContactPhone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    IsVerified = table.Column<bool>(type: "boolean", nullable: false),
                    VerifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    VerificationCaseId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_intel_authorities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "intel_country_analytics",
                schema: "guides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    LastActivityAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_intel_country_analytics", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "intel_embassies",
                schema: "guides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    ServesCountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AuthorityId = table.Column<Guid>(type: "uuid", nullable: true),
                    City = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    Telephone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    EmergencyTelephone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    Website = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AppointmentUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RequiresAppointment = table.Column<bool>(type: "boolean", nullable: false),
                    OffersPassportServices = table.Column<bool>(type: "boolean", nullable: false),
                    OffersVisaServices = table.Column<bool>(type: "boolean", nullable: false),
                    OffersConsularServices = table.Column<bool>(type: "boolean", nullable: false),
                    OffersEmergencyAssistance = table.Column<bool>(type: "boolean", nullable: false),
                    MediaId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsVerified = table.Column<bool>(type: "boolean", nullable: false),
                    VerifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    VerificationCaseId = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_intel_embassies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "intel_emergency_directories",
                schema: "guides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    Guidance = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LastReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastReviewedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_intel_emergency_directories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "intel_notice_categories",
                schema: "guides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Icon = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    DefaultSeverity = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AlertFamily = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
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
                    table.PrimaryKey("PK_intel_notice_categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "intel_notices",
                schema: "guides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    Region = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    CategoryKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AuthorityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    Summary = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    Content = table.Column<string>(type: "character varying(40000)", maxLength: 40000, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Severity = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SeverityRank = table.Column<int>(type: "integer", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    SourceUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PublicationDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EffectiveFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ExpiredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastReviewedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    VerificationState = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    VerifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    VerifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    VerificationCaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    VerificationNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SupersededById = table.Column<Guid>(type: "uuid", nullable: true),
                    SupersededAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ViewCount = table.Column<long>(type: "bigint", nullable: false),
                    ClickThroughCount = table.Column<long>(type: "bigint", nullable: false),
                    CompletionCount = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_intel_notices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "intel_public_holidays",
                schema: "guides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Region = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    ObservedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ObservedUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    ClosesGovernmentOffices = table.Column<bool>(type: "boolean", nullable: false),
                    ClosesEmbassies = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_intel_public_holidays", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "intel_visa_advisories",
                schema: "guides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    AppliesToCountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    AuthorityId = table.Column<Guid>(type: "uuid", nullable: false),
                    VisaCategory = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Title = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    Summary = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    Content = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: true),
                    ProcessingTime = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    ApplicationUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OfficialReference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    SourceUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    EffectiveFrom = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastReviewedBy = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_intel_visa_advisories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "intel_country_counters",
                schema: "guides",
                columns: table => new
                {
                    Kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CountryIntelligenceAnalyticsId = table.Column<Guid>(type: "uuid", nullable: false),
                    Count = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_intel_country_counters", x => new { x.CountryIntelligenceAnalyticsId, x.Kind });
                    table.ForeignKey(
                        name: "FK_intel_country_counters_intel_country_analytics_CountryIntel~",
                        column: x => x.CountryIntelligenceAnalyticsId,
                        principalSchema: "guides",
                        principalTable: "intel_country_analytics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "intel_embassy_localizations",
                schema: "guides",
                columns: table => new
                {
                    Locale = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    EmbassyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_intel_embassy_localizations", x => new { x.EmbassyId, x.Locale });
                    table.ForeignKey(
                        name: "FK_intel_embassy_localizations_intel_embassies_EmbassyId",
                        column: x => x.EmbassyId,
                        principalSchema: "guides",
                        principalTable: "intel_embassies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "intel_embassy_office_hours",
                schema: "guides",
                columns: table => new
                {
                    Day = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    EmbassyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Hours = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsClosed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_intel_embassy_office_hours", x => new { x.EmbassyId, x.Day });
                    table.ForeignKey(
                        name: "FK_intel_embassy_office_hours_intel_embassies_EmbassyId",
                        column: x => x.EmbassyId,
                        principalSchema: "guides",
                        principalTable: "intel_embassies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "intel_embassy_services",
                schema: "guides",
                columns: table => new
                {
                    Key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EmbassyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RequiresAppointment = table.Column<bool>(type: "boolean", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_intel_embassy_services", x => new { x.EmbassyId, x.Key });
                    table.ForeignKey(
                        name: "FK_intel_embassy_services_intel_embassies_EmbassyId",
                        column: x => x.EmbassyId,
                        principalSchema: "guides",
                        principalTable: "intel_embassies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "intel_emergency_contact_localizations",
                schema: "guides",
                columns: table => new
                {
                    Locale = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CountryEmergencyDirectoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_intel_emergency_contact_localizations", x => new { x.CountryEmergencyDirectoryId, x.Locale, x.Kind });
                    table.ForeignKey(
                        name: "FK_intel_emergency_contact_localizations_intel_emergency_direc~",
                        column: x => x.CountryEmergencyDirectoryId,
                        principalSchema: "guides",
                        principalTable: "intel_emergency_directories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "intel_emergency_contacts",
                schema: "guides",
                columns: table => new
                {
                    Kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CountryEmergencyDirectoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    AlternateNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsAvailableAllHours = table.Column<bool>(type: "boolean", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_intel_emergency_contacts", x => new { x.CountryEmergencyDirectoryId, x.Kind });
                    table.ForeignKey(
                        name: "FK_intel_emergency_contacts_intel_emergency_directories_Countr~",
                        column: x => x.CountryEmergencyDirectoryId,
                        principalSchema: "guides",
                        principalTable: "intel_emergency_directories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "intel_notice_category_localizations",
                schema: "guides",
                columns: table => new
                {
                    Locale = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    NoticeCategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_intel_notice_category_localizations", x => new { x.NoticeCategoryId, x.Locale });
                    table.ForeignKey(
                        name: "FK_intel_notice_category_localizations_intel_notice_categories~",
                        column: x => x.NoticeCategoryId,
                        principalSchema: "guides",
                        principalTable: "intel_notice_categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "intel_notice_attachments",
                schema: "guides",
                columns: table => new
                {
                    MediaId = table.Column<Guid>(type: "uuid", nullable: false),
                    OfficialNoticeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Caption = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_intel_notice_attachments", x => new { x.OfficialNoticeId, x.MediaId });
                    table.ForeignKey(
                        name: "FK_intel_notice_attachments_intel_notices_OfficialNoticeId",
                        column: x => x.OfficialNoticeId,
                        principalSchema: "guides",
                        principalTable: "intel_notices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "intel_notice_languages",
                schema: "guides",
                columns: table => new
                {
                    Code = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    OfficialNoticeId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_intel_notice_languages", x => new { x.OfficialNoticeId, x.Code });
                    table.ForeignKey(
                        name: "FK_intel_notice_languages_intel_notices_OfficialNoticeId",
                        column: x => x.OfficialNoticeId,
                        principalSchema: "guides",
                        principalTable: "intel_notices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "intel_notice_localizations",
                schema: "guides",
                columns: table => new
                {
                    Locale = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    OfficialNoticeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    Summary = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    Content = table.Column<string>(type: "character varying(40000)", maxLength: 40000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_intel_notice_localizations", x => new { x.OfficialNoticeId, x.Locale });
                    table.ForeignKey(
                        name: "FK_intel_notice_localizations_intel_notices_OfficialNoticeId",
                        column: x => x.OfficialNoticeId,
                        principalSchema: "guides",
                        principalTable: "intel_notices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "intel_notice_references",
                schema: "guides",
                columns: table => new
                {
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    OfficialNoticeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_intel_notice_references", x => new { x.OfficialNoticeId, x.Kind, x.TargetId });
                    table.ForeignKey(
                        name: "FK_intel_notice_references_intel_notices_OfficialNoticeId",
                        column: x => x.OfficialNoticeId,
                        principalSchema: "guides",
                        principalTable: "intel_notices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "intel_public_holiday_localizations",
                schema: "guides",
                columns: table => new
                {
                    Locale = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    PublicHolidayId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_intel_public_holiday_localizations", x => new { x.PublicHolidayId, x.Locale });
                    table.ForeignKey(
                        name: "FK_intel_public_holiday_localizations_intel_public_holidays_Pu~",
                        column: x => x.PublicHolidayId,
                        principalSchema: "guides",
                        principalTable: "intel_public_holidays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "intel_visa_guides",
                schema: "guides",
                columns: table => new
                {
                    GuideId = table.Column<Guid>(type: "uuid", nullable: false),
                    VisaAdvisoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_intel_visa_guides", x => new { x.VisaAdvisoryId, x.GuideId });
                    table.ForeignKey(
                        name: "FK_intel_visa_guides_intel_visa_advisories_VisaAdvisoryId",
                        column: x => x.VisaAdvisoryId,
                        principalSchema: "guides",
                        principalTable: "intel_visa_advisories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "intel_visa_lines",
                schema: "guides",
                columns: table => new
                {
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    VisaAdvisoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_intel_visa_lines", x => new { x.VisaAdvisoryId, x.Kind, x.Position });
                    table.ForeignKey(
                        name: "FK_intel_visa_lines_intel_visa_advisories_VisaAdvisoryId",
                        column: x => x.VisaAdvisoryId,
                        principalSchema: "guides",
                        principalTable: "intel_visa_advisories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "intel_visa_localizations",
                schema: "guides",
                columns: table => new
                {
                    Locale = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    VisaAdvisoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    Summary = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    Content = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_intel_visa_localizations", x => new { x.VisaAdvisoryId, x.Locale });
                    table.ForeignKey(
                        name: "FK_intel_visa_localizations_intel_visa_advisories_VisaAdvisory~",
                        column: x => x.VisaAdvisoryId,
                        principalSchema: "guides",
                        principalTable: "intel_visa_advisories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_intel_authorities_country_verified",
                schema: "guides",
                table: "intel_authorities",
                columns: new[] { "CountryCode", "IsVerified" });

            migrationBuilder.CreateIndex(
                name: "ix_intel_authorities_verification_case",
                schema: "guides",
                table: "intel_authorities",
                column: "VerificationCaseId");

            migrationBuilder.CreateIndex(
                name: "ux_intel_country_analytics_country",
                schema: "guides",
                table: "intel_country_analytics",
                column: "CountryCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_intel_embassies_location",
                schema: "guides",
                table: "intel_embassies",
                columns: new[] { "CountryCode", "IsVerified" });

            migrationBuilder.CreateIndex(
                name: "ix_intel_embassies_serves",
                schema: "guides",
                table: "intel_embassies",
                columns: new[] { "ServesCountryCode", "IsVerified" });

            migrationBuilder.CreateIndex(
                name: "ix_intel_embassies_verification_case",
                schema: "guides",
                table: "intel_embassies",
                column: "VerificationCaseId");

            migrationBuilder.CreateIndex(
                name: "ux_intel_emergency_directories_country",
                schema: "guides",
                table: "intel_emergency_directories",
                column: "CountryCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_intel_notice_attachments_media",
                schema: "guides",
                table: "intel_notice_attachments",
                column: "MediaId");

            migrationBuilder.CreateIndex(
                name: "ix_intel_notice_categories_order",
                schema: "guides",
                table: "intel_notice_categories",
                column: "Order");

            migrationBuilder.CreateIndex(
                name: "ux_intel_notice_categories_key",
                schema: "guides",
                table: "intel_notice_categories",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_intel_notice_references_target",
                schema: "guides",
                table: "intel_notice_references",
                columns: new[] { "Kind", "TargetId" });

            migrationBuilder.CreateIndex(
                name: "ix_intel_notices_authority_status",
                schema: "guides",
                table: "intel_notices",
                columns: new[] { "AuthorityId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_intel_notices_country_category",
                schema: "guides",
                table: "intel_notices",
                columns: new[] { "CountryCode", "CategoryKey" });

            migrationBuilder.CreateIndex(
                name: "ix_intel_notices_country_status_severity",
                schema: "guides",
                table: "intel_notices",
                columns: new[] { "CountryCode", "Status", "SeverityRank" });

            migrationBuilder.CreateIndex(
                name: "ix_intel_notices_expiry_sweep",
                schema: "guides",
                table: "intel_notices",
                columns: new[] { "Status", "ExpiresAt", "ExpiredAt" });

            migrationBuilder.CreateIndex(
                name: "ix_intel_notices_published",
                schema: "guides",
                table: "intel_notices",
                column: "PublishedAt");

            migrationBuilder.CreateIndex(
                name: "ix_intel_public_holidays_country_date",
                schema: "guides",
                table: "intel_public_holidays",
                columns: new[] { "CountryCode", "ObservedOn" });

            migrationBuilder.CreateIndex(
                name: "ix_intel_public_holidays_country_year",
                schema: "guides",
                table: "intel_public_holidays",
                columns: new[] { "CountryCode", "Year" });

            migrationBuilder.CreateIndex(
                name: "ix_intel_visa_advisories_authority",
                schema: "guides",
                table: "intel_visa_advisories",
                column: "AuthorityId");

            migrationBuilder.CreateIndex(
                name: "ix_intel_visa_advisories_country_applies",
                schema: "guides",
                table: "intel_visa_advisories",
                columns: new[] { "CountryCode", "AppliesToCountryCode", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_intel_visa_guides_guide",
                schema: "guides",
                table: "intel_visa_guides",
                column: "GuideId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "intel_authorities",
                schema: "guides");

            migrationBuilder.DropTable(
                name: "intel_country_counters",
                schema: "guides");

            migrationBuilder.DropTable(
                name: "intel_embassy_localizations",
                schema: "guides");

            migrationBuilder.DropTable(
                name: "intel_embassy_office_hours",
                schema: "guides");

            migrationBuilder.DropTable(
                name: "intel_embassy_services",
                schema: "guides");

            migrationBuilder.DropTable(
                name: "intel_emergency_contact_localizations",
                schema: "guides");

            migrationBuilder.DropTable(
                name: "intel_emergency_contacts",
                schema: "guides");

            migrationBuilder.DropTable(
                name: "intel_notice_attachments",
                schema: "guides");

            migrationBuilder.DropTable(
                name: "intel_notice_category_localizations",
                schema: "guides");

            migrationBuilder.DropTable(
                name: "intel_notice_languages",
                schema: "guides");

            migrationBuilder.DropTable(
                name: "intel_notice_localizations",
                schema: "guides");

            migrationBuilder.DropTable(
                name: "intel_notice_references",
                schema: "guides");

            migrationBuilder.DropTable(
                name: "intel_public_holiday_localizations",
                schema: "guides");

            migrationBuilder.DropTable(
                name: "intel_visa_guides",
                schema: "guides");

            migrationBuilder.DropTable(
                name: "intel_visa_lines",
                schema: "guides");

            migrationBuilder.DropTable(
                name: "intel_visa_localizations",
                schema: "guides");

            migrationBuilder.DropTable(
                name: "intel_country_analytics",
                schema: "guides");

            migrationBuilder.DropTable(
                name: "intel_embassies",
                schema: "guides");

            migrationBuilder.DropTable(
                name: "intel_emergency_directories",
                schema: "guides");

            migrationBuilder.DropTable(
                name: "intel_notice_categories",
                schema: "guides");

            migrationBuilder.DropTable(
                name: "intel_notices",
                schema: "guides");

            migrationBuilder.DropTable(
                name: "intel_public_holidays",
                schema: "guides");

            migrationBuilder.DropTable(
                name: "intel_visa_advisories",
                schema: "guides");
        }
    }
}
