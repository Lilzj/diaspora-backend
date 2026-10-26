START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261026160031_CreatorGrantPolicyRevisions') THEN
    ALTER TABLE creator.creator_milestones ADD "PolicyRevision" integer NOT NULL DEFAULT 1;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261026160031_CreatorGrantPolicyRevisions') THEN
    ALTER TABLE creator.creator_challenges ADD "PolicyRevision" integer NOT NULL DEFAULT 1;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261026160031_CreatorGrantPolicyRevisions') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261026160031_CreatorGrantPolicyRevisions', '10.0.13');
    END IF;
END $EF$;
COMMIT;

