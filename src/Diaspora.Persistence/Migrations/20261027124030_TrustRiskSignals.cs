using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diaspora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TrustRiskSignals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "risk_signals",
                schema: "trust",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SourceKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    VerificationType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ContentKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_risk_signals", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_risk_signals_occurred",
                schema: "trust",
                table: "risk_signals",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "ix_risk_signals_source",
                schema: "trust",
                table: "risk_signals",
                column: "SourceKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_risk_signals_user_occurred",
                schema: "trust",
                table: "risk_signals",
                columns: new[] { "UserId", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "risk_signals",
                schema: "trust");
        }
    }
}
