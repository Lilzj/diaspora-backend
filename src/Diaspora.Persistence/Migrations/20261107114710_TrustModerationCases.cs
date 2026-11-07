using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diaspora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TrustModerationCases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AppealDeadline",
                schema: "trust",
                table: "moderation_items",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Area",
                schema: "trust",
                table: "moderation_items",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrentOutcome",
                schema: "trust",
                table: "moderation_items",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FirstDeciderId",
                schema: "trust",
                table: "moderation_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HiddenAutomatically",
                schema: "trust",
                table: "moderation_items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsListed",
                schema: "trust",
                table: "moderation_items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "OpenedById",
                schema: "trust",
                table: "moderation_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OpeningNote",
                schema: "trust",
                table: "moderation_items",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProposedNote",
                schema: "trust",
                table: "moderation_items",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProposedReasonCode",
                schema: "trust",
                table: "moderation_items",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReasonCode",
                schema: "trust",
                table: "moderation_items",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReviewRequestedAt",
                schema: "trust",
                table: "moderation_items",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SecondReview",
                schema: "trust",
                table: "moderation_items",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "None");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SecondReviewDueAt",
                schema: "trust",
                table: "moderation_items",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "StaffInitiated",
                schema: "trust",
                table: "moderation_items",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TargetType",
                schema: "trust",
                table: "moderation_items",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ModeratorId",
                schema: "trust",
                table: "moderation_decisions",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                schema: "trust",
                table: "moderation_decisions",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Decision");

            migrationBuilder.AddColumn<string>(
                name: "Outcome",
                schema: "trust",
                table: "moderation_decisions",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReasonCode",
                schema: "trust",
                table: "moderation_decisions",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "moderation_case_reports",
                schema: "trust",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReporterId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReasonCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ModuleReason = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Detail = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Module = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ModuleReportId = table.Column<Guid>(type: "uuid", nullable: false),
                    FiledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_moderation_case_reports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_moderation_case_reports_moderation_items_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "trust",
                        principalTable: "moderation_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_moderation_items_target_status",
                schema: "trust",
                table: "moderation_items",
                columns: new[] { "TargetType", "ContentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ux_moderation_case_reports_reporter",
                schema: "trust",
                table: "moderation_case_reports",
                columns: new[] { "ItemId", "ReporterId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "moderation_case_reports",
                schema: "trust");

            migrationBuilder.DropIndex(
                name: "ix_moderation_items_target_status",
                schema: "trust",
                table: "moderation_items");

            migrationBuilder.DropColumn(
                name: "AppealDeadline",
                schema: "trust",
                table: "moderation_items");

            migrationBuilder.DropColumn(
                name: "Area",
                schema: "trust",
                table: "moderation_items");

            migrationBuilder.DropColumn(
                name: "CurrentOutcome",
                schema: "trust",
                table: "moderation_items");

            migrationBuilder.DropColumn(
                name: "FirstDeciderId",
                schema: "trust",
                table: "moderation_items");

            migrationBuilder.DropColumn(
                name: "HiddenAutomatically",
                schema: "trust",
                table: "moderation_items");

            migrationBuilder.DropColumn(
                name: "IsListed",
                schema: "trust",
                table: "moderation_items");

            migrationBuilder.DropColumn(
                name: "OpenedById",
                schema: "trust",
                table: "moderation_items");

            migrationBuilder.DropColumn(
                name: "OpeningNote",
                schema: "trust",
                table: "moderation_items");

            migrationBuilder.DropColumn(
                name: "ProposedNote",
                schema: "trust",
                table: "moderation_items");

            migrationBuilder.DropColumn(
                name: "ProposedReasonCode",
                schema: "trust",
                table: "moderation_items");

            migrationBuilder.DropColumn(
                name: "ReasonCode",
                schema: "trust",
                table: "moderation_items");

            migrationBuilder.DropColumn(
                name: "ReviewRequestedAt",
                schema: "trust",
                table: "moderation_items");

            migrationBuilder.DropColumn(
                name: "SecondReview",
                schema: "trust",
                table: "moderation_items");

            migrationBuilder.DropColumn(
                name: "SecondReviewDueAt",
                schema: "trust",
                table: "moderation_items");

            migrationBuilder.DropColumn(
                name: "StaffInitiated",
                schema: "trust",
                table: "moderation_items");

            migrationBuilder.DropColumn(
                name: "TargetType",
                schema: "trust",
                table: "moderation_items");

            migrationBuilder.DropColumn(
                name: "Kind",
                schema: "trust",
                table: "moderation_decisions");

            migrationBuilder.DropColumn(
                name: "Outcome",
                schema: "trust",
                table: "moderation_decisions");

            migrationBuilder.DropColumn(
                name: "ReasonCode",
                schema: "trust",
                table: "moderation_decisions");

            migrationBuilder.AlterColumn<Guid>(
                name: "ModeratorId",
                schema: "trust",
                table: "moderation_decisions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
