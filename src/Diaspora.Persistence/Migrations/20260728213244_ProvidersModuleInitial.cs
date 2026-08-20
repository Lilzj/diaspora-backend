using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diaspora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProvidersModuleInitial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "providers");

            migrationBuilder.CreateTable(
                name: "provider_recommendations",
                schema: "providers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecommenderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Skill = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    IsFeatured = table.Column<bool>(type: "boolean", nullable: false),
                    IsWithdrawn = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_provider_recommendations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "provider_reviews",
                schema: "providers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Rating = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    Body = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ServiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Response = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    RespondedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RespondedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    EditedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsEdited = table.Column<bool>(type: "boolean", nullable: false),
                    IsVerifiedInteraction = table.Column<bool>(type: "boolean", nullable: false),
                    HelpfulCount = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_provider_reviews", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "provider_service_categories",
                schema: "providers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    slug = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Icon = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    path = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    depth = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ProviderCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_provider_service_categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "providers",
                schema: "providers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    slug = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeliveryMode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    description = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    description_summary = table.Column<string>(type: "character varying(280)", maxLength: 280, nullable: false),
                    tagline = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    mission = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    founded_on = table.Column<int>(type: "integer", nullable: true),
                    cover_media_id = table.Column<Guid>(type: "uuid", nullable: true),
                    logo_media_id = table.Column<Guid>(type: "uuid", nullable: true),
                    typical_response_hours = table.Column<int>(type: "integer", nullable: true),
                    profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    registration_number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    registration_jurisdiction = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    registered_on = table.Column<int>(type: "integer", nullable: true),
                    tax_identifier = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    is_non_profit = table.Column<bool>(type: "boolean", nullable: true),
                    membership_size = table.Column<int>(type: "integer", nullable: true),
                    staff_size = table.Column<int>(type: "integer", nullable: true),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: true),
                    country_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    area_id = table.Column<Guid>(type: "uuid", nullable: true),
                    area_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    city_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    latitude = table.Column<double>(type: "double precision", nullable: true),
                    longitude = table.Column<double>(type: "double precision", nullable: true),
                    hierarchy_path = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    address_line = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    postal_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    visibility_level = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    visibility_country_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    visibility_area_id = table.Column<Guid>(type: "uuid", nullable: true),
                    IsFeatured = table.Column<bool>(type: "boolean", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    StatusChangedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    StatusReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    availability_kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    time_zone_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    accepts_walk_ins = table.Column<bool>(type: "boolean", nullable: false),
                    accepts_appointments = table.Column<bool>(type: "boolean", nullable: false),
                    offers_emergency_service = table.Column<bool>(type: "boolean", nullable: false),
                    offers_remote_service = table.Column<bool>(type: "boolean", nullable: false),
                    availability_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    view_count = table.Column<long>(type: "bigint", nullable: false),
                    contact_count = table.Column<long>(type: "bigint", nullable: false),
                    search_impression_count = table.Column<long>(type: "bigint", nullable: false),
                    bookmark_count = table.Column<long>(type: "bigint", nullable: false),
                    share_count = table.Column<long>(type: "bigint", nullable: false),
                    follower_count = table.Column<long>(type: "bigint", nullable: false),
                    post_count = table.Column<long>(type: "bigint", nullable: false),
                    service_count = table.Column<int>(type: "integer", nullable: false),
                    member_count = table.Column<int>(type: "integer", nullable: false),
                    review_count = table.Column<int>(type: "integer", nullable: false),
                    recommendation_count = table.Column<int>(type: "integer", nullable: false),
                    profile_completion_percent = table.Column<int>(type: "integer", nullable: false),
                    rating_1_count = table.Column<int>(type: "integer", nullable: false),
                    rating_2_count = table.Column<int>(type: "integer", nullable: false),
                    rating_3_count = table.Column<int>(type: "integer", nullable: false),
                    rating_4_count = table.Column<int>(type: "integer", nullable: false),
                    rating_5_count = table.Column<int>(type: "integer", nullable: false),
                    total_reviews = table.Column<int>(type: "integer", nullable: false),
                    average_rating = table.Column<double>(type: "double precision", nullable: false),
                    is_verified = table.Column<bool>(type: "boolean", nullable: false),
                    verification_state = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    verified_kind_count = table.Column<int>(type: "integer", nullable: false),
                    verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    verification_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_providers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "provider_category_aliases",
                schema: "providers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Normalized = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Source = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_provider_category_aliases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_provider_category_aliases_provider_service_categories_Categ~",
                        column: x => x.CategoryId,
                        principalSchema: "providers",
                        principalTable: "provider_service_categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "provider_category_keywords",
                schema: "providers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Value = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Normalized = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Weight = table.Column<int>(type: "integer", nullable: false),
                    LanguageCode = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: true),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_provider_category_keywords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_provider_category_keywords_provider_service_categories_Cate~",
                        column: x => x.CategoryId,
                        principalSchema: "providers",
                        principalTable: "provider_service_categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "provider_category_translations",
                schema: "providers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LanguageCode = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_provider_category_translations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_provider_category_translations_provider_service_categories_~",
                        column: x => x.CategoryId,
                        principalSchema: "providers",
                        principalTable: "provider_service_categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "provider_availability_exceptions",
                schema: "providers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    StartsOn = table.Column<int>(type: "integer", nullable: false),
                    EndsOn = table.Column<int>(type: "integer", nullable: false),
                    IsClosed = table.Column<bool>(type: "boolean", nullable: false),
                    OpensAt = table.Column<int>(type: "integer", nullable: true),
                    ClosesAt = table.Column<int>(type: "integer", nullable: true),
                    Reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_provider_availability_exceptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_provider_availability_exceptions_providers_ProviderId",
                        column: x => x.ProviderId,
                        principalSchema: "providers",
                        principalTable: "providers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "provider_certifications",
                schema: "providers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Issuer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Reference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    IssuedOn = table.Column<int>(type: "integer", nullable: true),
                    ExpiresOn = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    EvidenceMediaId = table.Column<Guid>(type: "uuid", nullable: true),
                    VerificationRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    VerifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_provider_certifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_provider_certifications_providers_ProviderId",
                        column: x => x.ProviderId,
                        principalSchema: "providers",
                        principalTable: "providers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "provider_contact_methods",
                schema: "providers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Value = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Label = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    IsPublic = table.Column<bool>(type: "boolean", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_provider_contact_methods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_provider_contact_methods_providers_ProviderId",
                        column: x => x.ProviderId,
                        principalSchema: "providers",
                        principalTable: "providers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "provider_gallery",
                schema: "providers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MediaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    Caption = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsClaimed = table.Column<bool>(type: "boolean", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_provider_gallery", x => x.Id);
                    table.ForeignKey(
                        name: "FK_provider_gallery_providers_ProviderId",
                        column: x => x.ProviderId,
                        principalSchema: "providers",
                        principalTable: "providers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "provider_languages",
                schema: "providers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    Proficiency = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_provider_languages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_provider_languages_providers_ProviderId",
                        column: x => x.ProviderId,
                        principalSchema: "providers",
                        principalTable: "providers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "provider_locations",
                schema: "providers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    country_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    area_id = table.Column<Guid>(type: "uuid", nullable: true),
                    area_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    city_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    latitude = table.Column<double>(type: "double precision", nullable: true),
                    longitude = table.Column<double>(type: "double precision", nullable: true),
                    hierarchy_path = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    address_line = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    postal_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    IsPubliclyVisible = table.Column<bool>(type: "boolean", nullable: false),
                    OperatingHoursNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PhoneNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_provider_locations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_provider_locations_providers_ProviderId",
                        column: x => x.ProviderId,
                        principalSchema: "providers",
                        principalTable: "providers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "provider_members",
                schema: "providers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    JoinedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    InvitedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    IsPubliclyListed = table.Column<bool>(type: "boolean", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_provider_members", x => x.Id);
                    table.ForeignKey(
                        name: "FK_provider_members_providers_ProviderId",
                        column: x => x.ProviderId,
                        principalSchema: "providers",
                        principalTable: "providers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "provider_operating_hours",
                schema: "providers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Day = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IsClosed = table.Column<bool>(type: "boolean", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_provider_operating_hours", x => x.Id);
                    table.ForeignKey(
                        name: "FK_provider_operating_hours_providers_ProviderId",
                        column: x => x.ProviderId,
                        principalSchema: "providers",
                        principalTable: "providers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "provider_service_areas",
                schema: "providers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    AreaId = table.Column<Guid>(type: "uuid", nullable: true),
                    HierarchyPath = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CentreLatitude = table.Column<double>(type: "double precision", nullable: true),
                    CentreLongitude = table.Column<double>(type: "double precision", nullable: true),
                    RadiusKm = table.Column<double>(type: "double precision", nullable: true),
                    TravelSurcharge = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    TravelSurchargeCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_provider_service_areas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_provider_service_areas_providers_ProviderId",
                        column: x => x.ProviderId,
                        principalSchema: "providers",
                        principalTable: "providers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "provider_services",
                schema: "providers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    pricing_model = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    price_min = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    price_max = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    price_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    price_unit = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    price_is_negotiable = table.Column<bool>(type: "boolean", nullable: false),
                    DeliveryMode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: true),
                    IsAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    IsFeatured = table.Column<bool>(type: "boolean", nullable: false),
                    ImageMediaId = table.Column<Guid>(type: "uuid", nullable: true),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_provider_services", x => x.Id);
                    table.ForeignKey(
                        name: "FK_provider_services_providers_ProviderId",
                        column: x => x.ProviderId,
                        principalSchema: "providers",
                        principalTable: "providers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "provider_tags",
                schema: "providers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Slug = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Source = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_provider_tags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_provider_tags_providers_ProviderId",
                        column: x => x.ProviderId,
                        principalSchema: "providers",
                        principalTable: "providers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "provider_verifications",
                schema: "providers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TrustVerificationId = table.Column<Guid>(type: "uuid", nullable: true),
                    SubmittedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReasonCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CertificationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProviderId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_provider_verifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_provider_verifications_providers_ProviderId",
                        column: x => x.ProviderId,
                        principalSchema: "providers",
                        principalTable: "providers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "provider_availability_windows",
                schema: "providers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperatingHoursId = table.Column<Guid>(type: "uuid", nullable: false),
                    OpensAt = table.Column<int>(type: "integer", nullable: false),
                    ClosesAt = table.Column<int>(type: "integer", nullable: false),
                    Label = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_provider_availability_windows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_provider_availability_windows_provider_operating_hours_Oper~",
                        column: x => x.OperatingHoursId,
                        principalSchema: "providers",
                        principalTable: "provider_operating_hours",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "service_offerings",
                schema: "providers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    pricing_model = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    price_min = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    price_max = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    price_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    price_unit = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    price_is_negotiable = table.Column<bool>(type: "boolean", nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_offerings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_service_offerings_provider_services_ServiceId",
                        column: x => x.ServiceId,
                        principalSchema: "providers",
                        principalTable: "provider_services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_provider_availability_exceptions_end",
                schema: "providers",
                table: "provider_availability_exceptions",
                column: "EndsOn");

            migrationBuilder.CreateIndex(
                name: "IX_provider_availability_exceptions_ProviderId",
                schema: "providers",
                table: "provider_availability_exceptions",
                column: "ProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_provider_availability_windows_OperatingHoursId",
                schema: "providers",
                table: "provider_availability_windows",
                column: "OperatingHoursId");

            migrationBuilder.CreateIndex(
                name: "IX_provider_category_aliases_CategoryId",
                schema: "providers",
                table: "provider_category_aliases",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "ix_provider_category_aliases_normalized",
                schema: "providers",
                table: "provider_category_aliases",
                column: "Normalized");

            migrationBuilder.CreateIndex(
                name: "IX_provider_category_keywords_CategoryId",
                schema: "providers",
                table: "provider_category_keywords",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "ix_provider_category_keywords_normalized",
                schema: "providers",
                table: "provider_category_keywords",
                column: "Normalized");

            migrationBuilder.CreateIndex(
                name: "ux_provider_category_translations_language",
                schema: "providers",
                table: "provider_category_translations",
                columns: new[] { "CategoryId", "LanguageCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_provider_certifications_expiry",
                schema: "providers",
                table: "provider_certifications",
                column: "ExpiresOn");

            migrationBuilder.CreateIndex(
                name: "IX_provider_certifications_ProviderId",
                schema: "providers",
                table: "provider_certifications",
                column: "ProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_provider_contact_methods_ProviderId",
                schema: "providers",
                table: "provider_contact_methods",
                column: "ProviderId");

            migrationBuilder.CreateIndex(
                name: "ix_provider_gallery_claimed",
                schema: "providers",
                table: "provider_gallery",
                column: "IsClaimed");

            migrationBuilder.CreateIndex(
                name: "IX_provider_gallery_ProviderId",
                schema: "providers",
                table: "provider_gallery",
                column: "ProviderId");

            migrationBuilder.CreateIndex(
                name: "ix_provider_languages_code",
                schema: "providers",
                table: "provider_languages",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_provider_languages_ProviderId",
                schema: "providers",
                table: "provider_languages",
                column: "ProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_provider_locations_ProviderId",
                schema: "providers",
                table: "provider_locations",
                column: "ProviderId");

            migrationBuilder.CreateIndex(
                name: "ix_provider_members_user",
                schema: "providers",
                table: "provider_members",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "ux_provider_members_provider_user",
                schema: "providers",
                table: "provider_members",
                columns: new[] { "ProviderId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_provider_operating_hours_ProviderId",
                schema: "providers",
                table: "provider_operating_hours",
                column: "ProviderId");

            migrationBuilder.CreateIndex(
                name: "ix_provider_recommendations_provider_featured",
                schema: "providers",
                table: "provider_recommendations",
                columns: new[] { "ProviderId", "IsFeatured", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "ux_provider_recommendations_provider_recommender",
                schema: "providers",
                table: "provider_recommendations",
                columns: new[] { "ProviderId", "RecommenderId", "IsDeleted" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_provider_reviews_provider_status_created",
                schema: "providers",
                table: "provider_reviews",
                columns: new[] { "ProviderId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_provider_reviews_reviewer_created",
                schema: "providers",
                table: "provider_reviews",
                columns: new[] { "ReviewerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "ux_provider_reviews_provider_reviewer",
                schema: "providers",
                table: "provider_reviews",
                columns: new[] { "ProviderId", "ReviewerId", "IsDeleted" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_provider_service_areas_country",
                schema: "providers",
                table: "provider_service_areas",
                column: "CountryCode");

            migrationBuilder.CreateIndex(
                name: "ix_provider_service_areas_path",
                schema: "providers",
                table: "provider_service_areas",
                column: "HierarchyPath");

            migrationBuilder.CreateIndex(
                name: "IX_provider_service_areas_ProviderId",
                schema: "providers",
                table: "provider_service_areas",
                column: "ProviderId");

            migrationBuilder.CreateIndex(
                name: "ix_provider_service_categories_active_sort",
                schema: "providers",
                table: "provider_service_categories",
                columns: new[] { "IsActive", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "ix_provider_service_categories_parent",
                schema: "providers",
                table: "provider_service_categories",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "ix_provider_service_categories_path",
                schema: "providers",
                table: "provider_service_categories",
                column: "path");

            migrationBuilder.CreateIndex(
                name: "ux_provider_service_categories_slug",
                schema: "providers",
                table: "provider_service_categories",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_provider_services_category",
                schema: "providers",
                table: "provider_services",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "ix_provider_services_name",
                schema: "providers",
                table: "provider_services",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_provider_services_ProviderId",
                schema: "providers",
                table: "provider_services",
                column: "ProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_provider_tags_ProviderId",
                schema: "providers",
                table: "provider_tags",
                column: "ProviderId");

            migrationBuilder.CreateIndex(
                name: "ix_provider_tags_slug",
                schema: "providers",
                table: "provider_tags",
                column: "Slug");

            migrationBuilder.CreateIndex(
                name: "IX_provider_verifications_ProviderId",
                schema: "providers",
                table: "provider_verifications",
                column: "ProviderId");

            migrationBuilder.CreateIndex(
                name: "ix_provider_verifications_trust_id",
                schema: "providers",
                table: "provider_verifications",
                column: "TrustVerificationId");

            migrationBuilder.CreateIndex(
                name: "ix_providers_category",
                schema: "providers",
                table: "providers",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "ix_providers_city",
                schema: "providers",
                table: "providers",
                column: "city_name");

            migrationBuilder.CreateIndex(
                name: "ix_providers_coordinates",
                schema: "providers",
                table: "providers",
                columns: new[] { "latitude", "longitude" });

            migrationBuilder.CreateIndex(
                name: "ix_providers_country",
                schema: "providers",
                table: "providers",
                column: "country_code");

            migrationBuilder.CreateIndex(
                name: "ix_providers_deleted",
                schema: "providers",
                table: "providers",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "ix_providers_featured",
                schema: "providers",
                table: "providers",
                column: "IsFeatured");

            migrationBuilder.CreateIndex(
                name: "ix_providers_name",
                schema: "providers",
                table: "providers",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "ix_providers_path",
                schema: "providers",
                table: "providers",
                column: "hierarchy_path");

            migrationBuilder.CreateIndex(
                name: "ix_providers_rating",
                schema: "providers",
                table: "providers",
                column: "average_rating");

            migrationBuilder.CreateIndex(
                name: "ix_providers_status_published",
                schema: "providers",
                table: "providers",
                columns: new[] { "Status", "PublishedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_providers_type",
                schema: "providers",
                table: "providers",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "ix_providers_verification_expiry",
                schema: "providers",
                table: "providers",
                column: "verification_expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_providers_verified",
                schema: "providers",
                table: "providers",
                column: "is_verified");

            migrationBuilder.CreateIndex(
                name: "ux_providers_slug",
                schema: "providers",
                table: "providers",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_service_offerings_ServiceId",
                schema: "providers",
                table: "service_offerings",
                column: "ServiceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "provider_availability_exceptions",
                schema: "providers");

            migrationBuilder.DropTable(
                name: "provider_availability_windows",
                schema: "providers");

            migrationBuilder.DropTable(
                name: "provider_category_aliases",
                schema: "providers");

            migrationBuilder.DropTable(
                name: "provider_category_keywords",
                schema: "providers");

            migrationBuilder.DropTable(
                name: "provider_category_translations",
                schema: "providers");

            migrationBuilder.DropTable(
                name: "provider_certifications",
                schema: "providers");

            migrationBuilder.DropTable(
                name: "provider_contact_methods",
                schema: "providers");

            migrationBuilder.DropTable(
                name: "provider_gallery",
                schema: "providers");

            migrationBuilder.DropTable(
                name: "provider_languages",
                schema: "providers");

            migrationBuilder.DropTable(
                name: "provider_locations",
                schema: "providers");

            migrationBuilder.DropTable(
                name: "provider_members",
                schema: "providers");

            migrationBuilder.DropTable(
                name: "provider_recommendations",
                schema: "providers");

            migrationBuilder.DropTable(
                name: "provider_reviews",
                schema: "providers");

            migrationBuilder.DropTable(
                name: "provider_service_areas",
                schema: "providers");

            migrationBuilder.DropTable(
                name: "provider_tags",
                schema: "providers");

            migrationBuilder.DropTable(
                name: "provider_verifications",
                schema: "providers");

            migrationBuilder.DropTable(
                name: "service_offerings",
                schema: "providers");

            migrationBuilder.DropTable(
                name: "provider_operating_hours",
                schema: "providers");

            migrationBuilder.DropTable(
                name: "provider_service_categories",
                schema: "providers");

            migrationBuilder.DropTable(
                name: "provider_services",
                schema: "providers");

            migrationBuilder.DropTable(
                name: "providers",
                schema: "providers");
        }
    }
}
