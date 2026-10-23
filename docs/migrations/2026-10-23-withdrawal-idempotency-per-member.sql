START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023192414_WithdrawalIdempotencyPerMember') THEN
    DROP INDEX financial.ix_financial_withdrawals_idempotency;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023192414_WithdrawalIdempotencyPerMember') THEN
    CREATE UNIQUE INDEX ix_financial_withdrawals_requester_idempotency ON financial.financial_withdrawals ("RequestedByUserId", "IdempotencyKey");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023192414_WithdrawalIdempotencyPerMember') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261023192414_WithdrawalIdempotencyPerMember', '10.0.13');
    END IF;
END $EF$;
COMMIT;

