using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diaspora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CoordinatesOnEveryListing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "latitude",
                schema: "marketplace",
                table: "marketplace_listings",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "longitude",
                schema: "marketplace",
                table: "marketplace_listings",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "latitude",
                schema: "jobs",
                table: "job_postings",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "longitude",
                schema: "jobs",
                table: "job_postings",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "latitude",
                schema: "guides",
                table: "guides",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "longitude",
                schema: "guides",
                table: "guides",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "latitude",
                schema: "community_life",
                table: "community_life_organizations",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "longitude",
                schema: "community_life",
                table: "community_life_organizations",
                type: "double precision",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_marketplace_listings_coordinates",
                schema: "marketplace",
                table: "marketplace_listings",
                columns: new[] { "latitude", "longitude" });

            migrationBuilder.CreateIndex(
                name: "ix_job_postings_coordinates",
                schema: "jobs",
                table: "job_postings",
                columns: new[] { "latitude", "longitude" });

            migrationBuilder.CreateIndex(
                name: "ix_housing_listings_coordinates",
                schema: "housing",
                table: "housing_listings",
                columns: new[] { "latitude", "longitude" });

            migrationBuilder.CreateIndex(
                name: "ix_guides_coordinates",
                schema: "guides",
                table: "guides",
                columns: new[] { "latitude", "longitude" });

            migrationBuilder.CreateIndex(
                name: "ix_community_posts_coordinates",
                schema: "community",
                table: "community_posts",
                columns: new[] { "latitude", "longitude" });

            migrationBuilder.CreateIndex(
                name: "ix_community_life_organizations_coordinates",
                schema: "community_life",
                table: "community_life_organizations",
                columns: new[] { "latitude", "longitude" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_marketplace_listings_coordinates",
                schema: "marketplace",
                table: "marketplace_listings");

            migrationBuilder.DropIndex(
                name: "ix_job_postings_coordinates",
                schema: "jobs",
                table: "job_postings");

            migrationBuilder.DropIndex(
                name: "ix_housing_listings_coordinates",
                schema: "housing",
                table: "housing_listings");

            migrationBuilder.DropIndex(
                name: "ix_guides_coordinates",
                schema: "guides",
                table: "guides");

            migrationBuilder.DropIndex(
                name: "ix_community_posts_coordinates",
                schema: "community",
                table: "community_posts");

            migrationBuilder.DropIndex(
                name: "ix_community_life_organizations_coordinates",
                schema: "community_life",
                table: "community_life_organizations");

            migrationBuilder.DropColumn(
                name: "latitude",
                schema: "marketplace",
                table: "marketplace_listings");

            migrationBuilder.DropColumn(
                name: "longitude",
                schema: "marketplace",
                table: "marketplace_listings");

            migrationBuilder.DropColumn(
                name: "latitude",
                schema: "jobs",
                table: "job_postings");

            migrationBuilder.DropColumn(
                name: "longitude",
                schema: "jobs",
                table: "job_postings");

            migrationBuilder.DropColumn(
                name: "latitude",
                schema: "guides",
                table: "guides");

            migrationBuilder.DropColumn(
                name: "longitude",
                schema: "guides",
                table: "guides");

            migrationBuilder.DropColumn(
                name: "latitude",
                schema: "community_life",
                table: "community_life_organizations");

            migrationBuilder.DropColumn(
                name: "longitude",
                schema: "community_life",
                table: "community_life_organizations");
        }
    }
}
