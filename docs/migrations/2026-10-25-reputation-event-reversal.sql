START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025111958_ReputationEventReversal') THEN
    ALTER TABLE trust.reputation_events ADD "AppliedPoints" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025111958_ReputationEventReversal') THEN
    ALTER TABLE trust.reputation_events ADD "ReversesEventId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025111958_ReputationEventReversal') THEN
    CREATE INDEX ix_reputation_events_reverses ON trust.reputation_events ("ReversesEventId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025111958_ReputationEventReversal') THEN
    CREATE INDEX ix_reputation_events_source ON trust.reputation_events ("SourceType", "SourceId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025111958_ReputationEventReversal') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261025111958_ReputationEventReversal', '10.0.13');
    END IF;
END $EF$;
COMMIT;

