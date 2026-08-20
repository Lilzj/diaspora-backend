using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diaspora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AnalyticsPlatformInitial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "analytics");

            migrationBuilder.CreateTable(
                name: "analytics_backfill_runs",
                schema: "analytics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FromDay = table.Column<DateOnly>(type: "date", nullable: false),
                    ToDay = table.Column<DateOnly>(type: "date", nullable: false),
                    NextDay = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CellsRebuilt = table.Column<long>(type: "bigint", nullable: false),
                    FailureReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_analytics_backfill_runs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "analytics_content_daily",
                schema: "analytics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ContentId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    views = table.Column<long>(type: "bigint", nullable: false),
                    saves = table.Column<long>(type: "bigint", nullable: false),
                    shares = table.Column<long>(type: "bigint", nullable: false),
                    comments = table.Column<long>(type: "bigint", nullable: false),
                    reactions = table.Column<long>(type: "bigint", nullable: false),
                    helpful_votes = table.Column<long>(type: "bigint", nullable: false),
                    conversions = table.Column<long>(type: "bigint", nullable: false),
                    published = table.Column<long>(type: "bigint", nullable: false),
                    follows_gained = table.Column<long>(type: "bigint", nullable: false),
                    follows_lost = table.Column<long>(type: "bigint", nullable: false),
                    DailyUniqueViewers = table.Column<long>(type: "bigint", nullable: false),
                    ComputedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_analytics_content_daily", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "analytics_content_dimensions",
                schema: "analytics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ContentId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    CategoryKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CountryCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    CommunityId = table.Column<Guid>(type: "uuid", nullable: true),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FirstSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastUpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_analytics_content_dimensions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "analytics_event_schemas",
                schema: "analytics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    TrustClass = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AllowsClientSubmission = table.Column<bool>(type: "boolean", nullable: false),
                    DedupStrategy = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RequiresContent = table.Column<bool>(type: "boolean", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IsAccepted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_analytics_event_schemas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "analytics_events",
                schema: "analytics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                    EventVersion = table.Column<int>(type: "integer", nullable: false),
                    TrustClass = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SourceModule = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DedupKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    OccurredOn = table.Column<DateOnly>(type: "date", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ContentId = table.Column<Guid>(type: "uuid", nullable: true),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CountryCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    CommunityId = table.Column<Guid>(type: "uuid", nullable: true),
                    Surface = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Qualifier = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Weight = table.Column<int>(type: "integer", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: true),
                    CausationId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_analytics_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "analytics_owner_daily",
                schema: "analytics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    views = table.Column<long>(type: "bigint", nullable: false),
                    saves = table.Column<long>(type: "bigint", nullable: false),
                    shares = table.Column<long>(type: "bigint", nullable: false),
                    comments = table.Column<long>(type: "bigint", nullable: false),
                    reactions = table.Column<long>(type: "bigint", nullable: false),
                    helpful_votes = table.Column<long>(type: "bigint", nullable: false),
                    conversions = table.Column<long>(type: "bigint", nullable: false),
                    published = table.Column<long>(type: "bigint", nullable: false),
                    follows_gained = table.Column<long>(type: "bigint", nullable: false),
                    follows_lost = table.Column<long>(type: "bigint", nullable: false),
                    DailyUniqueViewers = table.Column<long>(type: "bigint", nullable: false),
                    ProfileViews = table.Column<long>(type: "bigint", nullable: false),
                    ComputedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_analytics_owner_daily", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "analytics_projection_checkpoint",
                schema: "analytics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessedCount = table.Column<long>(type: "bigint", nullable: false),
                    LastRunAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_analytics_projection_checkpoint", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_analytics_backfill_runs_status",
                schema: "analytics",
                table: "analytics_backfill_runs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "ix_analytics_content_daily_day",
                schema: "analytics",
                table: "analytics_content_daily",
                column: "Day");

            migrationBuilder.CreateIndex(
                name: "ix_analytics_content_daily_owner",
                schema: "analytics",
                table: "analytics_content_daily",
                columns: new[] { "OwnerUserId", "Day" });

            migrationBuilder.CreateIndex(
                name: "ux_analytics_content_daily",
                schema: "analytics",
                table: "analytics_content_daily",
                columns: new[] { "ContentType", "ContentId", "Day" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_analytics_content_dimension_owner",
                schema: "analytics",
                table: "analytics_content_dimensions",
                columns: new[] { "OwnerUserId", "ContentType" });

            migrationBuilder.CreateIndex(
                name: "ux_analytics_content_dimension",
                schema: "analytics",
                table: "analytics_content_dimensions",
                columns: new[] { "ContentType", "ContentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_analytics_event_schemas",
                schema: "analytics",
                table: "analytics_event_schemas",
                columns: new[] { "EventType", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_analytics_events_actor",
                schema: "analytics",
                table: "analytics_events",
                column: "ActorKey");

            migrationBuilder.CreateIndex(
                name: "ix_analytics_events_content_day",
                schema: "analytics",
                table: "analytics_events",
                columns: new[] { "ContentType", "ContentId", "OccurredOn" });

            migrationBuilder.CreateIndex(
                name: "ix_analytics_events_occurred_on",
                schema: "analytics",
                table: "analytics_events",
                column: "OccurredOn");

            migrationBuilder.CreateIndex(
                name: "ix_analytics_events_owner_day",
                schema: "analytics",
                table: "analytics_events",
                columns: new[] { "OwnerUserId", "OccurredOn" });

            migrationBuilder.CreateIndex(
                name: "ix_analytics_events_received",
                schema: "analytics",
                table: "analytics_events",
                columns: new[] { "ReceivedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "ux_analytics_events_dedup",
                schema: "analytics",
                table: "analytics_events",
                column: "DedupKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_analytics_owner_daily_day",
                schema: "analytics",
                table: "analytics_owner_daily",
                column: "Day");

            migrationBuilder.CreateIndex(
                name: "ux_analytics_owner_daily",
                schema: "analytics",
                table: "analytics_owner_daily",
                columns: new[] { "OwnerUserId", "Day" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "analytics_backfill_runs",
                schema: "analytics");

            migrationBuilder.DropTable(
                name: "analytics_content_daily",
                schema: "analytics");

            migrationBuilder.DropTable(
                name: "analytics_content_dimensions",
                schema: "analytics");

            migrationBuilder.DropTable(
                name: "analytics_event_schemas",
                schema: "analytics");

            migrationBuilder.DropTable(
                name: "analytics_events",
                schema: "analytics");

            migrationBuilder.DropTable(
                name: "analytics_owner_daily",
                schema: "analytics");

            migrationBuilder.DropTable(
                name: "analytics_projection_checkpoint",
                schema: "analytics");
        }
    }
}
