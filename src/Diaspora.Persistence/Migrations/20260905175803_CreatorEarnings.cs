using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diaspora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CreatorEarnings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "creator_earnings",
                schema: "creator",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    AmountMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    GrossMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Exponent = table.Column<int>(type: "integer", nullable: false),
                    SourceReference = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    SubjectType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PayoutId = table.Column<Guid>(type: "uuid", nullable: true),
                    SettledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    StateReason = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_creator_earnings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_creator_earning_clearing",
                schema: "creator",
                table: "creator_earnings",
                columns: new[] { "State", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "ix_creator_earning_ledger",
                schema: "creator",
                table: "creator_earnings",
                columns: new[] { "CreatorId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "ix_creator_earning_source_total",
                schema: "creator",
                table: "creator_earnings",
                columns: new[] { "CreatorId", "Source" });

            migrationBuilder.CreateIndex(
                name: "ix_creator_earning_state",
                schema: "creator",
                table: "creator_earnings",
                columns: new[] { "CreatorId", "State" });

            migrationBuilder.CreateIndex(
                name: "ux_creator_earning_source",
                schema: "creator",
                table: "creator_earnings",
                column: "SourceReference",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "creator_earnings",
                schema: "creator");
        }
    }
}
