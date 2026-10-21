using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diaspora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AutomationTriggerSources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CommunityId",
                schema: "community_life",
                table: "community_life_events",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "member_presence",
                schema: "location",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    EnabledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastAreaId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastEvaluatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_member_presence", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_community_life_events_community_status",
                schema: "community_life",
                table: "community_life_events",
                columns: new[] { "CommunityId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_member_presence_user",
                schema: "location",
                table: "member_presence",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "member_presence",
                schema: "location");

            migrationBuilder.DropIndex(
                name: "ix_community_life_events_community_status",
                schema: "community_life",
                table: "community_life_events");

            migrationBuilder.DropColumn(
                name: "CommunityId",
                schema: "community_life",
                table: "community_life_events");
        }
    }
}
