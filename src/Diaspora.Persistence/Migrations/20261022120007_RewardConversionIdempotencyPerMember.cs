using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diaspora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RewardConversionIdempotencyPerMember : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_financial_reward_conversions_idempotency",
                schema: "financial",
                table: "financial_reward_conversions");

            migrationBuilder.CreateIndex(
                name: "ix_financial_reward_conversions_member_idempotency",
                schema: "financial",
                table: "financial_reward_conversions",
                columns: new[] { "MemberUserId", "IdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_financial_reward_conversions_member_idempotency",
                schema: "financial",
                table: "financial_reward_conversions");

            migrationBuilder.CreateIndex(
                name: "ix_financial_reward_conversions_idempotency",
                schema: "financial",
                table: "financial_reward_conversions",
                column: "IdempotencyKey",
                unique: true);
        }
    }
}
