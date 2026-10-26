START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025205817_ReputationRuleSetVersion') THEN
    ALTER TABLE trust.reputation_events ADD "RuleSetVersion" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025205817_ReputationRuleSetVersion') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261025205817_ReputationRuleSetVersion', '10.0.13');
    END IF;
END $EF$;
COMMIT;

