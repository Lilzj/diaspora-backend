START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025234536_SharedDeviceSignal') THEN
    ALTER TABLE creator.creator_profiles ADD "SharedDeviceFlaggedAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025234536_SharedDeviceSignal') THEN
    CREATE INDEX ix_user_devices_identifier ON identity.user_devices ("DeviceIdentifier");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025234536_SharedDeviceSignal') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261025234536_SharedDeviceSignal', '10.0.13');
    END IF;
END $EF$;
COMMIT;

