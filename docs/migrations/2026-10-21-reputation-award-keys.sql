START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021172149_ReputationAwardKeys') THEN
    ALTER TABLE trust.reputation_events ADD "AwardKey" character varying(200);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021172149_ReputationAwardKeys') THEN
    CREATE UNIQUE INDEX ix_reputation_events_profile_award_key ON trust.reputation_events ("ProfileId", "AwardKey");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021172149_ReputationAwardKeys') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261021172149_ReputationAwardKeys', '10.0.13');
    END IF;
END $EF$;
COMMIT;

