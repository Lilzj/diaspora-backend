using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diaspora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PlatformTransactionReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "transaction_receipts",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    CreatedAtTicks = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transaction_receipts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_transaction_receipts_created_at",
                schema: "platform",
                table: "transaction_receipts",
                column: "CreatedAtTicks");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "transaction_receipts",
                schema: "platform");
        }
    }
}
