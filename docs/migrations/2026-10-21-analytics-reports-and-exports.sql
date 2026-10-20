START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261020070902_AnalyticsReportsAndExports') THEN
    CREATE TABLE analytics.analytics_exports (
        "Id" uuid NOT NULL,
        "OwnerUserId" uuid NOT NULL,
        "Format" character varying(64) NOT NULL,
        "DefinitionJson" character varying(8000) NOT NULL,
        "Status" character varying(32) NOT NULL,
        "RequestedAt" timestamp with time zone NOT NULL,
        "CompletedAt" timestamp with time zone,
        "ExpiresAt" timestamp with time zone,
        "FileText" text,
        "SizeBytes" bigint,
        "DownloadToken" character varying(64),
        "FailureReason" character varying(500),
        "Revision" integer NOT NULL,
        CONSTRAINT "PK_analytics_exports" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261020070902_AnalyticsReportsAndExports') THEN
    CREATE TABLE analytics.analytics_saved_reports (
        "Id" uuid NOT NULL,
        "OwnerUserId" uuid NOT NULL,
        "Name" character varying(120) NOT NULL,
        "Description" character varying(500),
        "DefinitionJson" character varying(8000) NOT NULL,
        "Visualization" character varying(32) NOT NULL,
        "Sharing" character varying(32) NOT NULL,
        "IsFavorite" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        "Revision" integer NOT NULL,
        CONSTRAINT "PK_analytics_saved_reports" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261020070902_AnalyticsReportsAndExports') THEN
    CREATE INDEX ix_analytics_exports_owner ON analytics.analytics_exports ("OwnerUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261020070902_AnalyticsReportsAndExports') THEN
    CREATE INDEX ix_analytics_saved_reports_owner ON analytics.analytics_saved_reports ("OwnerUserId", "UpdatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261020070902_AnalyticsReportsAndExports') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261020070902_AnalyticsReportsAndExports', '10.0.13');
    END IF;
END $EF$;
COMMIT;

