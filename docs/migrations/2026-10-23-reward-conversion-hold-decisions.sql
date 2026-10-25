START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023210127_RewardConversionHoldDecisions') THEN
    CREATE TABLE financial.financial_reward_hold_decisions (
        "DecisionId" uuid NOT NULL,
        "IsHeld" boolean NOT NULL,
        "Reason" character varying(512) NOT NULL,
        "DecidedByUserId" uuid,
        "DecidedAt" timestamp with time zone NOT NULL,
        "RewardAccountId" uuid NOT NULL,
        CONSTRAINT "PK_financial_reward_hold_decisions" PRIMARY KEY ("DecisionId"),
        CONSTRAINT "FK_financial_reward_hold_decisions_financial_reward_accounts_R~" FOREIGN KEY ("RewardAccountId") REFERENCES financial.financial_reward_accounts ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023210127_RewardConversionHoldDecisions') THEN
    CREATE INDEX "IX_financial_reward_hold_decisions_RewardAccountId" ON financial.financial_reward_hold_decisions ("RewardAccountId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261023210127_RewardConversionHoldDecisions') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261023210127_RewardConversionHoldDecisions', '10.0.13');
    END IF;
END $EF$;
COMMIT;

