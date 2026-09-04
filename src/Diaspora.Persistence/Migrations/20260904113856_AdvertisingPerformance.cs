using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diaspora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdvertisingPerformance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "financial_ad_performance_days",
                schema: "financial",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlacementId = table.Column<Guid>(type: "uuid", nullable: true),
                    Surface = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    Impressions = table.Column<long>(type: "bigint", nullable: false),
                    Clicks = table.Column<long>(type: "bigint", nullable: false),
                    Conversions = table.Column<long>(type: "bigint", nullable: false),
                    SpendMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Exponent = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_ad_performance_days", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_financial_ad_campaigns_subject",
                schema: "financial",
                table: "financial_ad_campaigns",
                columns: new[] { "SubjectType", "SubjectId" });

            migrationBuilder.CreateIndex(
                name: "ix_financial_ad_performance_bucket",
                schema: "financial",
                table: "financial_ad_performance_days",
                columns: new[] { "CampaignId", "PlacementId", "Surface", "Day" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_financial_ad_performance_campaign_day",
                schema: "financial",
                table: "financial_ad_performance_days",
                columns: new[] { "CampaignId", "Day" });

            migrationBuilder.CreateIndex(
                name: "ix_financial_ad_performance_placement_day",
                schema: "financial",
                table: "financial_ad_performance_days",
                columns: new[] { "PlacementId", "Day" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "financial_ad_performance_days",
                schema: "financial");

            migrationBuilder.DropIndex(
                name: "ix_financial_ad_campaigns_subject",
                schema: "financial",
                table: "financial_ad_campaigns");
        }
    }
}
