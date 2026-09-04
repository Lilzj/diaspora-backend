using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diaspora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CommunityIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccentColor",
                schema: "community_programme",
                table: "communities",
                type: "character varying(7)",
                maxLength: 7,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IconName",
                schema: "community_programme",
                table: "communities",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LogoMediaId",
                schema: "community_programme",
                table: "communities",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccentColor",
                schema: "community_programme",
                table: "communities");

            migrationBuilder.DropColumn(
                name: "IconName",
                schema: "community_programme",
                table: "communities");

            migrationBuilder.DropColumn(
                name: "LogoMediaId",
                schema: "community_programme",
                table: "communities");
        }
    }
}
