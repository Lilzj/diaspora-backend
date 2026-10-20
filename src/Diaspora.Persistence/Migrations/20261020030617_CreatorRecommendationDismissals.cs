using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diaspora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CreatorRecommendationDismissals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "creator_recommendation_dismissals",
                schema: "creator",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecommendationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    DismissedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_creator_recommendation_dismissals", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ux_creator_recommendation_dismissal",
                schema: "creator",
                table: "creator_recommendation_dismissals",
                columns: new[] { "UserId", "RecommendationId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "creator_recommendation_dismissals",
                schema: "creator");
        }
    }
}
