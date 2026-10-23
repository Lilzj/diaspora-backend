using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diaspora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReputationAwardKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AwardKey",
                schema: "trust",
                table: "reputation_events",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_reputation_events_profile_award_key",
                schema: "trust",
                table: "reputation_events",
                columns: new[] { "ProfileId", "AwardKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_reputation_events_profile_award_key",
                schema: "trust",
                table: "reputation_events");

            migrationBuilder.DropColumn(
                name: "AwardKey",
                schema: "trust",
                table: "reputation_events");
        }
    }
}
