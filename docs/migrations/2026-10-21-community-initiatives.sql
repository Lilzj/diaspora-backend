START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261020162048_CommunityInitiatives') THEN
    CREATE TABLE community_programme.community_initiatives (
        "Id" uuid NOT NULL,
        "CommunityId" uuid NOT NULL,
        "ProposedBy" uuid NOT NULL,
        "Title" character varying(140) NOT NULL,
        "Description" character varying(4000) NOT NULL,
        "TargetDate" date,
        "Status" character varying(32) NOT NULL,
        "StartedAt" timestamp with time zone,
        "ClosedAt" timestamp with time zone,
        "OrganiserIds" uuid[] NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "CreatedBy" text,
        "ModifiedAt" timestamp with time zone,
        "ModifiedBy" text,
        CONSTRAINT "PK_community_initiatives" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_community_initiatives_communities_CommunityId" FOREIGN KEY ("CommunityId") REFERENCES community_programme.communities ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261020162048_CommunityInitiatives') THEN
    CREATE TABLE community_programme.community_initiative_participants (
        "Id" uuid NOT NULL,
        "InitiativeId" uuid NOT NULL,
        "CommunityId" uuid NOT NULL,
        "MemberId" uuid NOT NULL,
        "JoinedAt" timestamp with time zone NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "CreatedBy" text,
        "ModifiedAt" timestamp with time zone,
        "ModifiedBy" text,
        CONSTRAINT "PK_community_initiative_participants" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_community_initiative_participants_community_initiatives_Ini~" FOREIGN KEY ("InitiativeId") REFERENCES community_programme.community_initiatives ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261020162048_CommunityInitiatives') THEN
    CREATE TABLE community_programme.community_initiative_updates (
        "Id" uuid NOT NULL,
        "InitiativeId" uuid NOT NULL,
        "CommunityId" uuid NOT NULL,
        "AuthorId" uuid NOT NULL,
        "PostedAt" timestamp with time zone NOT NULL,
        "Title" character varying(140) NOT NULL,
        "Body" character varying(4000) NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "CreatedBy" text,
        "ModifiedAt" timestamp with time zone,
        "ModifiedBy" text,
        CONSTRAINT "PK_community_initiative_updates" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_community_initiative_updates_community_initiatives_Initiati~" FOREIGN KEY ("InitiativeId") REFERENCES community_programme.community_initiatives ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261020162048_CommunityInitiatives') THEN
    CREATE UNIQUE INDEX ux_community_initiative_participants_initiative_member ON community_programme.community_initiative_participants ("InitiativeId", "MemberId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261020162048_CommunityInitiatives') THEN
    CREATE INDEX ix_community_initiative_updates_initiative_posted ON community_programme.community_initiative_updates ("InitiativeId", "PostedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261020162048_CommunityInitiatives') THEN
    CREATE INDEX ix_community_initiatives_community_created ON community_programme.community_initiatives ("CommunityId", "CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261020162048_CommunityInitiatives') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261020162048_CommunityInitiatives', '10.0.13');
    END IF;
END $EF$;
COMMIT;

