using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diaspora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MoneyDueProcess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PreviousDecidedByUserId",
                schema: "financial",
                table: "financial_refund_requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReopenedAt",
                schema: "financial",
                table: "financial_refund_requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "financial_credit_grant_requests",
                schema: "financial",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AmountMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Exponent = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RequestedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DecidedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DecisionNote = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CreditGrantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_credit_grant_requests", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_financial_credit_grant_requests_status",
                schema: "financial",
                table: "financial_credit_grant_requests",
                columns: new[] { "Status", "RequestedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "financial_credit_grant_requests",
                schema: "financial");

            migrationBuilder.DropColumn(
                name: "PreviousDecidedByUserId",
                schema: "financial",
                table: "financial_refund_requests");

            migrationBuilder.DropColumn(
                name: "ReopenedAt",
                schema: "financial",
                table: "financial_refund_requests");
        }
    }
}
