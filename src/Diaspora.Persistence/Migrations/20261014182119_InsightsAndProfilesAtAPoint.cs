using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diaspora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InsightsAndProfilesAtAPoint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "latitude",
                schema: "housing",
                table: "housing_neighborhoods",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "longitude",
                schema: "housing",
                table: "housing_neighborhoods",
                type: "double precision",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ProfileId",
                schema: "housing",
                table: "housing_neighborhood_insights",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "AreaId",
                schema: "housing",
                table: "housing_neighborhood_insights",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "country_code",
                schema: "housing",
                table: "housing_neighborhood_insights",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "latitude",
                schema: "housing",
                table: "housing_neighborhood_insights",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "longitude",
                schema: "housing",
                table: "housing_neighborhood_insights",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "place_id",
                schema: "housing",
                table: "housing_neighborhood_insights",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "place_name",
                schema: "housing",
                table: "housing_neighborhood_insights",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            // Backfill (14 Oct 2026): every existing profile and every insight written about a
            // catalogue neighbourhood takes that neighbourhood's centre from the place catalogue,
            // which still exists until Phase 3 removes it. Rows whose area has no centre stay null,
            // and an area page simply does not find them. Idempotent: only null points are written.
            migrationBuilder.Sql("""
                UPDATE housing.housing_neighborhoods AS profile
                SET latitude = area.latitude,
                    longitude = area.longitude
                FROM location.administrative_areas AS area
                WHERE area."Id" = profile."AreaId"
                  AND profile.latitude IS NULL
                  AND area.latitude IS NOT NULL
                  AND area.longitude IS NOT NULL;
                """);

            migrationBuilder.Sql("""
                UPDATE housing.housing_neighborhood_insights AS insight
                SET latitude = area.latitude,
                    longitude = area.longitude
                FROM location.administrative_areas AS area
                WHERE area."Id" = insight."AreaId"
                  AND insight.latitude IS NULL
                  AND area.latitude IS NOT NULL
                  AND area.longitude IS NOT NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "ix_housing_neighborhoods_point",
                schema: "housing",
                table: "housing_neighborhoods",
                columns: new[] { "latitude", "longitude" });

            migrationBuilder.CreateIndex(
                name: "ix_housing_insights_point",
                schema: "housing",
                table: "housing_neighborhood_insights",
                columns: new[] { "latitude", "longitude" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_housing_neighborhoods_point",
                schema: "housing",
                table: "housing_neighborhoods");

            migrationBuilder.DropIndex(
                name: "ix_housing_insights_point",
                schema: "housing",
                table: "housing_neighborhood_insights");

            migrationBuilder.DropColumn(
                name: "latitude",
                schema: "housing",
                table: "housing_neighborhoods");

            migrationBuilder.DropColumn(
                name: "longitude",
                schema: "housing",
                table: "housing_neighborhoods");

            migrationBuilder.DropColumn(
                name: "country_code",
                schema: "housing",
                table: "housing_neighborhood_insights");

            migrationBuilder.DropColumn(
                name: "latitude",
                schema: "housing",
                table: "housing_neighborhood_insights");

            migrationBuilder.DropColumn(
                name: "longitude",
                schema: "housing",
                table: "housing_neighborhood_insights");

            migrationBuilder.DropColumn(
                name: "place_id",
                schema: "housing",
                table: "housing_neighborhood_insights");

            migrationBuilder.DropColumn(
                name: "place_name",
                schema: "housing",
                table: "housing_neighborhood_insights");

            migrationBuilder.AlterColumn<Guid>(
                name: "ProfileId",
                schema: "housing",
                table: "housing_neighborhood_insights",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "AreaId",
                schema: "housing",
                table: "housing_neighborhood_insights",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
