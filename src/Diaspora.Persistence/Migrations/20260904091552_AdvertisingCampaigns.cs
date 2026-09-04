using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diaspora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdvertisingCampaigns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "financial_ad_campaigns",
                schema: "financial",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Objective = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    BudgetMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    SpendMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Exponent = table.Column<int>(type: "integer", nullable: false),
                    SettledMinorUnits = table.Column<long>(type: "bigint", nullable: true),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AudienceCountryCodes = table.Column<string[]>(type: "text[]", nullable: false),
                    AudienceCommunityIds = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    AudienceNeighbourhoodIds = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    AudienceInterests = table.Column<string[]>(type: "text[]", nullable: false),
                    AudienceCategories = table.Column<string[]>(type: "text[]", nullable: false),
                    CreativeMediaId = table.Column<Guid>(type: "uuid", nullable: true),
                    Headline = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Body = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SubjectType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProductCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    PurchaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewDecision = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ReviewNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReviewedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AppealNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AppealedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ActivatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PausedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EndedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EndReason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_ad_campaigns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "financial_ad_campaign_placements",
                schema: "financial",
                columns: table => new
                {
                    PlacementId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AdCampaignId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_ad_campaign_placements", x => x.PlacementId);
                    table.ForeignKey(
                        name: "FK_financial_ad_campaign_placements_financial_ad_campaigns_AdC~",
                        column: x => x.AdCampaignId,
                        principalSchema: "financial",
                        principalTable: "financial_ad_campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_financial_ad_campaign_placements_AdCampaignId",
                schema: "financial",
                table: "financial_ad_campaign_placements",
                column: "AdCampaignId");

            migrationBuilder.CreateIndex(
                name: "ix_financial_ad_campaigns_ends",
                schema: "financial",
                table: "financial_ad_campaigns",
                column: "EndsAt");

            migrationBuilder.CreateIndex(
                name: "ix_financial_ad_campaigns_owner",
                schema: "financial",
                table: "financial_ad_campaigns",
                columns: new[] { "OwnerUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_financial_ad_campaigns_status",
                schema: "financial",
                table: "financial_ad_campaigns",
                columns: new[] { "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "financial_ad_campaign_placements",
                schema: "financial");

            migrationBuilder.DropTable(
                name: "financial_ad_campaigns",
                schema: "financial");
        }
    }
}
