using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diaspora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AccountActionProposals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StatusReasonCode",
                schema: "identity",
                table: "users",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "account_action_proposals",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TargetUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProposedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ReasonCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Note = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DecidedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DecisionNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_action_proposals", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_account_action_proposals_status_created",
                schema: "identity",
                table: "account_action_proposals",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_account_action_proposals_target_kind_status",
                schema: "identity",
                table: "account_action_proposals",
                columns: new[] { "TargetUserId", "Kind", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "account_action_proposals",
                schema: "identity");

            migrationBuilder.DropColumn(
                name: "StatusReasonCode",
                schema: "identity",
                table: "users");
        }
    }
}
