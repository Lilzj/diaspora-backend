START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021181732_ReputationProfileRevision') THEN
    ALTER TABLE trust.reputation_profiles ADD "Revision" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021181732_ReputationProfileRevision') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261021181732_ReputationProfileRevision', '10.0.13');
    END IF;
END $EF$;
COMMIT;

