using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diaspora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AnalyticsEnquiries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "enquiries",
                schema: "analytics",
                table: "analytics_owner_daily",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "enquiries",
                schema: "analytics",
                table: "analytics_content_daily",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "enquiries",
                schema: "analytics",
                table: "analytics_owner_daily");

            migrationBuilder.DropColumn(
                name: "enquiries",
                schema: "analytics",
                table: "analytics_content_daily");
        }
    }
}
