using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diaspora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProviderUsageAllowance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "provider_usage",
                schema: "platform",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Provider = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Sku = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Period = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    Used = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_provider_usage", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "provider_usage",
                schema: "platform");
        }
    }
}
