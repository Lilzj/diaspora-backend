using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diaspora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CommunityLifeEventSeries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSeriesException",
                schema: "community_life",
                table: "community_life_events",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "OriginalStartsAt",
                schema: "community_life",
                table: "community_life_events",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SeriesId",
                schema: "community_life",
                table: "community_life_events",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SeriesSequence",
                schema: "community_life",
                table: "community_life_events",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "community_life_event_series",
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
                    recurrence_frequency = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    recurrence_interval = table.Column<int>(type: "integer", nullable: false),
                    recurrence_by_weekday = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    recurrence_by_month_day = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    recurrence_end_kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    recurrence_until = table.Column<DateOnly>(type: "date", nullable: true),
                    recurrence_count = table.Column<int>(type: "integer", nullable: true),
                    first_local_date = table.Column<DateOnly>(type: "date", nullable: false),
                    local_start_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    time_zone_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
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
                    CapacityMaximum = table.Column<int>(type: "integer", nullable: true),
                    RequiresRsvp = table.Column<bool>(type: "boolean", nullable: false),
                    IsAttendeeListPublic = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Visibility = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    MaterialisedThrough = table.Column<DateOnly>(type: "date", nullable: true),
                    LastMaterialisedSequence = table.Column<int>(type: "integer", nullable: false),
                    OccurrenceCount = table.Column<int>(type: "integer", nullable: false),
                    CompletedCount = table.Column<int>(type: "integer", nullable: false),
                    CancelledOccurrenceCount = table.Column<int>(type: "integer", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FirstPublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancellationReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_community_life_event_series", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "community_life_event_series_exceptions",
                schema: "community_life",
                columns: table => new
                {
                    LocalDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EventSeriesId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_life_event_series_exceptions", x => new { x.EventSeriesId, x.LocalDate });
                    table.ForeignKey(
                        name: "FK_community_life_event_series_exceptions_community_life_event~",
                        column: x => x.EventSeriesId,
                        principalSchema: "community_life",
                        principalTable: "community_life_event_series",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_community_life_events_series_sequence",
                schema: "community_life",
                table: "community_life_events",
                columns: new[] { "SeriesId", "SeriesSequence" });

            migrationBuilder.CreateIndex(
                name: "ix_community_life_event_series_category",
                schema: "community_life",
                table: "community_life_event_series",
                column: "CategoryKey");

            migrationBuilder.CreateIndex(
                name: "ix_community_life_event_series_country_city",
                schema: "community_life",
                table: "community_life_event_series",
                columns: new[] { "country_code", "city_name" });

            migrationBuilder.CreateIndex(
                name: "ix_community_life_event_series_expansion",
                schema: "community_life",
                table: "community_life_event_series",
                columns: new[] { "Status", "MaterialisedThrough" });

            migrationBuilder.CreateIndex(
                name: "ix_community_life_event_series_organization_status",
                schema: "community_life",
                table: "community_life_event_series",
                columns: new[] { "OrganizationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_community_life_event_series_organizer_status",
                schema: "community_life",
                table: "community_life_event_series",
                columns: new[] { "OrganizerUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_community_life_event_series_slug",
                schema: "community_life",
                table: "community_life_event_series",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_community_life_event_series_title",
                schema: "community_life",
                table: "community_life_event_series",
                column: "title");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "community_life_event_series_exceptions",
                schema: "community_life");

            migrationBuilder.DropTable(
                name: "community_life_event_series",
                schema: "community_life");

            migrationBuilder.DropIndex(
                name: "ix_community_life_events_series_sequence",
                schema: "community_life",
                table: "community_life_events");

            migrationBuilder.DropColumn(
                name: "IsSeriesException",
                schema: "community_life",
                table: "community_life_events");

            migrationBuilder.DropColumn(
                name: "OriginalStartsAt",
                schema: "community_life",
                table: "community_life_events");

            migrationBuilder.DropColumn(
                name: "SeriesId",
                schema: "community_life",
                table: "community_life_events");

            migrationBuilder.DropColumn(
                name: "SeriesSequence",
                schema: "community_life",
                table: "community_life_events");
        }
    }
}
