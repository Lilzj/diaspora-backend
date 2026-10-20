START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261020030617_CreatorRecommendationDismissals') THEN
    CREATE TABLE creator.creator_recommendation_dismissals (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "RecommendationId" character varying(128) NOT NULL,
        "DismissedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_creator_recommendation_dismissals" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261020030617_CreatorRecommendationDismissals') THEN
    CREATE UNIQUE INDEX ux_creator_recommendation_dismissal ON creator.creator_recommendation_dismissals ("UserId", "RecommendationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261020030617_CreatorRecommendationDismissals') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261020030617_CreatorRecommendationDismissals', '10.0.13');
    END IF;
END $EF$;
COMMIT;

