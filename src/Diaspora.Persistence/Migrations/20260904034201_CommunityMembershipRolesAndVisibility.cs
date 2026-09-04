using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diaspora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CommunityMembershipRolesAndVisibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string[]>(
                name: "Roles",
                schema: "community_programme",
                table: "community_memberships",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "VerifiedAt",
                schema: "community_programme",
                table: "community_memberships",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Visibility",
                schema: "community_programme",
                table: "community_memberships",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Roles",
                schema: "community_programme",
                table: "community_memberships");

            migrationBuilder.DropColumn(
                name: "VerifiedAt",
                schema: "community_programme",
                table: "community_memberships");

            migrationBuilder.DropColumn(
                name: "Visibility",
                schema: "community_programme",
                table: "community_memberships");
        }
    }
}
