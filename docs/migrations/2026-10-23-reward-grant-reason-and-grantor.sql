START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023203426_RewardGrantReasonAndGrantor') THEN
    ALTER TABLE financial.financial_reward_entries ADD "GrantedByUserId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023203426_RewardGrantReasonAndGrantor') THEN
    ALTER TABLE financial.financial_reward_entries ADD "Reason" character varying(512);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023203426_RewardGrantReasonAndGrantor') THEN
    CREATE INDEX ix_financial_reward_entries_grantor ON financial.financial_reward_entries ("GrantedByUserId", "OccurredAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023203426_RewardGrantReasonAndGrantor') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261023203426_RewardGrantReasonAndGrantor', '10.0.13');
    END IF;
END $EF$;
COMMIT;

