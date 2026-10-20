START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261020073140_CommunityGovernanceAndPreferences') THEN
    CREATE TABLE community_programme.community_member_preferences (
        "Id" uuid NOT NULL,
        "MemberId" uuid NOT NULL,
        "CrossCommunityDiscoveryEnabled" boolean NOT NULL,
        "ShowPrimaryCommunityOnProfile" boolean NOT NULL,
        "ShowInterestedCommunitiesOnProfile" boolean NOT NULL,
        "ShowCommunityRolesOnProfile" boolean NOT NULL,
        "DefaultMembershipVisibility" character varying(32) NOT NULL,
        "PreferredContentLanguage" character varying(35),
        "NotificationsJson" text NOT NULL,
        "Revision" integer NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "CreatedBy" text,
        "ModifiedAt" timestamp with time zone,
        "ModifiedBy" text,
        CONSTRAINT "PK_community_member_preferences" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261020073140_CommunityGovernanceAndPreferences') THEN
    CREATE TABLE community_programme.community_notices (
        "Id" uuid NOT NULL,
        "CommunityId" uuid NOT NULL,
        "PublishedBy" uuid NOT NULL,
        "PublishedByRole" character varying(32) NOT NULL,
        "PublishedAt" timestamp with time zone NOT NULL,
        "Title" character varying(140) NOT NULL,
        "Body" character varying(4000) NOT NULL,
        "ExpiresAt" timestamp with time zone,
        "IsPinned" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "CreatedBy" text,
        "ModifiedAt" timestamp with time zone,
        "ModifiedBy" text,
        CONSTRAINT "PK_community_notices" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_community_notices_communities_CommunityId" FOREIGN KEY ("CommunityId") REFERENCES community_programme.communities ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261020073140_CommunityGovernanceAndPreferences') THEN
    CREATE TABLE community_programme.community_rules (
        "Id" uuid NOT NULL,
        "CommunityId" uuid NOT NULL,
        "Position" integer NOT NULL,
        "Title" character varying(120) NOT NULL,
        "Body" character varying(2000) NOT NULL,
        "Consequence" character varying(500),
        "CreatedAt" timestamp with time zone NOT NULL,
        "CreatedBy" text,
        "ModifiedAt" timestamp with time zone,
        "ModifiedBy" text,
        CONSTRAINT "PK_community_rules" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_community_rules_communities_CommunityId" FOREIGN KEY ("CommunityId") REFERENCES community_programme.communities ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261020073140_CommunityGovernanceAndPreferences') THEN
    CREATE UNIQUE INDEX ux_community_member_preferences_member ON community_programme.community_member_preferences ("MemberId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261020073140_CommunityGovernanceAndPreferences') THEN
    CREATE INDEX ix_community_notices_community_published ON community_programme.community_notices ("CommunityId", "PublishedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261020073140_CommunityGovernanceAndPreferences') THEN
    CREATE INDEX ix_community_rules_community_position ON community_programme.community_rules ("CommunityId", "Position");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261020073140_CommunityGovernanceAndPreferences') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261020073140_CommunityGovernanceAndPreferences', '10.0.13');
    END IF;
END $EF$;
COMMIT;

