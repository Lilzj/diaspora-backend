using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diaspora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AreaBoundaryProvenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "boundary_attribution",
                schema: "location",
                table: "administrative_areas",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "boundary_licence",
                schema: "location",
                table: "administrative_areas",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "boundary_retrieved_on",
                schema: "location",
                table: "administrative_areas",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "boundary_source",
                schema: "location",
                table: "administrative_areas",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "boundary_source_reference",
                schema: "location",
                table: "administrative_areas",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "boundary_attribution",
                schema: "location",
                table: "administrative_areas");

            migrationBuilder.DropColumn(
                name: "boundary_licence",
                schema: "location",
                table: "administrative_areas");

            migrationBuilder.DropColumn(
                name: "boundary_retrieved_on",
                schema: "location",
                table: "administrative_areas");

            migrationBuilder.DropColumn(
                name: "boundary_source",
                schema: "location",
                table: "administrative_areas");

            migrationBuilder.DropColumn(
                name: "boundary_source_reference",
                schema: "location",
                table: "administrative_areas");
        }
    }
}
