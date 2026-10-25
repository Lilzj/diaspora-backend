using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diaspora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RewardConversionHold : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AvailableAt",
                schema: "financial",
                table: "financial_reward_conversions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReleasedAt",
                schema: "financial",
                table: "financial_reward_conversions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_financial_reward_conversions_release",
                schema: "financial",
                table: "financial_reward_conversions",
                columns: new[] { "ReleasedAt", "AvailableAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_financial_reward_conversions_release",
                schema: "financial",
                table: "financial_reward_conversions");

            migrationBuilder.DropColumn(
                name: "AvailableAt",
                schema: "financial",
                table: "financial_reward_conversions");

            migrationBuilder.DropColumn(
                name: "ReleasedAt",
                schema: "financial",
                table: "financial_reward_conversions");
        }
    }
}
