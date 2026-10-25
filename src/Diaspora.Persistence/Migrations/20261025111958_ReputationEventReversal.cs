using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diaspora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReputationEventReversal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AppliedPoints",
                schema: "trust",
                table: "reputation_events",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReversesEventId",
                schema: "trust",
                table: "reputation_events",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_reputation_events_reverses",
                schema: "trust",
                table: "reputation_events",
                column: "ReversesEventId");

            migrationBuilder.CreateIndex(
                name: "ix_reputation_events_source",
                schema: "trust",
                table: "reputation_events",
                columns: new[] { "SourceType", "SourceId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_reputation_events_reverses",
                schema: "trust",
                table: "reputation_events");

            migrationBuilder.DropIndex(
                name: "ix_reputation_events_source",
                schema: "trust",
                table: "reputation_events");

            migrationBuilder.DropColumn(
                name: "AppliedPoints",
                schema: "trust",
                table: "reputation_events");

            migrationBuilder.DropColumn(
                name: "ReversesEventId",
                schema: "trust",
                table: "reputation_events");
        }
    }
}
