START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014185244_TownNamesReplaceCatalogueAreas') THEN
    ALTER TABLE providers.provider_service_areas ADD "CityName" character varying(200);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014185244_TownNamesReplaceCatalogueAreas') THEN
    ALTER TABLE financial.financial_ad_campaigns ADD "AudienceTownNames" text[] NOT NULL DEFAULT ARRAY[]::text[];
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014185244_TownNamesReplaceCatalogueAreas') THEN
    ALTER TABLE community_programme.community_polls ADD "CityName" character varying(160);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014185244_TownNamesReplaceCatalogueAreas') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261014185244_TownNamesReplaceCatalogueAreas', '10.0.10');
    END IF;
END $EF$;
COMMIT;

