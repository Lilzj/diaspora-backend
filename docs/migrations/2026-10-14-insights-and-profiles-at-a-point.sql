START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014182119_InsightsAndProfilesAtAPoint') THEN
    ALTER TABLE housing.housing_neighborhoods ADD latitude double precision;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014182119_InsightsAndProfilesAtAPoint') THEN
    ALTER TABLE housing.housing_neighborhoods ADD longitude double precision;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014182119_InsightsAndProfilesAtAPoint') THEN
    ALTER TABLE housing.housing_neighborhood_insights ALTER COLUMN "ProfileId" DROP NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014182119_InsightsAndProfilesAtAPoint') THEN
    ALTER TABLE housing.housing_neighborhood_insights ALTER COLUMN "AreaId" DROP NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014182119_InsightsAndProfilesAtAPoint') THEN
    ALTER TABLE housing.housing_neighborhood_insights ADD country_code character varying(2);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014182119_InsightsAndProfilesAtAPoint') THEN
    ALTER TABLE housing.housing_neighborhood_insights ADD latitude double precision;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014182119_InsightsAndProfilesAtAPoint') THEN
    ALTER TABLE housing.housing_neighborhood_insights ADD longitude double precision;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014182119_InsightsAndProfilesAtAPoint') THEN
    ALTER TABLE housing.housing_neighborhood_insights ADD place_id character varying(256);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014182119_InsightsAndProfilesAtAPoint') THEN
    ALTER TABLE housing.housing_neighborhood_insights ADD place_name character varying(200);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014182119_InsightsAndProfilesAtAPoint') THEN
    UPDATE housing.housing_neighborhoods AS profile
    SET latitude = area.latitude,
        longitude = area.longitude
    FROM location.administrative_areas AS area
    WHERE area."Id" = profile."AreaId"
      AND profile.latitude IS NULL
      AND area.latitude IS NOT NULL
      AND area.longitude IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014182119_InsightsAndProfilesAtAPoint') THEN
    UPDATE housing.housing_neighborhood_insights AS insight
    SET latitude = area.latitude,
        longitude = area.longitude
    FROM location.administrative_areas AS area
    WHERE area."Id" = insight."AreaId"
      AND insight.latitude IS NULL
      AND area.latitude IS NOT NULL
      AND area.longitude IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014182119_InsightsAndProfilesAtAPoint') THEN
    CREATE INDEX ix_housing_neighborhoods_point ON housing.housing_neighborhoods (latitude, longitude);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014182119_InsightsAndProfilesAtAPoint') THEN
    CREATE INDEX ix_housing_insights_point ON housing.housing_neighborhood_insights (latitude, longitude);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261014182119_InsightsAndProfilesAtAPoint') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261014182119_InsightsAndProfilesAtAPoint', '10.0.10');
    END IF;
END $EF$;
COMMIT;

