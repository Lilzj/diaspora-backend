using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diaspora.Persistence.Migrations
{
    /// <summary>
    /// BG-10. Re-keys creator activity that was recorded against a <em>profile</em> identifier to the
    /// <em>member</em> who owns that profile, and removes the creator records that were minted for
    /// the profiles.
    /// <para>
    /// <b>What went wrong.</b> Three consumers passed the wrong identifier into
    /// <c>CreatorActivityRecorder.RecordAsync</c>, which expects a member:
    /// <c>ListingPublishedCreatorConsumer</c> passed <c>SellerId</c>,
    /// <c>JobPublishedCreatorConsumer</c> passed <c>EmployerId</c>, and
    /// <c>HousingPublishedCreatorConsumer</c> passed <c>LandlordId</c>. None of the three is a
    /// person. The recorder opens a creator record for whatever identifier it is handed, so each of
    /// those publications both failed to credit the member and minted a creator profile keyed to a
    /// seller, landlord or employer profile id. Counts, milestones, leaderboards and reward points
    /// are all computed from <c>creator_activity_entries</c>, so a member who sells was not being
    /// credited and a phantom was.
    /// </para>
    /// <para>
    /// <b>Why a migration rather than a backfill job.</b> The corruption is historical and bounded —
    /// it stops the moment the consumers ship — so the repair wants to run exactly once, in order,
    /// against the same schema version that introduced the fix. That is what a migration is.
    /// </para>
    /// <para>
    /// <b>No creator record is invented.</b> Where the owning member already has a creator profile
    /// the entries move to it and the phantom is deleted. Where the member has none, the phantom is
    /// <em>adopted</em> — its <c>UserId</c> is rewritten to the member — rather than a replacement
    /// being synthesised here with hand-written defaults that would drift from
    /// <c>CreatorProfile.Open</c>. Adoption also preserves the counters the phantom had already
    /// accumulated, which are the member's counters and always were.
    /// </para>
    /// <para>
    /// <b>The three derived counters are recomputed, not adjusted.</b> <c>RecognisedActions</c>,
    /// <c>WeightedContributionScore</c> and <c>TotalContentPublished</c> are written in exactly one
    /// place — <c>CreatorProfile.RecordActivity</c> — so they are a pure function of the entries and
    /// recomputing them from the entries is the definition rather than a guess. Only profiles this
    /// migration touched are recomputed: a profile with drift from some other cause is a different
    /// defect and is not silently absorbed into this one.
    /// </para>
    /// <para>
    /// <b>Not reversible.</b> See <see cref="Down"/>.
    /// </para>
    /// </summary>
    /// <inheritdoc />
    public partial class RepairCreatorActivityAttribution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                -- ─────────────────────────────────────────────────────────────────────────────────
                -- 1. The plan.
                --
                -- Every activity entry whose UserId resolves to no member, paired with the member who
                -- owns the profile that identifier actually belongs to, and with that member's own
                -- creator profile where they have one.
                --
                -- The three LEFT JOINs are the three defective consumers. An entry that matches none
                -- of them has a UserId that is neither a member nor any profile this migration knows
                -- how to resolve; it is left exactly as it is rather than guessed at.
                -- ─────────────────────────────────────────────────────────────────────────────────
                CREATE TEMPORARY TABLE bg10_plan AS
                SELECT e."Id"                                        AS entry_id,
                       e."CreatorId"                                 AS phantom_creator_id,
                       e."SourceReference"                           AS source_reference,
                       COALESCE(s."OwnerUserId", l."OwnerUserId", p."OwnerUserId") AS owner_user_id,
                       tc."Id"                                       AS target_creator_id
                FROM creator.creator_activity_entries e
                LEFT JOIN marketplace.marketplace_sellers s ON s."Id" = e."UserId"
                LEFT JOIN housing.housing_landlords       l ON l."Id" = e."UserId"
                LEFT JOIN jobs.job_employer_profiles      p ON p."Id" = e."UserId"
                LEFT JOIN creator.creator_profiles       tc
                       ON tc."UserId" = COALESCE(s."OwnerUserId", l."OwnerUserId", p."OwnerUserId")
                WHERE NOT EXISTS (SELECT 1 FROM identity.users u WHERE u."Id" = e."UserId")
                  AND COALESCE(s."OwnerUserId", l."OwnerUserId", p."OwnerUserId") IS NOT NULL;

                -- Profiles this migration touches, captured before anything moves. Both sides: the
                -- phantoms that lose entries and the real profiles that gain them.
                CREATE TEMPORARY TABLE bg10_touched AS
                SELECT DISTINCT phantom_creator_id AS creator_id FROM bg10_plan
                UNION
                SELECT DISTINCT target_creator_id  FROM bg10_plan WHERE target_creator_id IS NOT NULL;

                -- ─────────────────────────────────────────────────────────────────────────────────
                -- 2. Adoption.
                --
                -- A phantom whose owning member has no creator profile becomes that member's profile.
                -- One phantom per member — the lowest id, chosen deterministically so a re-run picks
                -- the same one — because "UserId" is uniquely indexed and two phantoms adopting the
                -- same member would collide. Any further phantoms for that member fall through to
                -- step 3 and merge into the newly adopted profile.
                --
                -- array_agg rather than min: PostgreSQL has no min(uuid) aggregate, and casting to
                -- text to get one would order by text rather than by uuid. The ordered take is the
                -- same choice without the detour.
                -- ─────────────────────────────────────────────────────────────────────────────────
                WITH adoptable AS (
                    SELECT owner_user_id,
                           (ARRAY_AGG(phantom_creator_id ORDER BY phantom_creator_id))[1]
                               AS phantom_creator_id
                    FROM bg10_plan
                    WHERE target_creator_id IS NULL
                    GROUP BY owner_user_id
                )
                UPDATE creator.creator_profiles c
                SET "UserId"    = a.owner_user_id,
                    "UpdatedAt" = now()
                FROM adoptable a
                WHERE c."Id" = a.phantom_creator_id;

                -- The plan now knows where those entries belong.
                UPDATE bg10_plan pl
                SET target_creator_id = c."Id"
                FROM creator.creator_profiles c
                WHERE pl.target_creator_id IS NULL
                  AND c."UserId" = pl.owner_user_id;

                -- ─────────────────────────────────────────────────────────────────────────────────
                -- 3. Collisions.
                --
                -- ux_creator_activity_source is UNIQUE on (CreatorId, SourceReference). If the target
                -- already holds an entry for the same source event then the mis-keyed row is a
                -- duplicate of a correctly-attributed one and moving it would violate the index.
                -- Deleting it is the right answer: the source event is already credited once, and
                -- once is what the index exists to guarantee.
                -- ─────────────────────────────────────────────────────────────────────────────────
                DELETE FROM creator.creator_activity_entries e
                USING bg10_plan pl
                WHERE e."Id" = pl.entry_id
                  AND pl.target_creator_id IS NOT NULL
                  AND pl.target_creator_id <> pl.phantom_creator_id
                  AND EXISTS (
                      SELECT 1 FROM creator.creator_activity_entries x
                      WHERE x."CreatorId"       = pl.target_creator_id
                        AND x."SourceReference" = pl.source_reference);

                -- ─────────────────────────────────────────────────────────────────────────────────
                -- 4. The re-key.
                --
                -- Both columns move together. UserId is the member the entry is about; CreatorId is
                -- the profile the leaderboards and milestones read. An entry with one corrected and
                -- not the other would be a third state neither the old code nor the new code can
                -- produce.
                -- ─────────────────────────────────────────────────────────────────────────────────
                UPDATE creator.creator_activity_entries e
                SET "UserId"    = pl.owner_user_id,
                    "CreatorId" = pl.target_creator_id
                FROM bg10_plan pl
                WHERE e."Id" = pl.entry_id
                  AND pl.target_creator_id IS NOT NULL;

                -- ─────────────────────────────────────────────────────────────────────────────────
                -- 5. The counters.
                --
                -- Recomputed from the entries for the touched profiles only. The publication list is
                -- CreatorActivityTypes.Publications; it is spelled out here because a migration is
                -- pinned to the schema at its own moment in history and must not follow a constant
                -- that a later release may extend. CreatorActivityAttributionTests asserts the two
                -- agree today.
                -- ─────────────────────────────────────────────────────────────────────────────────
                UPDATE creator.creator_profiles c
                SET "RecognisedActions"         = COALESCE(agg.actions, 0),
                    "WeightedContributionScore" = COALESCE(agg.weight, 0),
                    "TotalContentPublished"     = COALESCE(agg.publications, 0),
                    "UpdatedAt"                 = now()
                FROM bg10_touched t
                LEFT JOIN LATERAL (
                    SELECT count(*)                       AS actions,
                           sum(GREATEST(e."Weight", 0))   AS weight,
                           count(*) FILTER (WHERE e."ActionType" IN (
                               'guide.published', 'post.published', 'event.published',
                               'provider.published', 'listing.published', 'job.published',
                               'housing.published')) AS publications
                    FROM creator.creator_activity_entries e
                    WHERE e."CreatorId" = t.creator_id
                ) agg ON TRUE
                WHERE c."Id" = t.creator_id;

                -- ─────────────────────────────────────────────────────────────────────────────────
                -- 6. The phantoms.
                --
                -- Only those left holding nothing at all. A profile that still owns an entry, a
                -- milestone, a certificate or a grant application is not deleted here — it would mean
                -- this migration had missed something, and losing the evidence of that is worse than
                -- leaving a row behind for somebody to find.
                -- ─────────────────────────────────────────────────────────────────────────────────
                DELETE FROM creator.creator_profiles c
                WHERE c."Id" IN (SELECT phantom_creator_id FROM bg10_plan)
                  AND NOT EXISTS (SELECT 1 FROM identity.users u  WHERE u."Id" = c."UserId")
                  AND NOT EXISTS (SELECT 1 FROM creator.creator_activity_entries          x WHERE x."CreatorId" = c."Id")
                  AND NOT EXISTS (SELECT 1 FROM creator.creator_milestone_progress        x WHERE x."CreatorId" = c."Id")
                  AND NOT EXISTS (SELECT 1 FROM creator.creator_challenge_enrollments     x WHERE x."CreatorId" = c."Id")
                  AND NOT EXISTS (SELECT 1 FROM creator.creator_learning_progress         x WHERE x."CreatorId" = c."Id")
                  AND NOT EXISTS (SELECT 1 FROM creator.creator_certificates              x WHERE x."CreatorId" = c."Id")
                  AND NOT EXISTS (SELECT 1 FROM creator.creator_referral_codes            x WHERE x."CreatorId" = c."Id")
                  AND NOT EXISTS (SELECT 1 FROM creator.creator_reward_redemptions        x WHERE x."CreatorId" = c."Id")
                  AND NOT EXISTS (SELECT 1 FROM creator.creator_signup_bonuses            x WHERE x."CreatorId" = c."Id")
                  AND NOT EXISTS (SELECT 1 FROM creator.creator_verification_applications x WHERE x."CreatorId" = c."Id")
                  AND NOT EXISTS (SELECT 1 FROM creator.grant_applications                x WHERE x."CreatorId" = c."Id");

                -- ─────────────────────────────────────────────────────────────────────────────────
                -- 7. The rung.
                --
                -- A member who published and was never credited reads NotACreator on their own
                -- profile. Now that the entries are theirs, the status the platform would have
                -- served all along is Contributor.
                --
                -- This restates one branch of CreatorStatusPolicy.EarnedRung in SQL, which is a real
                -- duplication and is guarded rather than hidden: every condition below is one of the
                -- guards that branch is reached through, and CreatorStatusAttributionTests fails if
                -- the policy and this predicate ever disagree. Nothing here invents a threshold —
                -- a profile that could reach any higher rung is excluded and left for the platform's
                -- own refresh, which runs on the member's next recognised action.
                -- ─────────────────────────────────────────────────────────────────────────────────
                UPDATE creator.creator_profiles c
                SET "EarnedStatus"    = 'Contributor',
                    "Status"          = 'Contributor',
                    "StatusChangedAt" = now(),
                    "UpdatedAt"       = now()
                WHERE c."Id" IN (SELECT creator_id FROM bg10_touched)
                  AND EXISTS (SELECT 1 FROM identity.users u WHERE u."Id" = c."UserId")
                  AND c."RecognisedActions" >= 1
                  AND c."EarnedStatus" = 'NotACreator'
                  AND c."IsVerifiedCreator"      = FALSE
                  AND c."IsSuspended"            = FALSE
                  AND c."IsRestricted"           = FALSE
                  AND c."HasVerificationInReview" = FALSE
                  AND NOT (c."TotalContentPublished" >= 5 AND c."Followers" >= 25);

                DROP TABLE bg10_plan;
                DROP TABLE bg10_touched;
                """);
        }

        /// <summary>
        /// Deliberately empty, and not an oversight.
        /// <para>
        /// The <c>Up</c> direction discards information: once an entry has been re-keyed from a
        /// seller profile to the member who owns it, nothing records which of the two identifiers it
        /// arrived with, and a phantom creator record that has been deleted cannot be told apart from
        /// one that never existed. A <c>Down</c> that re-derived the old values would have to
        /// reintroduce the defect on the rows it could still identify and would silently miss the
        /// rest, which is a worse state than either side of this migration.
        /// </para>
        /// <para>
        /// Rolling this back means restoring from a backup taken before it ran.
        /// </para>
        /// </summary>
        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
