using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diaspora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CommunityClosure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "community_deletion_requests",
                schema: "community_programme",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CommunityId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EffectiveAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ReviewedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DecisionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_deletion_requests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_community_deletion_requests_communities_CommunityId",
                        column: x => x.CommunityId,
                        principalSchema: "community_programme",
                        principalTable: "communities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "community_ownership_transfers",
                schema: "community_programme",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CommunityId = table.Column<Guid>(type: "uuid", nullable: false),
                    OfferedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    OfferedTo = table.Column<Guid>(type: "uuid", nullable: false),
                    Message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OfferedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RespondedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeclineReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_ownership_transfers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_community_ownership_transfers_communities_CommunityId",
                        column: x => x.CommunityId,
                        principalSchema: "community_programme",
                        principalTable: "communities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_community_deletion_requests_community_status",
                schema: "community_programme",
                table: "community_deletion_requests",
                columns: new[] { "CommunityId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_community_deletion_requests_status_effective",
                schema: "community_programme",
                table: "community_deletion_requests",
                columns: new[] { "Status", "EffectiveAt" });

            migrationBuilder.CreateIndex(
                name: "ix_community_ownership_transfers_community_status",
                schema: "community_programme",
                table: "community_ownership_transfers",
                columns: new[] { "CommunityId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_community_ownership_transfers_offered_to_status",
                schema: "community_programme",
                table: "community_ownership_transfers",
                columns: new[] { "OfferedTo", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_community_ownership_transfers_status_expiry",
                schema: "community_programme",
                table: "community_ownership_transfers",
                columns: new[] { "Status", "ExpiresAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "community_deletion_requests",
                schema: "community_programme");

            migrationBuilder.DropTable(
                name: "community_ownership_transfers",
                schema: "community_programme");
        }
    }
}
