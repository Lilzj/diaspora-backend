START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261022120007_RewardConversionIdempotencyPerMember') THEN
    DROP INDEX financial.ix_financial_reward_conversions_idempotency;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261022120007_RewardConversionIdempotencyPerMember') THEN
    CREATE UNIQUE INDEX ix_financial_reward_conversions_member_idempotency ON financial.financial_reward_conversions ("MemberUserId", "IdempotencyKey");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261022120007_RewardConversionIdempotencyPerMember') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261022120007_RewardConversionIdempotencyPerMember', '10.0.13');
    END IF;
END $EF$;
COMMIT;

