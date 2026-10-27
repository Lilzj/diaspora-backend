START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027124030_TrustRiskSignals') THEN
    CREATE TABLE trust.risk_signals (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "Kind" character varying(32) NOT NULL,
        "SourceKey" character varying(128) NOT NULL,
        "VerificationType" character varying(32),
        "ContentKey" character varying(200),
        "OccurredAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_risk_signals" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027124030_TrustRiskSignals') THEN
    CREATE INDEX ix_risk_signals_occurred ON trust.risk_signals ("OccurredAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027124030_TrustRiskSignals') THEN
    CREATE UNIQUE INDEX ix_risk_signals_source ON trust.risk_signals ("SourceKey");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027124030_TrustRiskSignals') THEN
    CREATE INDEX ix_risk_signals_user_occurred ON trust.risk_signals ("UserId", "OccurredAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027124030_TrustRiskSignals') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261027124030_TrustRiskSignals', '10.0.13');
    END IF;
END $EF$;
COMMIT;

