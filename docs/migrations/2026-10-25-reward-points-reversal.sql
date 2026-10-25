START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025122309_RewardPointsReversal') THEN
    ALTER TABLE financial.financial_reward_entries ADD "IsReversal" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025122309_RewardPointsReversal') THEN
    ALTER TABLE financial.financial_reward_entries ADD "ReversalDeclinedAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025122309_RewardPointsReversal') THEN
    ALTER TABLE financial.financial_reward_entries ADD "ReversalReason" character varying(512);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025122309_RewardPointsReversal') THEN
    ALTER TABLE financial.financial_reward_entries ADD "ReversedAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025122309_RewardPointsReversal') THEN
    ALTER TABLE financial.financial_reward_accounts ADD "LifetimeReversed" bigint NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025122309_RewardPointsReversal') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261025122309_RewardPointsReversal', '10.0.13');
    END IF;
END $EF$;
COMMIT;

