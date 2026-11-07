using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diaspora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PlatformAuditRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_records",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ActorUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ActorRole = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Action = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    EntityType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    EntityId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Detail = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_records", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_audit_records_actor",
                schema: "platform",
                table: "audit_records",
                columns: new[] { "ActorUserId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_records_entity",
                schema: "platform",
                table: "audit_records",
                columns: new[] { "EntityId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_records_occurred_at",
                schema: "platform",
                table: "audit_records",
                column: "OccurredAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_records",
                schema: "platform");
        }
    }
}
