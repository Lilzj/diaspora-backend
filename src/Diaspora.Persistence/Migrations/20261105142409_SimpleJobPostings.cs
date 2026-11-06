using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diaspora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SimpleJobPostings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "length_amount",
                schema: "jobs",
                table: "job_postings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "length_unit",
                schema: "jobs",
                table: "job_postings",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "posting_mode",
                schema: "jobs",
                table: "job_postings",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                // Every posting before simple jobs is a full one.
                defaultValue: "Standard");

            migrationBuilder.AddColumn<DateOnly>(
                name: "starts_on",
                schema: "jobs",
                table: "job_postings",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "length_amount",
                schema: "jobs",
                table: "job_postings");

            migrationBuilder.DropColumn(
                name: "length_unit",
                schema: "jobs",
                table: "job_postings");

            migrationBuilder.DropColumn(
                name: "posting_mode",
                schema: "jobs",
                table: "job_postings");

            migrationBuilder.DropColumn(
                name: "starts_on",
                schema: "jobs",
                table: "job_postings");
        }
    }
}
