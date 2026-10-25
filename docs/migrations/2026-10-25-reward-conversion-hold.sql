START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025104933_RewardConversionHold') THEN
    ALTER TABLE financial.financial_reward_conversions ADD "AvailableAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025104933_RewardConversionHold') THEN
    ALTER TABLE financial.financial_reward_conversions ADD "ReleasedAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025104933_RewardConversionHold') THEN
    CREATE INDEX ix_financial_reward_conversions_release ON financial.financial_reward_conversions ("ReleasedAt", "AvailableAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025104933_RewardConversionHold') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261025104933_RewardConversionHold', '10.0.13');
    END IF;
END $EF$;
COMMIT;

