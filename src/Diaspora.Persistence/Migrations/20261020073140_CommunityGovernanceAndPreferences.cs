using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diaspora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CommunityGovernanceAndPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "community_member_preferences",
                schema: "community_programme",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    CrossCommunityDiscoveryEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ShowPrimaryCommunityOnProfile = table.Column<bool>(type: "boolean", nullable: false),
                    ShowInterestedCommunitiesOnProfile = table.Column<bool>(type: "boolean", nullable: false),
                    ShowCommunityRolesOnProfile = table.Column<bool>(type: "boolean", nullable: false),
                    DefaultMembershipVisibility = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PreferredContentLanguage = table.Column<string>(type: "character varying(35)", maxLength: 35, nullable: true),
                    NotificationsJson = table.Column<string>(type: "text", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_member_preferences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "community_notices",
                schema: "community_programme",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CommunityId = table.Column<Guid>(type: "uuid", nullable: false),
                    PublishedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    PublishedByRole = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Title = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: false),
                    Body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsPinned = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_notices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_community_notices_communities_CommunityId",
                        column: x => x.CommunityId,
                        principalSchema: "community_programme",
                        principalTable: "communities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "community_rules",
                schema: "community_programme",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CommunityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Consequence = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_rules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_community_rules_communities_CommunityId",
                        column: x => x.CommunityId,
                        principalSchema: "community_programme",
                        principalTable: "communities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_community_member_preferences_member",
                schema: "community_programme",
                table: "community_member_preferences",
                column: "MemberId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_community_notices_community_published",
                schema: "community_programme",
                table: "community_notices",
                columns: new[] { "CommunityId", "PublishedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_community_rules_community_position",
                schema: "community_programme",
                table: "community_rules",
                columns: new[] { "CommunityId", "Position" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "community_member_preferences",
                schema: "community_programme");

            migrationBuilder.DropTable(
                name: "community_notices",
                schema: "community_programme");

            migrationBuilder.DropTable(
                name: "community_rules",
                schema: "community_programme");
        }
    }
}
