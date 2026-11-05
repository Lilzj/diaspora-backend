using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diaspora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WorldTowns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "world_towns",
                schema: "location",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GeoNameId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false),
                    Population = table.Column<long>(type: "bigint", nullable: false),
                    SearchNames = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_world_towns", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_world_towns_country_population",
                schema: "location",
                table: "world_towns",
                columns: new[] { "CountryCode", "Population" });

            migrationBuilder.CreateIndex(
                name: "ix_world_towns_geoname",
                schema: "location",
                table: "world_towns",
                column: "GeoNameId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "world_towns",
                schema: "location");
        }
    }
}
