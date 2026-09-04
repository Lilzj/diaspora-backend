using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diaspora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Communities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "communities",
                schema: "community_programme",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Tagline = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Visibility = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    OriginCountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    OriginRegionName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    PrimaryLanguage = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    PresenceCountryCodes = table.Column<string[]>(type: "text[]", nullable: false),
                    SupportedLanguages = table.Column<string[]>(type: "text[]", nullable: false),
                    MemberCount = table.Column<int>(type: "integer", nullable: false),
                    StatusReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_communities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "community_memberships",
                schema: "community_programme",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CommunityId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    JoinedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EndedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DecisionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DecidedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_memberships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_community_memberships_communities_CommunityId",
                        column: x => x.CommunityId,
                        principalSchema: "community_programme",
                        principalTable: "communities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_communities_origin_country",
                schema: "community_programme",
                table: "communities",
                column: "OriginCountryCode");

            migrationBuilder.CreateIndex(
                name: "ix_communities_slug",
                schema: "community_programme",
                table: "communities",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_communities_status_visibility",
                schema: "community_programme",
                table: "communities",
                columns: new[] { "Status", "Visibility" });

            migrationBuilder.CreateIndex(
                name: "ix_communities_type",
                schema: "community_programme",
                table: "communities",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "ix_community_memberships_community_member",
                schema: "community_programme",
                table: "community_memberships",
                columns: new[] { "CommunityId", "MemberId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_community_memberships_member_state",
                schema: "community_programme",
                table: "community_memberships",
                columns: new[] { "MemberId", "State" });

            migrationBuilder.CreateIndex(
                name: "ix_community_memberships_one_primary_per_member",
                schema: "community_programme",
                table: "community_memberships",
                column: "MemberId",
                unique: true,
                filter: "\"IsPrimary\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "community_memberships",
                schema: "community_programme");

            migrationBuilder.DropTable(
                name: "communities",
                schema: "community_programme");
        }
    }
}
