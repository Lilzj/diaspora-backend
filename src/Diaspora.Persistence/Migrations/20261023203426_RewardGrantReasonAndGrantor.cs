using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diaspora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RewardGrantReasonAndGrantor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "GrantedByUserId",
                schema: "financial",
                table: "financial_reward_entries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                schema: "financial",
                table: "financial_reward_entries",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_financial_reward_entries_grantor",
                schema: "financial",
                table: "financial_reward_entries",
                columns: new[] { "GrantedByUserId", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_financial_reward_entries_grantor",
                schema: "financial",
                table: "financial_reward_entries");

            migrationBuilder.DropColumn(
                name: "GrantedByUserId",
                schema: "financial",
                table: "financial_reward_entries");

            migrationBuilder.DropColumn(
                name: "Reason",
                schema: "financial",
                table: "financial_reward_entries");
        }
    }
}
