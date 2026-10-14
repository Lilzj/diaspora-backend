using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diaspora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TownNamesReplaceCatalogueAreas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CityName",
                schema: "providers",
                table: "provider_service_areas",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string[]>(
                name: "AudienceTownNames",
                schema: "financial",
                table: "financial_ad_campaigns",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            migrationBuilder.AddColumn<string>(
                name: "CityName",
                schema: "community_programme",
                table: "community_polls",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CityName",
                schema: "providers",
                table: "provider_service_areas");

            migrationBuilder.DropColumn(
                name: "AudienceTownNames",
                schema: "financial",
                table: "financial_ad_campaigns");

            migrationBuilder.DropColumn(
                name: "CityName",
                schema: "community_programme",
                table: "community_polls");
        }
    }
}
