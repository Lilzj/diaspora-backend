START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025115018_RewardPointsVesting') THEN
    ALTER TABLE financial.financial_reward_entries ADD "VestedAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025115018_RewardPointsVesting') THEN
    ALTER TABLE financial.financial_reward_entries ADD "VestsAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025115018_RewardPointsVesting') THEN
    ALTER TABLE financial.financial_reward_accounts ADD "PendingBalance" bigint NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025115018_RewardPointsVesting') THEN
    CREATE INDEX ix_financial_reward_entries_vesting ON financial.financial_reward_entries ("VestedAt", "VestsAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025115018_RewardPointsVesting') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261025115018_RewardPointsVesting', '10.0.13');
    END IF;
END $EF$;
COMMIT;

