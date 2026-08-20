using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diaspora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AutomationPlatformInitial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "automation");

            migrationBuilder.CreateTable(
                name: "automation_occurrences",
                schema: "automation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AutomationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    DueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TimeZoneId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    LocalWallClock = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ClaimedBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ClaimExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    LastError = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Resolution = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_automation_occurrences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "automation_reminders",
                schema: "automation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectKind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SubjectMoment = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    OffsetMinutes = table.Column<int>(type: "integer", nullable: false),
                    Anchor = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    TimeZoneId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ScheduledFor = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    DeduplicationKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_automation_reminders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "automation_runs",
                schema: "automation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AutomationId = table.Column<Guid>(type: "uuid", nullable: false),
                    AutomationName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerKind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Reason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TriggerEventId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TriggerKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TriggerSummary = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    TriggeredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DurationMs = table.Column<long>(type: "bigint", nullable: true),
                    FailureKind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    FailureMessage = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    RecoveryHint = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IsSimulated = table.Column<bool>(type: "boolean", nullable: false),
                    WasDeduplicated = table.Column<bool>(type: "boolean", nullable: false),
                    ChainDepth = table.Column<int>(type: "integer", nullable: false),
                    ChainId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_automation_runs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "automation_saved_search_matches",
                schema: "automation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SavedSearchId = table.Column<Guid>(type: "uuid", nullable: false),
                    MatchedItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Change = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    MatchedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Notified = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_automation_saved_search_matches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "automation_saved_searches",
                schema: "automation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    Subject = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Criteria = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CriteriaFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    QuerySummary = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AlertsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AlertFrequency = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    WatchFor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastMatchedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NewMatchCount = table.Column<int>(type: "integer", nullable: false),
                    AutomationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_automation_saved_searches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "automations",
                schema: "automation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerKind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerLabel = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CommunityIds = table.Column<string>(type: "character varying(1200)", maxLength: 1200, nullable: false),
                    IsCrossCommunity = table.Column<bool>(type: "boolean", nullable: false),
                    CountryCodes = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    NeighbourhoodIds = table.Column<string>(type: "character varying(1200)", maxLength: 1200, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TemplateId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TriggerKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TriggerParameters = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    HasSchedule = table.Column<bool>(type: "boolean", nullable: false),
                    Frequency = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TimezoneMode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TimeZoneId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Hour = table.Column<int>(type: "integer", nullable: true),
                    Minute = table.Column<int>(type: "integer", nullable: true),
                    DaysOfWeek = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DayOfMonth = table.Column<int>(type: "integer", nullable: true),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    MaxExecutions = table.Column<int>(type: "integer", nullable: true),
                    EventOffsetMinutes = table.Column<int>(type: "integer", nullable: true),
                    EventOffsetAnchor = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    CustomDescription = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastRunAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NextRunAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ExecutionCount = table.Column<int>(type: "integer", nullable: false),
                    LastRunStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    AttentionReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_automations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "automation_run_actions",
                schema: "automation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActionKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Label = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SuppressionReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    FailureKind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Message = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ResultKind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ResultId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    DeepLink = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_automation_run_actions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_automation_run_actions_automation_runs_RunId",
                        column: x => x.RunId,
                        principalSchema: "automation",
                        principalTable: "automation_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "automation_run_conditions",
                schema: "automation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConditionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Matched = table.Column<bool>(type: "boolean", nullable: false),
                    EvaluatedByServer = table.Column<bool>(type: "boolean", nullable: false),
                    Explanation = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_automation_run_conditions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_automation_run_conditions_automation_runs_RunId",
                        column: x => x.RunId,
                        principalSchema: "automation",
                        principalTable: "automation_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "automation_actions",
                schema: "automation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AutomationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Parameters = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_automation_actions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_automation_actions_automations_AutomationId",
                        column: x => x.AutomationId,
                        principalSchema: "automation",
                        principalTable: "automations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "automation_audit",
                schema: "automation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AutomationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorLabel = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Detail = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_automation_audit", x => x.Id);
                    table.ForeignKey(
                        name: "FK_automation_audit_automations_AutomationId",
                        column: x => x.AutomationId,
                        principalSchema: "automation",
                        principalTable: "automations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "automation_conditions",
                schema: "automation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AutomationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Operator = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Value = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    SecondaryValue = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_automation_conditions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_automation_conditions_automations_AutomationId",
                        column: x => x.AutomationId,
                        principalSchema: "automation",
                        principalTable: "automations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_automation_actions_AutomationId",
                schema: "automation",
                table: "automation_actions",
                column: "AutomationId");

            migrationBuilder.CreateIndex(
                name: "IX_automation_audit_AutomationId_At",
                schema: "automation",
                table: "automation_audit",
                columns: new[] { "AutomationId", "At" });

            migrationBuilder.CreateIndex(
                name: "IX_automation_conditions_AutomationId",
                schema: "automation",
                table: "automation_conditions",
                column: "AutomationId");

            migrationBuilder.CreateIndex(
                name: "IX_automation_occurrences_AutomationId_DueAt",
                schema: "automation",
                table: "automation_occurrences",
                columns: new[] { "AutomationId", "DueAt" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_automation_occurrences_Status_DueAt",
                schema: "automation",
                table: "automation_occurrences",
                columns: new[] { "Status", "DueAt" });

            migrationBuilder.CreateIndex(
                name: "IX_automation_reminders_DeduplicationKey",
                schema: "automation",
                table: "automation_reminders",
                column: "DeduplicationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_automation_reminders_MemberId_SubjectKind_SubjectId",
                schema: "automation",
                table: "automation_reminders",
                columns: new[] { "MemberId", "SubjectKind", "SubjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_automation_reminders_State_ScheduledFor",
                schema: "automation",
                table: "automation_reminders",
                columns: new[] { "State", "ScheduledFor" });

            migrationBuilder.CreateIndex(
                name: "IX_automation_reminders_SubjectId_State",
                schema: "automation",
                table: "automation_reminders",
                columns: new[] { "SubjectId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_automation_run_actions_RunId",
                schema: "automation",
                table: "automation_run_actions",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_automation_run_actions_RunId_ActionId",
                schema: "automation",
                table: "automation_run_actions",
                columns: new[] { "RunId", "ActionId" });

            migrationBuilder.CreateIndex(
                name: "IX_automation_run_conditions_RunId",
                schema: "automation",
                table: "automation_run_conditions",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_automation_runs_AutomationId_StartedAt",
                schema: "automation",
                table: "automation_runs",
                columns: new[] { "AutomationId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_automation_runs_AutomationId_TriggerEventId",
                schema: "automation",
                table: "automation_runs",
                columns: new[] { "AutomationId", "TriggerEventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_automation_runs_OwnerId_StartedAt",
                schema: "automation",
                table: "automation_runs",
                columns: new[] { "OwnerId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_automation_runs_Status_StartedAt",
                schema: "automation",
                table: "automation_runs",
                columns: new[] { "Status", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_automation_saved_search_matches_SavedSearchId_MatchedItemId",
                schema: "automation",
                table: "automation_saved_search_matches",
                columns: new[] { "SavedSearchId", "MatchedItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_automation_saved_search_matches_SavedSearchId_Notified",
                schema: "automation",
                table: "automation_saved_search_matches",
                columns: new[] { "SavedSearchId", "Notified" });

            migrationBuilder.CreateIndex(
                name: "IX_automation_saved_searches_MemberId_Subject_CriteriaFingerpr~",
                schema: "automation",
                table: "automation_saved_searches",
                columns: new[] { "MemberId", "Subject", "CriteriaFingerprint" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_automation_saved_searches_Subject_AlertsEnabled",
                schema: "automation",
                table: "automation_saved_searches",
                columns: new[] { "Subject", "AlertsEnabled" });

            migrationBuilder.CreateIndex(
                name: "IX_automations_CreatedByUserId_IdempotencyKey",
                schema: "automation",
                table: "automations",
                columns: new[] { "CreatedByUserId", "IdempotencyKey" });

            migrationBuilder.CreateIndex(
                name: "IX_automations_OwnerKind_OwnerId_Status",
                schema: "automation",
                table: "automations",
                columns: new[] { "OwnerKind", "OwnerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_automations_Status_NextRunAt",
                schema: "automation",
                table: "automations",
                columns: new[] { "Status", "NextRunAt" });

            migrationBuilder.CreateIndex(
                name: "IX_automations_TriggerKey_Status",
                schema: "automation",
                table: "automations",
                columns: new[] { "TriggerKey", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "automation_actions",
                schema: "automation");

            migrationBuilder.DropTable(
                name: "automation_audit",
                schema: "automation");

            migrationBuilder.DropTable(
                name: "automation_conditions",
                schema: "automation");

            migrationBuilder.DropTable(
                name: "automation_occurrences",
                schema: "automation");

            migrationBuilder.DropTable(
                name: "automation_reminders",
                schema: "automation");

            migrationBuilder.DropTable(
                name: "automation_run_actions",
                schema: "automation");

            migrationBuilder.DropTable(
                name: "automation_run_conditions",
                schema: "automation");

            migrationBuilder.DropTable(
                name: "automation_saved_search_matches",
                schema: "automation");

            migrationBuilder.DropTable(
                name: "automation_saved_searches",
                schema: "automation");

            migrationBuilder.DropTable(
                name: "automations",
                schema: "automation");

            migrationBuilder.DropTable(
                name: "automation_runs",
                schema: "automation");
        }
    }
}
