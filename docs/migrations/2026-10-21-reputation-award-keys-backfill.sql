-- ════════════════════════════════════════════════════════════════════════════════════════════════
-- Reputation award keys — historical backfill (CS-1b, docs/rewards/stage-4-integrity-design.md)
--
-- Run AFTER 2026-10-21-reputation-award-keys.sql. Idempotent: safe to run more than once.
-- Changes no score, no points and no counter; deletes nothing. It only labels existing rows.
--
-- Why: rows written before awards were keyed have AwardKey NULL, so the unique index cannot see
-- them. Without this, a guide verified before the deploy could be paid GuideApproved once more if
-- its verification flickers, and an old verification re-applied could pay again.
--
-- Each key must be byte-for-byte what ReputationAwardKey produces in C#:
--   {ActionType}:{kind}:{Guid.ToString("N")}   — lowercase hex, no hyphens.
-- SourceId was stored as Guid.ToString() (lowercase, hyphenated), hence lower(replace(...)).
--
-- Only the EARLIEST row per (profile, action, source) is labelled. On 2026-10-21 production held no
-- duplicates at all (measured on a Neon branch), so this is belt and braces; if any had existed,
-- the later rows would stay NULL and remain visible as history.
--
-- Not backfillable, and why:
--   HelpfulVoteReceived  — the key needs the voter; the row recorded only the guide.
--   GuideUpdated         — keyed on the integration event id, never stored.
--   ViolationRecorded / ContentRemoved — keyed on the moderation item id; the row stored the content id.
--   CommunityConfirmation — already deduplicated by community_contributions' unique index.
-- ════════════════════════════════════════════════════════════════════════════════════════════════

START TRANSACTION;

WITH candidates AS (
    SELECT DISTINCT ON (e."ProfileId", e."ActionType", lower(e."SourceId"))
           e."Id",
           e."ProfileId",
           e."ActionType" || ':' ||
               CASE e."ActionType"
                   WHEN 'GuidePublished'  THEN 'guide'
                   WHEN 'GuideApproved'   THEN 'guide'
                   WHEN 'UpdatePublished' THEN 'post'
                   WHEN 'CommentWritten'  THEN 'comment'
                   ELSE 'verification'
               END || ':' || replace(lower(e."SourceId"), '-', '') AS award_key
    FROM trust.reputation_events e
    WHERE e."AwardKey" IS NULL
      AND e."SourceId" ~* '^[0-9a-f]{8}-?[0-9a-f]{4}-?[0-9a-f]{4}-?[0-9a-f]{4}-?[0-9a-f]{12}$'
      AND (
            (e."ActionType" = 'GuidePublished'  AND e."SourceType" = 'guide')
         OR (e."ActionType" = 'GuideApproved'   AND e."SourceType" = 'guide')
         OR (e."ActionType" = 'UpdatePublished' AND e."SourceType" = 'post')
         OR (e."ActionType" = 'CommentWritten'  AND e."SourceType" = 'comment')
         OR (e."ActionType" IN ('PhoneVerified', 'EmailVerified', 'CountryVerified',
                                'GovernmentIdVerified', 'ProviderVerified')
             AND e."SourceType" = 'verification')
      )
    ORDER BY e."ProfileId", e."ActionType", lower(e."SourceId"), e."CreatedAt", e."Id"
)
UPDATE trust.reputation_events target
SET "AwardKey" = c.award_key
FROM candidates c
WHERE target."Id" = c."Id"
  -- A row written by the new code after deploy may already carry this key.
  AND NOT EXISTS (
        SELECT 1 FROM trust.reputation_events keyed
        WHERE keyed."ProfileId" = c."ProfileId" AND keyed."AwardKey" = c.award_key);

COMMIT;
