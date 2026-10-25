using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diaspora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RewardPointsReversal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsReversal",
                schema: "financial",
                table: "financial_reward_entries",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReversalDeclinedAt",
                schema: "financial",
                table: "financial_reward_entries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReversalReason",
                schema: "financial",
                table: "financial_reward_entries",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReversedAt",
                schema: "financial",
                table: "financial_reward_entries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "LifetimeReversed",
                schema: "financial",
                table: "financial_reward_accounts",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsReversal",
                schema: "financial",
                table: "financial_reward_entries");

            migrationBuilder.DropColumn(
                name: "ReversalDeclinedAt",
                schema: "financial",
                table: "financial_reward_entries");

            migrationBuilder.DropColumn(
                name: "ReversalReason",
                schema: "financial",
                table: "financial_reward_entries");

            migrationBuilder.DropColumn(
                name: "ReversedAt",
                schema: "financial",
                table: "financial_reward_entries");

            migrationBuilder.DropColumn(
                name: "LifetimeReversed",
                schema: "financial",
                table: "financial_reward_accounts");
        }
    }
}
