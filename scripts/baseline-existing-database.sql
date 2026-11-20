-- Move an existing Diaspora database onto the squashed migration history.
--
-- WHY THIS EXISTS
--
-- The 92 migrations that built this schema between 26 Jul and 10 Nov 2026 were replaced by one
-- baseline, 20261120140702_PlatformBaseline, which creates the same schema in a single step. A
-- database that already HAS that schema must not run the baseline -- every CREATE TABLE in it
-- would fail. Instead its migration history is rewritten to say the baseline is already applied,
-- which is what "baselining" means. EF then has nothing to do, and the next real migration
-- applies on top as usual.
--
-- An empty database needs none of this: `dotnet ef database update` applies the baseline and is
-- done. This script is only for databases built by the old chain -- production, and any long-lived
-- staging or developer database that was never recreated.
--
-- WHEN TO RUN IT
--
-- After the squash is merged and BEFORE the next deploy that runs migrations. A deploy against an
-- un-baselined database will try to create tables that exist and will fail; it will not damage
-- anything, but it will not start either.
--
-- HOW TO RUN IT
--
--     psql "<connection string>" -f scripts/baseline-existing-database.sql
--
-- It is safe to run twice: a database already carrying the baseline and nothing else is left
-- alone. It refuses outright if the history is not exactly the 92 rows it expects, so a database
-- that is half-migrated, or ahead of this change, stops rather than being rewritten.
--
-- The five raw-SQL statements in the old chain were all backfills of existing rows -- repairing
-- creator attribution, projecting community members, setting milestone floors, and placing
-- housing rows at a point. The baseline does not carry them, because every one is a no-op on an
-- empty database and every database with rows has already run them. That is precisely the
-- assertion this script makes before it rewrites anything.

BEGIN;

DO $baseline$
DECLARE
    baseline     CONSTANT text := '20261120140702_PlatformBaseline';
    expected_head CONSTANT text := '20261110164005_StepUpGrants';
    expected_rows CONSTANT int  := 92;
    applied      int;
BEGIN
    IF to_regclass('public.__EFMigrationsHistory') IS NULL THEN
        RAISE EXCEPTION
            'No __EFMigrationsHistory table: this database was never migrated. Run `dotnet ef database update` instead.';
    END IF;

    SELECT count(*) INTO applied FROM public."__EFMigrationsHistory";

    -- Already done. Saying so and stopping makes a second run harmless.
    IF applied = 1 AND EXISTS (
        SELECT 1 FROM public."__EFMigrationsHistory" WHERE "MigrationId" = baseline)
    THEN
        RAISE NOTICE 'Already baselined on %. Nothing to do.', baseline;
        RETURN;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM public."__EFMigrationsHistory" WHERE "MigrationId" = expected_head)
    THEN
        RAISE EXCEPTION
            'This database is not at %. Its newest migration is %. Baselining it would claim a schema it does not have.',
            expected_head,
            (SELECT max("MigrationId") FROM public."__EFMigrationsHistory");
    END IF;

    IF applied <> expected_rows THEN
        RAISE EXCEPTION
            'Expected exactly % applied migrations, found %. This database did not come from the chain this baseline replaces.',
            expected_rows, applied;
    END IF;

    DELETE FROM public."__EFMigrationsHistory";

    INSERT INTO public."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES (baseline, '10.0.13');

    RAISE NOTICE 'Baselined: % rows replaced by %.', expected_rows, baseline;
END
$baseline$;

COMMIT;
