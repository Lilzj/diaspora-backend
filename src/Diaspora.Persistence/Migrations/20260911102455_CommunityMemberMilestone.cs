using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diaspora.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CommunityMemberMilestone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "HighestMemberMilestone",
                schema: "community_programme",
                table: "communities",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // ── Backfill, or every existing community announces a milestone it passed long ago ──
            //
            // A zero here would read as "has never reached anything", so the next person to join a
            // community already at 150 members would set off "this community reached 100 members".
            // Each row starts at the largest milestone its count has already passed. The list must
            // match `Community.MemberMilestones`; `CommunityMilestoneTests` checks that it does.
            migrationBuilder.Sql("""
                UPDATE community_programme.communities
                SET "HighestMemberMilestone" = CASE
                    WHEN "MemberCount" >= 100000 THEN 100000
                    WHEN "MemberCount" >= 50000 THEN 50000
                    WHEN "MemberCount" >= 25000 THEN 25000
                    WHEN "MemberCount" >= 10000 THEN 10000
                    WHEN "MemberCount" >= 5000 THEN 5000
                    WHEN "MemberCount" >= 2500 THEN 2500
                    WHEN "MemberCount" >= 1000 THEN 1000
                    WHEN "MemberCount" >= 500 THEN 500
                    WHEN "MemberCount" >= 250 THEN 250
                    WHEN "MemberCount" >= 100 THEN 100
                    WHEN "MemberCount" >= 50 THEN 50
                    WHEN "MemberCount" >= 25 THEN 25
                    WHEN "MemberCount" >= 10 THEN 10
                    ELSE 0
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HighestMemberMilestone",
                schema: "community_programme",
                table: "communities");
        }
    }
}
