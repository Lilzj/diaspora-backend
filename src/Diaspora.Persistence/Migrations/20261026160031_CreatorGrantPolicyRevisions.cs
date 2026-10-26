using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diaspora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CreatorGrantPolicyRevisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing milestones and challenges are their first revision, as a new one is: the
            // default is 1, not 0, so every grant from now on names a revision that exists (RWD-13).
            migrationBuilder.AddColumn<int>(
                name: "PolicyRevision",
                schema: "creator",
                table: "creator_milestones",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "PolicyRevision",
                schema: "creator",
                table: "creator_challenges",
                type: "integer",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PolicyRevision",
                schema: "creator",
                table: "creator_milestones");

            migrationBuilder.DropColumn(
                name: "PolicyRevision",
                schema: "creator",
                table: "creator_challenges");
        }
    }
}
