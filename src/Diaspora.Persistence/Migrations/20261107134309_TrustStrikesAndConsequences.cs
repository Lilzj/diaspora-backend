using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diaspora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TrustStrikesAndConsequences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ConsequencesAppliedAt",
                schema: "trust",
                table: "moderation_items",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SecondDeciderId",
                schema: "trust",
                table: "appeals",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "strikes",
                schema: "trust",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Area = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Level = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    IssuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    BlockedUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevokedReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_strikes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_strikes_member",
                schema: "trust",
                table: "strikes",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "ux_strikes_case",
                schema: "trust",
                table: "strikes",
                column: "CaseId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "strikes",
                schema: "trust");

            migrationBuilder.DropColumn(
                name: "ConsequencesAppliedAt",
                schema: "trust",
                table: "moderation_items");

            migrationBuilder.DropColumn(
                name: "SecondDeciderId",
                schema: "trust",
                table: "appeals");
        }
    }
}
