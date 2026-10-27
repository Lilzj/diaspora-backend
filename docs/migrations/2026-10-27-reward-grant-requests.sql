START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027220023_RewardGrantRequests') THEN
    ALTER TABLE financial.financial_reward_entries ADD "ApprovedByUserId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027220023_RewardGrantRequests') THEN
    CREATE TABLE financial.financial_reward_grant_requests (
        "Id" uuid NOT NULL,
        "MemberUserId" uuid NOT NULL,
        "Points" bigint NOT NULL,
        "Source" character varying(64) NOT NULL,
        "GrantReference" character varying(256) NOT NULL,
        "Reason" character varying(512) NOT NULL,
        "RequestedByUserId" uuid NOT NULL,
        "RequestedAt" timestamp with time zone NOT NULL,
        "Status" character varying(32) NOT NULL,
        "DecidedByUserId" uuid,
        "DecidedAt" timestamp with time zone,
        "DecisionNote" character varying(512),
        "PolicyVersion" character varying(64),
        "Revision" integer NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "CreatedBy" text,
        "ModifiedAt" timestamp with time zone,
        "ModifiedBy" text,
        CONSTRAINT "PK_financial_reward_grant_requests" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027220023_RewardGrantRequests') THEN
    CREATE UNIQUE INDEX ix_financial_reward_grant_requests_reference ON financial.financial_reward_grant_requests ("GrantReference");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027220023_RewardGrantRequests') THEN
    CREATE INDEX ix_financial_reward_grant_requests_status ON financial.financial_reward_grant_requests ("Status", "RequestedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027220023_RewardGrantRequests') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261027220023_RewardGrantRequests', '10.0.13');
    END IF;
END $EF$;
COMMIT;

