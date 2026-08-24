using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diaspora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SellerReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "rating_five_star",
                schema: "marketplace",
                table: "marketplace_sellers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "rating_four_star",
                schema: "marketplace",
                table: "marketplace_sellers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "rating_one_star",
                schema: "marketplace",
                table: "marketplace_sellers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "rating_three_star",
                schema: "marketplace",
                table: "marketplace_sellers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "rating_two_star",
                schema: "marketplace",
                table: "marketplace_sellers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "marketplace_seller_reviews",
                schema: "marketplace",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SellerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReservationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ListingId = table.Column<Guid>(type: "uuid", nullable: false),
                    Rating = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    Body = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Response = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    RespondedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EditedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_marketplace_seller_reviews", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_marketplace_seller_reviews_reservation",
                schema: "marketplace",
                table: "marketplace_seller_reviews",
                column: "ReservationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_marketplace_seller_reviews_reviewer",
                schema: "marketplace",
                table: "marketplace_seller_reviews",
                column: "ReviewerId");

            migrationBuilder.CreateIndex(
                name: "ix_marketplace_seller_reviews_seller_status",
                schema: "marketplace",
                table: "marketplace_seller_reviews",
                columns: new[] { "SellerId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "marketplace_seller_reviews",
                schema: "marketplace");

            migrationBuilder.DropColumn(
                name: "rating_five_star",
                schema: "marketplace",
                table: "marketplace_sellers");

            migrationBuilder.DropColumn(
                name: "rating_four_star",
                schema: "marketplace",
                table: "marketplace_sellers");

            migrationBuilder.DropColumn(
                name: "rating_one_star",
                schema: "marketplace",
                table: "marketplace_sellers");

            migrationBuilder.DropColumn(
                name: "rating_three_star",
                schema: "marketplace",
                table: "marketplace_sellers");

            migrationBuilder.DropColumn(
                name: "rating_two_star",
                schema: "marketplace",
                table: "marketplace_sellers");
        }
    }
}
