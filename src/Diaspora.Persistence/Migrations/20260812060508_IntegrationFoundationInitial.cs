using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diaspora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IntegrationFoundationInitial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "integrations");

            migrationBuilder.CreateTable(
                name: "integration_calendar_links",
                schema: "integrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    CalendarId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    SourceKind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SourceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExternalEventId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ViewUrl = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    TimeZone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    WrittenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastAttemptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RemovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_integration_calendar_links", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "integration_calendar_settings",
                schema: "integrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    DefaultCalendarId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    DefaultProvider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    AutoExportRsvps = table.Column<bool>(type: "boolean", nullable: false),
                    TwoWaySyncEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    SyncFrequency = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TimeZoneBehaviour = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ConflictBehaviour = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    default_reminder_minutes = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_integration_calendar_settings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "integration_connections",
                schema: "integrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerKind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProviderDomain = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ExternalAccountId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    AccountLabel = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsPrivateRelay = table.Column<bool>(type: "boolean", nullable: false),
                    AvatarUrl = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ConnectedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevokedAtProvider = table.Column<bool>(type: "boolean", nullable: false),
                    LastSuccessAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastFailureKind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    LastFailureMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LastFailureAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastFailureCorrelationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CredentialKeyId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ObservedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    access_token_cipher = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    refresh_token_cipher = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    scopes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_integration_connections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "integration_oauth_requests",
                schema: "integrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerKind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: true),
                    Provider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    StateHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CodeChallenge = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CodeChallengeMethod = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RedirectUri = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Intent = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConsumedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    requested_scopes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_integration_oauth_requests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "integration_sync_runs",
                schema: "integrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CalendarId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Direction = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Phase = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ItemsSynced = table.Column<int>(type: "integer", nullable: false),
                    ItemsFailed = table.Column<int>(type: "integer", nullable: false),
                    NeedsAttention = table.Column<bool>(type: "boolean", nullable: false),
                    failure_reasons = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_integration_sync_runs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "integration_webhook_events",
                schema: "integrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProviderEventId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    EventKind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ExternalReference = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ConnectionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Detail = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_integration_webhook_events", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_integration_calendar_links_external",
                schema: "integrations",
                table: "integration_calendar_links",
                columns: new[] { "ConnectionId", "ExternalEventId" });

            migrationBuilder.CreateIndex(
                name: "ix_integration_calendar_links_owner",
                schema: "integrations",
                table: "integration_calendar_links",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "ux_integration_calendar_links_source",
                schema: "integrations",
                table: "integration_calendar_links",
                columns: new[] { "ConnectionId", "CalendarId", "SourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_integration_calendar_settings_owner",
                schema: "integrations",
                table: "integration_calendar_settings",
                column: "OwnerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_integration_connections_expiry",
                schema: "integrations",
                table: "integration_connections",
                columns: new[] { "Status", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "ix_integration_connections_external_account",
                schema: "integrations",
                table: "integration_connections",
                columns: new[] { "Provider", "ExternalAccountId" });

            migrationBuilder.CreateIndex(
                name: "ix_integration_connections_owner_domain",
                schema: "integrations",
                table: "integration_connections",
                columns: new[] { "OwnerKind", "OwnerId", "ProviderDomain" });

            migrationBuilder.CreateIndex(
                name: "ux_integration_connections_owner_provider",
                schema: "integrations",
                table: "integration_connections",
                columns: new[] { "OwnerKind", "OwnerId", "Provider" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_integration_oauth_requests_expiry",
                schema: "integrations",
                table: "integration_oauth_requests",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "ux_integration_oauth_requests_state",
                schema: "integrations",
                table: "integration_oauth_requests",
                column: "StateHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_integration_sync_runs_completed",
                schema: "integrations",
                table: "integration_sync_runs",
                column: "CompletedAt");

            migrationBuilder.CreateIndex(
                name: "ix_integration_sync_runs_key",
                schema: "integrations",
                table: "integration_sync_runs",
                columns: new[] { "OwnerId", "Provider", "CalendarId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_integration_sync_runs_phase",
                schema: "integrations",
                table: "integration_sync_runs",
                columns: new[] { "Phase", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_integration_webhook_events_received",
                schema: "integrations",
                table: "integration_webhook_events",
                column: "ReceivedAt");

            migrationBuilder.CreateIndex(
                name: "ux_integration_webhook_events_dedup",
                schema: "integrations",
                table: "integration_webhook_events",
                columns: new[] { "Provider", "ProviderEventId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "integration_calendar_links",
                schema: "integrations");

            migrationBuilder.DropTable(
                name: "integration_calendar_settings",
                schema: "integrations");

            migrationBuilder.DropTable(
                name: "integration_connections",
                schema: "integrations");

            migrationBuilder.DropTable(
                name: "integration_oauth_requests",
                schema: "integrations");

            migrationBuilder.DropTable(
                name: "integration_sync_runs",
                schema: "integrations");

            migrationBuilder.DropTable(
                name: "integration_webhook_events",
                schema: "integrations");
        }
    }
}
