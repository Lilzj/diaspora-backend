using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diaspora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CommunityAlertLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AffectedRadiusMetres",
                schema: "community",
                table: "community_posts",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ClosedAt",
                schema: "community",
                table: "community_posts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "point_is_exact",
                schema: "community",
                table: "community_posts",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AffectedRadiusMetres",
                schema: "community",
                table: "community_posts");

            migrationBuilder.DropColumn(
                name: "ClosedAt",
                schema: "community",
                table: "community_posts");

            migrationBuilder.DropColumn(
                name: "point_is_exact",
                schema: "community",
                table: "community_posts");
        }
    }
}
