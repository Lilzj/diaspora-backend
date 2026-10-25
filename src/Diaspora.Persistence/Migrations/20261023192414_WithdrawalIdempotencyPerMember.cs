using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diaspora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WithdrawalIdempotencyPerMember : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_financial_withdrawals_idempotency",
                schema: "financial",
                table: "financial_withdrawals");

            migrationBuilder.CreateIndex(
                name: "ix_financial_withdrawals_requester_idempotency",
                schema: "financial",
                table: "financial_withdrawals",
                columns: new[] { "RequestedByUserId", "IdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_financial_withdrawals_requester_idempotency",
                schema: "financial",
                table: "financial_withdrawals");

            migrationBuilder.CreateIndex(
                name: "ix_financial_withdrawals_idempotency",
                schema: "financial",
                table: "financial_withdrawals",
                column: "IdempotencyKey",
                unique: true);
        }
    }
}
