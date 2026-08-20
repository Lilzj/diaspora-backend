using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diaspora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LocationModuleInitial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "location");

            migrationBuilder.CreateTable(
                name: "administrative_areas",
                schema: "location",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Level = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    local_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    Slug = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CountryId = table.Column<Guid>(type: "uuid", nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    path = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    latitude = table.Column<double>(type: "double precision", nullable: true),
                    longitude = table.Column<double>(type: "double precision", nullable: true),
                    extent_north = table.Column<double>(type: "double precision", nullable: true),
                    extent_south = table.Column<double>(type: "double precision", nullable: true),
                    extent_east = table.Column<double>(type: "double precision", nullable: true),
                    extent_west = table.Column<double>(type: "double precision", nullable: true),
                    boundary = table.Column<string>(type: "character varying(60000)", maxLength: 60000, nullable: true),
                    PostalCode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    Overview = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    SafetyScore = table.Column<int>(type: "integer", nullable: true),
                    WalkabilityScore = table.Column<int>(type: "integer", nullable: true),
                    CostOfLiving = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    Prominence = table.Column<int>(type: "integer", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_administrative_areas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "countries",
                schema: "location",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    local_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    Slug = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    calling_code = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    currency_code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    primary_language = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    default_locale = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    primary_time_zone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    continent = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    subregion = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    center_latitude = table.Column<double>(type: "double precision", nullable: true),
                    center_longitude = table.Column<double>(type: "double precision", nullable: true),
                    extent_north = table.Column<double>(type: "double precision", nullable: true),
                    extent_south = table.Column<double>(type: "double precision", nullable: true),
                    extent_east = table.Column<double>(type: "double precision", nullable: true),
                    extent_west = table.Column<double>(type: "double precision", nullable: true),
                    IsSupported = table.Column<bool>(type: "boolean", nullable: false),
                    IsFeatured = table.Column<bool>(type: "boolean", nullable: false),
                    LandmarkImageUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true),
                    spoken_languages = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    time_zones = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_countries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "country_experiences",
                schema: "location",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    arrived_on = table.Column<DateOnly>(type: "date", nullable: false),
                    departed_on = table.Column<DateOnly>(type: "date", nullable: true),
                    Role = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    LanguageFamiliarity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CulturalFamiliarity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_country_experiences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "location_profiles",
                schema: "location",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    current_country_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    current_country_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    current_state_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    current_city_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    current_neighborhood = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    current_street = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    current_latitude = table.Column<double>(type: "double precision", nullable: true),
                    current_longitude = table.Column<double>(type: "double precision", nullable: true),
                    current_area_id = table.Column<Guid>(type: "uuid", nullable: true),
                    current_display_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    travel_country_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    travel_country_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    travel_state_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    travel_city_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    travel_neighborhood = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    travel_street = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    travel_latitude = table.Column<double>(type: "double precision", nullable: true),
                    travel_longitude = table.Column<double>(type: "double precision", nullable: true),
                    travel_area_id = table.Column<Guid>(type: "uuid", nullable: true),
                    travel_display_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    TravelMode = table.Column<bool>(type: "boolean", nullable: false),
                    OriginCountry = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    HomeCountry = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    search_radius_scope = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    search_radius_km = table.Column<double>(type: "double precision", nullable: false),
                    distance_unit = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    coordinate_precision = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    preferred_country_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    preferred_area_id = table.Column<Guid>(type: "uuid", nullable: true),
                    PermissionStatus = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_location_profiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "verified_residencies",
                schema: "location",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    State = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    VerificationId = table.Column<Guid>(type: "uuid", nullable: true),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Reason = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ObservedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_verified_residencies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "country_expertise",
                schema: "location",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExperienceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Area = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Confidence = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DeclaredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_country_expertise", x => x.Id);
                    table.ForeignKey(
                        name: "FK_country_expertise_country_experiences_ExperienceId",
                        column: x => x.ExperienceId,
                        principalSchema: "location",
                        principalTable: "country_experiences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "recent_locations",
                schema: "location",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    country_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    country_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    state_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    city_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    neighborhood = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    street = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    latitude = table.Column<double>(type: "double precision", nullable: true),
                    longitude = table.Column<double>(type: "double precision", nullable: true),
                    area_id = table.Column<Guid>(type: "uuid", nullable: true),
                    display_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    VisitedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recent_locations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_recent_locations_location_profiles_ProfileId",
                        column: x => x.ProfileId,
                        principalSchema: "location",
                        principalTable: "location_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "saved_locations",
                schema: "location",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    country_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    country_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    state_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    city_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    neighborhood = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    street = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    latitude = table.Column<double>(type: "double precision", nullable: true),
                    longitude = table.Column<double>(type: "double precision", nullable: true),
                    area_id = table.Column<Guid>(type: "uuid", nullable: true),
                    display_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    SavedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saved_locations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_saved_locations_location_profiles_ProfileId",
                        column: x => x.ProfileId,
                        principalSchema: "location",
                        principalTable: "location_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_areas_coordinates",
                schema: "location",
                table: "administrative_areas",
                columns: new[] { "latitude", "longitude" });

            migrationBuilder.CreateIndex(
                name: "ix_areas_country_level",
                schema: "location",
                table: "administrative_areas",
                columns: new[] { "CountryCode", "Level" });

            migrationBuilder.CreateIndex(
                name: "ix_areas_name",
                schema: "location",
                table: "administrative_areas",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "ix_areas_parent_prominence",
                schema: "location",
                table: "administrative_areas",
                columns: new[] { "ParentId", "Prominence" });

            migrationBuilder.CreateIndex(
                name: "ix_areas_parent_slug",
                schema: "location",
                table: "administrative_areas",
                columns: new[] { "ParentId", "Slug" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_areas_path",
                schema: "location",
                table: "administrative_areas",
                column: "path");

            migrationBuilder.CreateIndex(
                name: "ix_areas_slug_level",
                schema: "location",
                table: "administrative_areas",
                columns: new[] { "Slug", "Level" });

            migrationBuilder.CreateIndex(
                name: "ix_countries_code",
                schema: "location",
                table: "countries",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_countries_slug",
                schema: "location",
                table: "countries",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_countries_supported_featured",
                schema: "location",
                table: "countries",
                columns: new[] { "IsSupported", "IsFeatured" });

            migrationBuilder.CreateIndex(
                name: "ix_country_experiences_user_country",
                schema: "location",
                table: "country_experiences",
                columns: new[] { "UserId", "CountryCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_country_expertise_experience_area",
                schema: "location",
                table: "country_expertise",
                columns: new[] { "ExperienceId", "Area" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_location_profiles_user",
                schema: "location",
                table: "location_profiles",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_recent_locations_profile_visited",
                schema: "location",
                table: "recent_locations",
                columns: new[] { "ProfileId", "VisitedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_saved_locations_profile_label",
                schema: "location",
                table: "saved_locations",
                columns: new[] { "ProfileId", "Label" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_verified_residencies_state_expiry",
                schema: "location",
                table: "verified_residencies",
                columns: new[] { "State", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "ix_verified_residencies_user_country",
                schema: "location",
                table: "verified_residencies",
                columns: new[] { "UserId", "CountryCode" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "administrative_areas",
                schema: "location");

            migrationBuilder.DropTable(
                name: "countries",
                schema: "location");

            migrationBuilder.DropTable(
                name: "country_expertise",
                schema: "location");

            migrationBuilder.DropTable(
                name: "recent_locations",
                schema: "location");

            migrationBuilder.DropTable(
                name: "saved_locations",
                schema: "location");

            migrationBuilder.DropTable(
                name: "verified_residencies",
                schema: "location");

            migrationBuilder.DropTable(
                name: "country_experiences",
                schema: "location");

            migrationBuilder.DropTable(
                name: "location_profiles",
                schema: "location");
        }
    }
}
