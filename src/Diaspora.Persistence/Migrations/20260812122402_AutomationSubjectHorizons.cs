using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diaspora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AutomationSubjectHorizons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "automation_subject_horizons",
                schema: "automation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TriggerKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SubjectKind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubjectTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SubjectMoment = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TimeZoneId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcernedOwnerIds = table.Column<string>(type: "character varying(1200)", maxLength: 1200, nullable: false),
                    Values = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Summary = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ClaimedBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ClaimExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    LastError = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Resolution = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_automation_subject_horizons", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_automation_subject_horizons_CompletedAt",
                schema: "automation",
                table: "automation_subject_horizons",
                column: "CompletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_automation_subject_horizons_Status_DueAt",
                schema: "automation",
                table: "automation_subject_horizons",
                columns: new[] { "Status", "DueAt" });

            migrationBuilder.CreateIndex(
                name: "IX_automation_subject_horizons_SubjectId_Status",
                schema: "automation",
                table: "automation_subject_horizons",
                columns: new[] { "SubjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_automation_subject_horizons_TriggerKey_SubjectId",
                schema: "automation",
                table: "automation_subject_horizons",
                columns: new[] { "TriggerKey", "SubjectId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "automation_subject_horizons",
                schema: "automation");
        }
    }
}
