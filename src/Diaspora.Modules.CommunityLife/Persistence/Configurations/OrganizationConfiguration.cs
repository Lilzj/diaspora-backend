using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Organizations;
using Diaspora.Modules.CommunityLife.Domain.Policies;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.CommunityLife.Persistence.Configurations;

/// <summary>
/// Maps the organization aggregate and the seven tables it owns.
/// <para>
/// <b>Three indexes are load-bearing.</b> The neighbourhood index makes "every organization in
/// Peckham" a seek rather than a scan; the area-path index is its subtree equivalent, over B4's
/// materialised path stored as text precisely so "everything under London" is an indexed
/// <c>LIKE 'prefix%'</c>; and the discovery index carries the public browse predicate in the order
/// it narrows.
/// </para>
/// <para>
/// <b>Owned-collection indexes must be declared inside the <c>OwnsMany</c> block.</b> Declaring
/// them on the outer builder silently targets the parent table and produces an index over a column
/// that is not there — the trap B6 records and every module since has had to remember.
/// </para>
/// <para>
/// Nothing here declares the soft-delete filter or the delete cascade for owned rows. The shared
/// context applies the global query filter and <c>SoftDeleteConverter</c> handles owned rows —
/// B1.0.1 exists so a module never has to.
/// </para>
/// </summary>
public sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            CommunityLifePersistenceConstants.OrganizationsTable,
            CommunityLifeModuleMetadata.Schema);

        builder.HasKey(organization => organization.Id);

        // The optimistic concurrency token, declared rather than merely bumped in the aggregate.
        // EnsureRevision rejects a stale editor that sends back the revision it loaded; nothing
        // stops the editor that does not send one, and two saves from one committee's two desks
        // would then both commit with the later silently winning. B11 shipped without this until an
        // integration test raced two contexts and found no exception at all.
        builder.Property(organization => organization.Revision).IsConcurrencyToken();

        builder.Property(organization => organization.Id)
            .HasConversion(CommunityLifeValueConverters.Id<OrganizationId>());

        builder.Property(organization => organization.CreatedByUserId)
            .HasConversion(CommunityLifeValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(organization => organization.OrganizationTypeKey)
            .HasMaxLength(CommunityLifeLimits.MaxCategoryKey)
            .IsRequired();

        builder.Property(organization => organization.Mission)
            .HasMaxLength(CommunityLifeLimits.MaxMission);

        builder.Property(organization => organization.Vision)
            .HasMaxLength(CommunityLifeLimits.MaxVision);

        ConfigureEnums(builder);
        ConfigureOwnedValues(builder);
        ConfigureOwnedCollections(builder);
        ConfigureIndexes(builder);

        builder.Ignore(organization => organization.DomainEvents);
        builder.Ignore(organization => organization.CoverImage);
        builder.Ignore(organization => organization.Logo);
        builder.Ignore(organization => organization.IsDiscoverable);
        builder.Ignore(organization => organization.AcceptsMembership);
        builder.Ignore(organization => organization.AcceptsEvents);
        builder.Ignore(organization => organization.IsPublishable);
        builder.Ignore(organization => organization.Completeness);
    }

    private static void ConfigureEnums(EntityTypeBuilder<Organization> builder)
    {
        builder.Property(organization => organization.Status)
            .HasConversion<string>()
            .HasMaxLength(CommunityLifePersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(organization => organization.Visibility)
            .HasConversion<string>()
            .HasMaxLength(CommunityLifePersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(organization => organization.ModerationState)
            .HasConversion<string>()
            .HasMaxLength(CommunityLifePersistenceConstants.EnumLength)
            .IsRequired();
    }

    private static void ConfigureOwnedValues(EntityTypeBuilder<Organization> builder)
    {
        builder.OwnsOne(organization => organization.Name, name =>
        {
            name.Property(value => value.Value)
                .HasColumnName("name")
                .HasMaxLength(CommunityLifeLimits.MaxTitle)
                .IsRequired();

            name.Property(value => value.ShortName)
                .HasColumnName("short_name")
                .HasMaxLength(CommunityLifeLimits.MaxDisplayName);

            name.Ignore(value => value.DisplayName);

            name.HasIndex(value => value.Value)
                .HasDatabaseName("ix_community_life_organizations_name");
        });

        builder.Navigation(organization => organization.Name).IsRequired();

        builder.OwnsOne(organization => organization.Slug, slug =>
        {
            slug.Property(value => value.Value)
                .HasColumnName("slug")
                .HasMaxLength(CommunityLifeLimits.MaxSlug)
                .IsRequired();

            // Unique across deleted rows too: a deleted organization keeps its slug, because the
            // links shared to it are pinned in family group chats years old.
            slug.HasIndex(value => value.Value)
                .IsUnique()
                .HasDatabaseName("ix_community_life_organizations_slug");
        });

        builder.Navigation(organization => organization.Slug).IsRequired();

        builder.OwnsOne(organization => organization.Description, description =>
        {
            description.Property(value => value.Summary)
                .HasColumnName("summary")
                .HasMaxLength(CommunityLifeLimits.MaxSummary)
                .IsRequired();

            description.Property(value => value.Body)
                .HasColumnName("description")
                .HasMaxLength(CommunityLifeLimits.MaxDescription);
        });

        builder.Navigation(organization => organization.Description).IsRequired();

        builder.OwnsOne(organization => organization.Location, location =>
        {
            location.Property(value => value.CountryCode)
                .HasColumnName("country_code")
                .HasMaxLength(CommunityLifePersistenceConstants.CountryCodeLength)
                .IsRequired();

            location.Property(value => value.RegionName)
                .HasColumnName("region_name")
                .HasMaxLength(CommunityLifeLimits.MaxLocationName);

            location.Property(value => value.CityName)
                .HasColumnName("city_name")
                .HasMaxLength(CommunityLifeLimits.MaxLocationName);

            location.Property(value => value.NeighborhoodName)
                .HasColumnName("neighborhood_name")
                .HasMaxLength(CommunityLifeLimits.MaxLocationName);

            location.Property(value => value.NeighborhoodAreaId)
                .HasColumnName("neighborhood_area_id");

            location.Property(value => value.AreaPath)
                .HasColumnName("area_path")
                .HasMaxLength(CommunityLifeLimits.MaxAreaPath);

            location.Ignore(value => value.DisplayName);
            location.Ignore(value => value.HasNeighborhood);

            // The module's most-run organization query: every body in one neighbourhood.
            location.HasIndex(value => value.NeighborhoodAreaId)
                .HasDatabaseName("ix_community_life_organizations_neighborhood");

            // Its subtree equivalent, over B4's materialised path.
            location.HasIndex(value => value.AreaPath)
                .HasDatabaseName("ix_community_life_organizations_area_path");

            location.HasIndex(value => new { value.CountryCode, value.CityName })
                .HasDatabaseName("ix_community_life_organizations_country_city");
        });

        builder.Navigation(organization => organization.Location).IsRequired();

        builder.OwnsOne(organization => organization.Contact, contact =>
        {
            contact.Property(value => value.Phone)
                .HasColumnName("contact_phone")
                .HasMaxLength(CommunityLifeLimits.MaxDisplayName);

            contact.Property(value => value.WhatsApp)
                .HasColumnName("contact_whatsapp")
                .HasMaxLength(CommunityLifeLimits.MaxDisplayName);

            contact.Property(value => value.Email)
                .HasColumnName("contact_email")
                .HasMaxLength(CommunityLifeLimits.MaxDisplayName);

            contact.Property(value => value.Website)
                .HasColumnName("contact_website")
                .HasMaxLength(CommunityLifeLimits.MaxUrl);

            contact.Property(value => value.ContactName)
                .HasColumnName("contact_name")
                .HasMaxLength(CommunityLifeLimits.MaxDisplayName);

            contact.Ignore(value => value.IsEmpty);
        });

        builder.Navigation(organization => organization.Contact).IsRequired();

        builder.OwnsOne(organization => organization.SocialLinks, links =>
        {
            links.Property(value => value.Facebook)
                .HasColumnName("social_facebook")
                .HasMaxLength(CommunityLifeLimits.MaxUrl);

            links.Property(value => value.Instagram)
                .HasColumnName("social_instagram")
                .HasMaxLength(CommunityLifeLimits.MaxUrl);

            links.Property(value => value.Twitter)
                .HasColumnName("social_twitter")
                .HasMaxLength(CommunityLifeLimits.MaxUrl);

            links.Property(value => value.YouTube)
                .HasColumnName("social_youtube")
                .HasMaxLength(CommunityLifeLimits.MaxUrl);

            links.Property(value => value.LinkedIn)
                .HasColumnName("social_linkedin")
                .HasMaxLength(CommunityLifeLimits.MaxUrl);

            links.Property(value => value.TikTok)
                .HasColumnName("social_tiktok")
                .HasMaxLength(CommunityLifeLimits.MaxUrl);

            links.Ignore(value => value.IsEmpty);
        });

        builder.Navigation(organization => organization.SocialLinks).IsRequired();

        builder.OwnsOne(organization => organization.MembershipPolicy, policy =>
        {
            policy.Property(value => value.Kind)
                .HasColumnName("membership_policy")
                .HasConversion<string>()
                .HasMaxLength(CommunityLifePersistenceConstants.EnumLength)
                .IsRequired();

            policy.Property(value => value.Requirements)
                .HasColumnName("membership_requirements")
                .HasMaxLength(CommunityLifeLimits.MaxMembershipRequirements);

            policy.Property(value => value.RequiresApproval)
                .HasColumnName("membership_requires_approval");

            policy.Property(value => value.RenewalMonths)
                .HasColumnName("membership_renewal_months");

            policy.Property(value => value.IsMembershipPublic)
                .HasColumnName("membership_list_public");

            policy.Ignore(value => value.AcceptsSelfService);
        });

        builder.Navigation(organization => organization.MembershipPolicy).IsRequired();

        builder.OwnsOne(organization => organization.TrustMark, mark =>
        {
            mark.Property(value => value.State)
                .HasColumnName("verification_state")
                .HasConversion<string>()
                .HasMaxLength(CommunityLifePersistenceConstants.EnumLength)
                .IsRequired();

            mark.Property(value => value.VerificationId).HasColumnName("verification_id");

            mark.Property(value => value.Badge)
                .HasColumnName("verification_badge")
                .HasMaxLength(CommunityLifeLimits.MaxDisplayName);

            mark.Property(value => value.DecidedAt).HasColumnName("verification_decided_at");

            mark.Property(value => value.Reason)
                .HasColumnName("verification_reason")
                .HasMaxLength(CommunityLifeLimits.MaxNoteLength);

            mark.Ignore(value => value.IsVerified);
            mark.Ignore(value => value.IsPending);

            // The correlation key a Trust outcome arrives against.
            mark.HasIndex(value => value.VerificationId)
                .HasDatabaseName("ix_community_life_organizations_verification");
        });

        builder.Navigation(organization => organization.TrustMark).IsRequired();

        builder.OwnsOne(organization => organization.Statistics, statistics =>
        {
            statistics.Property(value => value.MemberCount).HasColumnName("member_count");

            statistics.Property(value => value.PendingRequestCount)
                .HasColumnName("pending_request_count");

            statistics.Property(value => value.AdministratorCount)
                .HasColumnName("administrator_count");

            statistics.Property(value => value.VolunteerCount).HasColumnName("volunteer_count");

            statistics.Property(value => value.PublishedEventCount)
                .HasColumnName("published_event_count");

            statistics.Property(value => value.CompletedEventCount)
                .HasColumnName("completed_event_count");

            // The directory's default sort. Without it, ordering ten thousand organizations by
            // size is a full scan on the busiest read in the module.
            statistics.HasIndex(value => value.MemberCount)
                .HasDatabaseName("ix_community_life_organizations_member_count");
        });

        builder.Navigation(organization => organization.Statistics).IsRequired();
    }

    private static void ConfigureOwnedCollections(EntityTypeBuilder<Organization> builder)
    {
        builder.OwnsMany(organization => organization.Leaders, leader =>
        {
            leader.ToTable(
                CommunityLifePersistenceConstants.OrganizationLeadersTable,
                CommunityLifeModuleMetadata.Schema);

            leader.HasKey(value => value.Id);

            // Caller-supplied key. Without this EF marks the Guid store-generated by convention,
            // reads a set key as "this row already exists", and emits an UPDATE where an INSERT was
            // meant — which surfaces as a bogus concurrency conflict rather than a missing row.
            leader.Property(value => value.Id).ValueGeneratedNever();

            leader.Property(value => value.Name)
                .HasMaxLength(CommunityLifeLimits.MaxDisplayName)
                .IsRequired();

            leader.Property(value => value.Role)
                .HasMaxLength(CommunityLifeLimits.MaxDisplayName);

            leader.Ignore(value => value.DomainEvents);
        });

        builder.OwnsMany(organization => organization.OperatingHours, hour =>
        {
            hour.ToTable(
                CommunityLifePersistenceConstants.OrganizationHoursTable,
                CommunityLifeModuleMetadata.Schema);

            hour.HasKey(value => value.Id);
            hour.Property(value => value.Id).ValueGeneratedNever();

            hour.Property(value => value.Day)
                .HasConversion<string>()
                .HasMaxLength(CommunityLifePersistenceConstants.EnumLength)
                .IsRequired();

            hour.Property(value => value.Note)
                .HasMaxLength(CommunityLifeLimits.MaxDisplayName);

            hour.Ignore(value => value.DomainEvents);
        });

        builder.OwnsMany(organization => organization.Languages, language =>
        {
            language.ToTable(
                CommunityLifePersistenceConstants.OrganizationLanguagesTable,
                CommunityLifeModuleMetadata.Schema);

            // The language name is the identity SetLanguages treats as unique — it groups by it —
            // so it is the key here too, and a duplicate becomes impossible in the database rather
            // than merely unlikely in code.
            //
            // Declaring a key at all is load-bearing. Left to convention EF invents a shadow
            // integer, marks it store-generated, and the composite primary key it lands in cannot
            // be an identity column: PostgreSQL tolerates it, SQLite refuses the insert outright,
            // and the failure surfaces as "NOT NULL constraint failed" the first time a row carries
            // one. B11 paid for this on four collections.
            language.WithOwner().HasForeignKey("OrganizationId");
            language.HasKey("OrganizationId", nameof(OrganizationLanguage.Name));

            language.Property(value => value.Name)
                .HasMaxLength(CommunityLifeLimits.MaxLanguageName)
                .IsRequired();

            // Declared inside the OwnsMany block: on the outer builder this would target the
            // organizations table and index a column that is not there. "Which churches here hold a
            // service in Twi" is the query it answers.
            language.HasIndex(value => value.Name)
                .HasDatabaseName("ix_community_life_organization_languages_name");
        });

        builder.OwnsMany(organization => organization.Services, service =>
        {
            service.ToTable(
                CommunityLifePersistenceConstants.OrganizationServicesTable,
                CommunityLifeModuleMetadata.Schema);

            service.WithOwner().HasForeignKey("OrganizationId");
            service.HasKey("OrganizationId", nameof(OrganizationService.Name));

            service.Property(value => value.Name)
                .HasMaxLength(CommunityLifeLimits.MaxServiceName)
                .IsRequired();
        });

        builder.OwnsMany(organization => organization.MediaReferences, media =>
        {
            media.ToTable(
                CommunityLifePersistenceConstants.OrganizationMediaTable,
                CommunityLifeModuleMetadata.Schema);

            // (media, role) is the identity AttachMedia treats as unique, so it is the key. That is
            // what makes attaching idempotent in the database rather than by convention.
            media.WithOwner().HasForeignKey("OrganizationId");
            media.HasKey(
                "OrganizationId",
                nameof(OrganizationMediaReference.MediaId),
                nameof(OrganizationMediaReference.Role));

            media.Property(value => value.MediaId).IsRequired();

            media.Property(value => value.Role)
                .HasConversion<string>()
                .HasMaxLength(CommunityLifePersistenceConstants.EnumLength)
                .IsRequired();

            media.Property(value => value.Caption)
                .HasMaxLength(CommunityLifeLimits.MaxDisplayName);

            media.Ignore(value => value.IsDocument);

            media.HasIndex(value => value.MediaId)
                .HasDatabaseName("ix_community_life_organization_media_asset");
        });

        builder.OwnsMany(organization => organization.GuideReferences, guide =>
        {
            guide.ToTable(
                CommunityLifePersistenceConstants.OrganizationGuidesTable,
                CommunityLifeModuleMetadata.Schema);

            guide.WithOwner().HasForeignKey("OrganizationId");
            guide.HasKey("OrganizationId", nameof(OrganizationGuideReference.GuideId));

            // Caller-supplied key half. See the leaders above.
            guide.Property(value => value.GuideId).ValueGeneratedNever().IsRequired();

            guide.Property(value => value.Title)
                .HasMaxLength(CommunityLifeLimits.MaxDisplayName);

            guide.Property(value => value.Slug).HasMaxLength(CommunityLifeLimits.MaxSlug);
        });

        builder.OwnsMany(organization => organization.ModerationNotes, note =>
        {
            note.ToTable(
                CommunityLifePersistenceConstants.OrganizationNotesTable,
                CommunityLifeModuleMetadata.Schema);

            note.HasKey(value => value.Id);
            note.Property(value => value.Id).ValueGeneratedNever();

            note.Property(value => value.AuthorId)
                .HasConversion(CommunityLifeValueConverters.Id<UserId>())
                .IsRequired();

            note.Property(value => value.Note)
                .HasMaxLength(CommunityLifeLimits.MaxModerationNote)
                .IsRequired();

            note.Property(value => value.CreatedAt).IsRequired();

            note.Ignore(value => value.DomainEvents);
        });
    }

    private static void ConfigureIndexes(EntityTypeBuilder<Organization> builder)
    {
        // The creator's own dashboard, and the abuse bound's count.
        builder.HasIndex(organization =>
                new { organization.CreatedByUserId, organization.Status })
            .HasDatabaseName("ix_community_life_organizations_creator_status");

        // The public browse predicate, in the order it narrows.
        builder.HasIndex(organization =>
                new { organization.Status, organization.Visibility, organization.ModerationState })
            .HasDatabaseName("ix_community_life_organizations_discovery");

        builder.HasIndex(organization => organization.OrganizationTypeKey)
            .HasDatabaseName("ix_community_life_organizations_type");

        builder.HasIndex(organization => organization.PublishedAt)
            .HasDatabaseName("ix_community_life_organizations_published_at");

        // What separates a living association from a page somebody made once, and the column the
        // most-active sort orders by.
        builder.HasIndex(organization => organization.LastActivityAt)
            .HasDatabaseName("ix_community_life_organizations_activity");

        builder.HasIndex(organization => organization.ProviderId)
            .HasDatabaseName("ix_community_life_organizations_provider");

        builder.HasIndex(organization => organization.ReportCount)
            .HasDatabaseName("ix_community_life_organizations_report_count");
    }
}
