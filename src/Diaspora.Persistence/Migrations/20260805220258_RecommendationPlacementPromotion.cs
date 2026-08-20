using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diaspora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RecommendationPlacementPromotion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PromotedUntil",
                schema: "recommendations",
                table: "recommendation_candidates",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PromotionWeight",
                schema: "recommendations",
                table: "recommendation_candidates",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PromotedUntil",
                schema: "recommendations",
                table: "recommendation_candidates");

            migrationBuilder.DropColumn(
                name: "PromotionWeight",
                schema: "recommendations",
                table: "recommendation_candidates");
        }
    }
}
