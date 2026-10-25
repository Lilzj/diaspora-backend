using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diaspora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RewardPointsVesting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "VestedAt",
                schema: "financial",
                table: "financial_reward_entries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "VestsAt",
                schema: "financial",
                table: "financial_reward_entries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PendingBalance",
                schema: "financial",
                table: "financial_reward_accounts",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "ix_financial_reward_entries_vesting",
                schema: "financial",
                table: "financial_reward_entries",
                columns: new[] { "VestedAt", "VestsAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_financial_reward_entries_vesting",
                schema: "financial",
                table: "financial_reward_entries");

            migrationBuilder.DropColumn(
                name: "VestedAt",
                schema: "financial",
                table: "financial_reward_entries");

            migrationBuilder.DropColumn(
                name: "VestsAt",
                schema: "financial",
                table: "financial_reward_entries");

            migrationBuilder.DropColumn(
                name: "PendingBalance",
                schema: "financial",
                table: "financial_reward_accounts");
        }
    }
}
