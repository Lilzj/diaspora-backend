using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diaspora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FinancialInvoices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "financial_invoices",
                schema: "financial",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentIntentId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceKind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Description = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    ListMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    DiscountMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    TotalMinorUnits = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Exponent = table.Column<int>(type: "integer", nullable: false),
                    PromotionCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SubjectType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    PeriodStart = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PeriodEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PaidAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IssuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_invoices", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_financial_invoices_owner",
                schema: "financial",
                table: "financial_invoices",
                columns: new[] { "OwnerUserId", "Year", "Sequence" });

            migrationBuilder.CreateIndex(
                name: "ux_financial_invoices_number",
                schema: "financial",
                table: "financial_invoices",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_financial_invoices_payment",
                schema: "financial",
                table: "financial_invoices",
                column: "PaymentIntentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_financial_invoices_sequence",
                schema: "financial",
                table: "financial_invoices",
                columns: new[] { "Year", "Sequence" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "financial_invoices",
                schema: "financial");
        }
    }
}
