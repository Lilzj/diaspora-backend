START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021024219_AutomationTriggerSources') THEN
    ALTER TABLE community_life.community_life_events ADD "CommunityId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021024219_AutomationTriggerSources') THEN
    CREATE TABLE location.member_presence (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "IsEnabled" boolean NOT NULL,
        "EnabledAt" timestamp with time zone,
        "LastAreaId" uuid,
        "LastEvaluatedAt" timestamp with time zone,
        "ModifiedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_member_presence" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021024219_AutomationTriggerSources') THEN
    CREATE INDEX ix_community_life_events_community_status ON community_life.community_life_events ("CommunityId", "Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021024219_AutomationTriggerSources') THEN
    CREATE UNIQUE INDEX ix_member_presence_user ON location.member_presence ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261021024219_AutomationTriggerSources') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261021024219_AutomationTriggerSources', '10.0.13');
    END IF;
END $EF$;
COMMIT;

