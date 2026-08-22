using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diaspora.Persistence.Migrations
{
    /// <summary>
    /// BG-05a. Community's own record of who exists, so a member can be reported.
    /// <para>
    /// The table is fed forward by <c>UserRegisteredCommunityConsumer</c>. This migration also
    /// backfills it, and the backfill is not optional: an outbox holds no history, so without it
    /// the projection would know only accounts created after deployment and reporting an
    /// established member would answer <c>community.content.not-found</c> — the same failure
    /// BG-05a exists to remove, arriving from the opposite direction. The first attempt at member
    /// reporting made precisely this mistake one layer down, against trust profiles, and it landed
    /// hardest on the newest accounts.
    /// </para>
    /// <para>
    /// <b>The one place Community's schema and Identity's meet, and it is here rather than in the
    /// module for a reason.</b> A migration is infrastructure: it runs once, outside the composed
    /// application, and holds no module's dependency graph. The rule the architecture tests enforce
    /// is that <c>Diaspora.Modules.Community</c> must not reference <c>Diaspora.Domain.Identity</c>,
    /// and it does not — nothing in the module knows this statement exists.
    /// </para>
    /// </summary>
    /// <inheritdoc />
    public partial class ProjectCommunityMembers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "community_members",
                schema: "community",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    RegisteredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_members", x => x.UserId);
                });

            migrationBuilder.Sql(
                """
                -- Every member who registered before this projection existed.
                --
                -- COALESCE on the name because the projection renders it in a moderation queue and
                -- an empty cell there reads as a broken row rather than as a missing name; the
                -- domain applies the same fallback, in CommunityMember.Name.
                --
                -- CreatedAt is the registration moment. It is what the consumer records from the
                -- integration event's OccurredAt for everybody after this, so the column means the
                -- same thing on both sides of the backfill.
                --
                -- ON CONFLICT DO NOTHING: the consumer may already be running by the time this
                -- executes, and a member projected twice is a failed deployment rather than a
                -- duplicate row.
                INSERT INTO community.community_members ("UserId", "DisplayName", "CountryCode", "RegisteredAt")
                SELECT u."Id",
                       COALESCE(NULLIF(btrim(u."FullName"), ''), 'Member'),
                       NULLIF(btrim(u."CountryOfResidence"), ''),
                       u."CreatedAt"
                FROM identity.users u
                ON CONFLICT ("UserId") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "community_members",
                schema: "community");
        }
    }
}
