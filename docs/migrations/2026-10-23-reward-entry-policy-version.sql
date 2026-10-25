START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023213650_RewardEntryPolicyVersion') THEN
    ALTER TABLE financial.financial_reward_entries ADD "PolicyVersion" character varying(64);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023213650_RewardEntryPolicyVersion') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261023213650_RewardEntryPolicyVersion', '10.0.13');
    END IF;
END $EF$;
COMMIT;

