using Diaspora.Modules.CommunityLife.Domain.CommunityEvents;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Policies;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.CommunityLife.Persistence.Configurations;

/// <summary>
/// Maps the event aggregate and the seven tables it owns.
/// <para>
/// <b>The schedule index is the one that matters most, and it is not the same as any other
/// module's.</b> Every other catalogue on this platform is ordered newest-first and indexed on a
/// creation timestamp; an event list is ordered soonest-first and is dominated by a range predicate
/// on the start time. The composite over status and start time is what makes "what is on this week"
/// a seek, and it is the busiest read in the module.
/// </para>
/// <para>
/// The neighbourhood and area-path indexes are the local-discovery pair, over B4's identifier and
/// its materialised path. The coordinate index narrows the nearby seam's bounding box.
/// </para>
/// <para>
/// <b>Owned-collection indexes go inside the <c>OwnsMany</c> block.</b> On the outer builder they
/// silently target the parent table.
/// </para>
/// </summary>
public sealed class CommunityEventConfiguration : IEntityTypeConfiguration<CommunityEvent>
{
    public void Configure(EntityTypeBuilder<CommunityEvent> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            CommunityLifePersistenceConstants.EventsTable,
            CommunityLifeModuleMetadata.Schema);

        builder.HasKey(communityEvent => communityEvent.Id);

        builder.Property(communityEvent => communityEvent.Revision).IsConcurrencyToken();

        builder.Property(communityEvent => communityEvent.Id)
            .HasConversion(CommunityLifeValueConverters.Id<CommunityEventId>());

        builder.Property(communityEvent => communityEvent.OrganizationId)
            .HasConversion(CommunityLifeValueConverters.Id<OrganizationId>());

        builder.Property(communityEvent => communityEvent.OrganizerUserId)
            .HasConversion(CommunityLifeValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(communityEvent => communityEvent.CategoryKey)
            .HasMaxLength(CommunityLifeLimits.MaxCategoryKey)
            .IsRequired();

        builder.Property(communityEvent => communityEvent.HostName)
            .HasMaxLength(CommunityLifeLimits.MaxDisplayName);

        builder.Property(communityEvent => communityEvent.CancellationReason)
            .HasMaxLength(CommunityLifeLimits.MaxNoteLength);

        // B12.1. The series axis. No navigation property and no foreign key to the series table,
        // deliberately: an occurrence is its own aggregate and loading a series must never drag
        // three years of weekly meetings in behind it. The identifier is the reference, and the
        // index below is what makes it navigable.
        builder.Property(communityEvent => communityEvent.SeriesId)
            .HasConversion(CommunityLifeValueConverters.Id<EventSeriesId>());

        ConfigureEnums(builder);
        ConfigureOwnedValues(builder);
        ConfigureOwnedCollections(builder);
        ConfigureIndexes(builder);

        builder.Ignore(communityEvent => communityEvent.DomainEvents);
        builder.Ignore(communityEvent => communityEvent.CoverImage);
        builder.Ignore(communityEvent => communityEvent.IsDiscoverable);
        builder.Ignore(communityEvent => communityEvent.IsFull);
        builder.Ignore(communityEvent => communityEvent.IsPublishable);
        builder.Ignore(communityEvent => communityEvent.IsOccurrence);
    }

    private static void ConfigureEnums(EntityTypeBuilder<CommunityEvent> builder)
    {
        builder.Property(communityEvent => communityEvent.Status)
            .HasConversion<string>()
            .HasMaxLength(CommunityLifePersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(communityEvent => communityEvent.Visibility)
            .HasConversion<string>()
            .HasMaxLength(CommunityLifePersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(communityEvent => communityEvent.ModerationState)
            .HasConversion<string>()
            .HasMaxLength(CommunityLifePersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(communityEvent => communityEvent.AttendanceMode)
            .HasConversion<string>()
            .HasMaxLength(CommunityLifePersistenceConstants.EnumLength)
            .IsRequired();
    }

    private static void ConfigureOwnedValues(EntityTypeBuilder<CommunityEvent> builder)
    {
        builder.OwnsOne(communityEvent => communityEvent.Title, title =>
        {
            title.Property(value => value.Value)
                .HasColumnName("title")
                .HasMaxLength(CommunityLifeLimits.MaxTitle)
                .IsRequired();

            // The duplicate check compares titles within one organizer on one day. Four committee
            // members each posting the same festival is the most common complaint about any
            // community calendar.
            title.HasIndex(value => value.Value)
                .HasDatabaseName("ix_community_life_events_title");
        });

        builder.Navigation(communityEvent => communityEvent.Title).IsRequired();

        builder.OwnsOne(communityEvent => communityEvent.Slug, slug =>
        {
            slug.Property(value => value.Value)
                .HasColumnName("slug")
                .HasMaxLength(CommunityLifeLimits.MaxSlug)
                .IsRequired();

            // Unique across deleted rows too. The link to a funeral or a wedding is pinned in
            // messages years old, and a reused slug would silently redirect them elsewhere.
            slug.HasIndex(value => value.Value)
                .IsUnique()
                .HasDatabaseName("ix_community_life_events_slug");
        });

        builder.Navigation(communityEvent => communityEvent.Slug).IsRequired();

        builder.OwnsOne(communityEvent => communityEvent.Description, description =>
        {
            description.Property(value => value.Summary)
                .HasColumnName("summary")
                .HasMaxLength(CommunityLifeLimits.MaxSummary)
                .IsRequired();

            description.Property(value => value.Body)
                .HasColumnName("description")
                .HasMaxLength(CommunityLifeLimits.MaxDescription);
        });

        builder.Navigation(communityEvent => communityEvent.Description).IsRequired();

        builder.OwnsOne(communityEvent => communityEvent.Schedule, schedule =>
        {
            schedule.Property(value => value.StartsAt).HasColumnName("starts_at").IsRequired();
            schedule.Property(value => value.EndsAt).HasColumnName("ends_at").IsRequired();

            schedule.Property(value => value.TimeZoneId)
                .HasColumnName("time_zone_id")
                .HasMaxLength(CommunityLifePersistenceConstants.TimeZoneIdLength)
                .IsRequired();

            schedule.Property(value => value.RegistrationDeadline)
                .HasColumnName("registration_deadline");

            schedule.Property(value => value.CheckInOpensAt).HasColumnName("checkin_opens_at");

            schedule.Ignore(value => value.Duration);

            // The module's busiest read: what is on, soonest first. Declared inside the owned block
            // because the column lives in the events table but belongs to this value object.
            schedule.HasIndex(value => value.StartsAt)
                .HasDatabaseName("ix_community_life_events_starts_at");

            // The completion sweep's work list.
            schedule.HasIndex(value => value.EndsAt)
                .HasDatabaseName("ix_community_life_events_ends_at");
        });

        builder.Navigation(communityEvent => communityEvent.Schedule).IsRequired();

        builder.OwnsOne(communityEvent => communityEvent.Location, location =>
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

            location.HasIndex(value => value.NeighborhoodAreaId)
                .HasDatabaseName("ix_community_life_events_neighborhood");

            location.HasIndex(value => value.AreaPath)
                .HasDatabaseName("ix_community_life_events_area_path");

            location.HasIndex(value => new { value.CountryCode, value.CityName })
                .HasDatabaseName("ix_community_life_events_country_city");
        });

        builder.Navigation(communityEvent => communityEvent.Location).IsRequired();

        builder.OwnsOne(communityEvent => communityEvent.Venue, venue =>
        {
            venue.Property(value => value.Name)
                .HasColumnName("venue_name")
                .HasMaxLength(CommunityLifeLimits.MaxDisplayName);

            venue.Property(value => value.AddressLine1)
                .HasColumnName("venue_line1")
                .HasMaxLength(CommunityLifeLimits.MaxDisplayName);

            venue.Property(value => value.AddressLine2)
                .HasColumnName("venue_line2")
                .HasMaxLength(CommunityLifeLimits.MaxDisplayName);

            // Its own column and never sent to a geocoder. "Opposite the Total filling station" is
            // how people actually find somewhere across much of the diaspora this platform serves.
            venue.Property(value => value.Landmark)
                .HasColumnName("venue_landmark")
                .HasMaxLength(CommunityLifeLimits.MaxDisplayName);

            venue.Property(value => value.PostalCode)
                .HasColumnName("venue_postal_code")
                .HasMaxLength(16);

            venue.Property(value => value.Latitude)
                .HasColumnName("latitude")
                .HasColumnType(CommunityLifePersistenceConstants.CoordinateColumnType);

            venue.Property(value => value.Longitude)
                .HasColumnName("longitude")
                .HasColumnType(CommunityLifePersistenceConstants.CoordinateColumnType);

            venue.Property(value => value.OnlineUrl)
                .HasColumnName("online_url")
                .HasMaxLength(CommunityLifeLimits.MaxUrl);

            venue.Ignore(value => value.HasCoordinate);
            venue.Ignore(value => value.IsAddressable);
            venue.Ignore(value => value.HasOnlineAccess);

            // What the nearby seam narrows its bounding box with.
            venue.HasIndex(value => new { value.Latitude, value.Longitude })
                .HasDatabaseName("ix_community_life_events_coordinate");
        });

        builder.Navigation(communityEvent => communityEvent.Venue).IsRequired();

        builder.OwnsOne(communityEvent => communityEvent.Capacity, capacity =>
        {
            capacity.Property(value => value.Maximum).HasColumnName("capacity_maximum");
            capacity.Property(value => value.GoingCount).HasColumnName("going_count");
            capacity.Property(value => value.InterestedCount).HasColumnName("interested_count");
            capacity.Property(value => value.WaitlistCount).HasColumnName("waitlist_count");
            capacity.Property(value => value.CheckedInCount).HasColumnName("checked_in_count");

            capacity.Ignore(value => value.IsUnlimited);
            capacity.Ignore(value => value.RemainingPlaces);
            capacity.Ignore(value => value.IsFull);
            capacity.Ignore(value => value.AttendanceRate);

            // The most-popular sort, and the has-places-remaining filter.
            capacity.HasIndex(value => value.GoingCount)
                .HasDatabaseName("ix_community_life_events_going_count");
        });

        builder.Navigation(communityEvent => communityEvent.Capacity).IsRequired();

        builder.OwnsOne(communityEvent => communityEvent.Audience, audience =>
        {
            audience.Property(value => value.IsFamilyFriendly).HasColumnName("family_friendly");
            audience.Property(value => value.IsChildFriendly).HasColumnName("child_friendly");
            audience.Property(value => value.IsStudentFocused).HasColumnName("student_focused");
            audience.Property(value => value.IsFree).HasColumnName("is_free");

            audience.Property(value => value.DressCode)
                .HasColumnName("dress_code")
                .HasMaxLength(CommunityLifeLimits.MaxDressCode);

            audience.Property(value => value.AccessibilityNotes)
                .HasColumnName("accessibility_notes")
                .HasMaxLength(CommunityLifeLimits.MaxAccessibilityNotes);

            // Three of the search facets the brief names, in the order they narrow.
            audience.HasIndex(value =>
                    new { value.IsFree, value.IsFamilyFriendly, value.IsStudentFocused })
                .HasDatabaseName("ix_community_life_events_audience");
        });

        builder.Navigation(communityEvent => communityEvent.Audience).IsRequired();

        builder.OwnsOne(communityEvent => communityEvent.Contact, contact =>
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

        builder.Navigation(communityEvent => communityEvent.Contact).IsRequired();

        builder.OwnsOne(communityEvent => communityEvent.Engagement, engagement =>
        {
            engagement.Property(value => value.ViewCount).HasColumnName("view_count");
            engagement.Property(value => value.UniqueViewCount).HasColumnName("unique_view_count");
            engagement.Property(value => value.SaveCount).HasColumnName("save_count");
            engagement.Property(value => value.ShareCount).HasColumnName("share_count");
            engagement.Property(value => value.RsvpCount).HasColumnName("rsvp_count");

            engagement.Ignore(value => value.ConversionRate);
        });

        builder.Navigation(communityEvent => communityEvent.Engagement).IsRequired();
    }

    private static void ConfigureOwnedCollections(EntityTypeBuilder<CommunityEvent> builder)
    {
        builder.OwnsMany(communityEvent => communityEvent.AgendaItems, item =>
        {
            item.ToTable(
                CommunityLifePersistenceConstants.EventAgendaTable,
                CommunityLifeModuleMetadata.Schema);

            item.HasKey(value => value.Id);
            item.Property(value => value.Id).ValueGeneratedNever();

            item.Property(value => value.Title)
                .HasMaxLength(CommunityLifeLimits.MaxAgendaTitle)
                .IsRequired();

            item.Property(value => value.Description)
                .HasMaxLength(CommunityLifeLimits.MaxNoteLength);

            item.Property(value => value.SpeakerName)
                .HasMaxLength(CommunityLifeLimits.MaxDisplayName);

            item.Ignore(value => value.DomainEvents);
        });

        builder.OwnsMany(communityEvent => communityEvent.Speakers, speaker =>
        {
            speaker.ToTable(
                CommunityLifePersistenceConstants.EventSpeakersTable,
                CommunityLifeModuleMetadata.Schema);

            speaker.HasKey(value => value.Id);
            speaker.Property(value => value.Id).ValueGeneratedNever();

            speaker.Property(value => value.Name)
                .HasMaxLength(CommunityLifeLimits.MaxDisplayName)
                .IsRequired();

            speaker.Property(value => value.Title)
                .HasMaxLength(CommunityLifeLimits.MaxDisplayName);

            speaker.Property(value => value.Organization)
                .HasMaxLength(CommunityLifeLimits.MaxDisplayName);

            speaker.Property(value => value.Biography)
                .HasMaxLength(CommunityLifeLimits.MaxNoteLength);

            speaker.Ignore(value => value.DomainEvents);
        });

        builder.OwnsMany(communityEvent => communityEvent.MediaReferences, media =>
        {
            media.ToTable(
                CommunityLifePersistenceConstants.EventMediaTable,
                CommunityLifeModuleMetadata.Schema);

            media.WithOwner().HasForeignKey("CommunityEventId");
            media.HasKey(
                "CommunityEventId",
                nameof(EventMediaReference.MediaId),
                nameof(EventMediaReference.Role));

            media.Property(value => value.MediaId).IsRequired();

            media.Property(value => value.Role)
                .HasConversion<string>()
                .HasMaxLength(CommunityLifePersistenceConstants.EnumLength)
                .IsRequired();

            media.Property(value => value.Caption)
                .HasMaxLength(CommunityLifeLimits.MaxDisplayName);

            media.Ignore(value => value.IsDocument);

            media.HasIndex(value => value.MediaId)
                .HasDatabaseName("ix_community_life_event_media_asset");
        });

        builder.OwnsMany(communityEvent => communityEvent.GuideReferences, guide =>
        {
            guide.ToTable(
                CommunityLifePersistenceConstants.EventGuidesTable,
                CommunityLifeModuleMetadata.Schema);

            guide.WithOwner().HasForeignKey("CommunityEventId");
            guide.HasKey("CommunityEventId", nameof(EventGuideReference.GuideId));

            guide.Property(value => value.GuideId).ValueGeneratedNever().IsRequired();
            guide.Property(value => value.Title).HasMaxLength(CommunityLifeLimits.MaxDisplayName);
            guide.Property(value => value.Slug).HasMaxLength(CommunityLifeLimits.MaxSlug);
        });

        builder.OwnsMany(communityEvent => communityEvent.ProviderReferences, provider =>
        {
            provider.ToTable(
                CommunityLifePersistenceConstants.EventProvidersTable,
                CommunityLifeModuleMetadata.Schema);

            provider.WithOwner().HasForeignKey("CommunityEventId");
            provider.HasKey("CommunityEventId", nameof(EventProviderReference.ProviderId));

            provider.Property(value => value.ProviderId).ValueGeneratedNever().IsRequired();

            provider.Property(value => value.ProviderName)
                .HasMaxLength(CommunityLifeLimits.MaxDisplayName);

            provider.Property(value => value.Role)
                .HasMaxLength(CommunityLifeLimits.MaxDisplayName);

            provider.HasIndex(value => value.ProviderId)
                .HasDatabaseName("ix_community_life_event_providers_provider");
        });

        builder.OwnsMany(communityEvent => communityEvent.LinkedResources, resource =>
        {
            resource.ToTable(
                CommunityLifePersistenceConstants.EventLinkedResourcesTable,
                CommunityLifeModuleMetadata.Schema);

            // (type, identifier) is the identity SetLinkedResources deduplicates on. An external
            // link carries Guid.Empty, so several of them share a key half — which is why the
            // order column joins the key rather than the URL, whose length would make a poor one.
            resource.WithOwner().HasForeignKey("CommunityEventId");
            resource.HasKey(
                "CommunityEventId",
                nameof(EventLinkedResource.ResourceType),
                nameof(EventLinkedResource.ResourceId),
                nameof(EventLinkedResource.Order));

            resource.Property(value => value.ResourceType)
                .HasConversion<string>()
                .HasMaxLength(CommunityLifePersistenceConstants.EnumLength)
                .IsRequired();

            resource.Property(value => value.ResourceId).ValueGeneratedNever();

            resource.Property(value => value.Label)
                .HasMaxLength(CommunityLifeLimits.MaxDisplayName);

            resource.Property(value => value.Url).HasMaxLength(CommunityLifeLimits.MaxUrl);

            // "Which events reference this job posting" — asked by nothing in this module and by
            // the client on every career-fair screen.
            resource.HasIndex(value => value.ResourceId)
                .HasDatabaseName("ix_community_life_event_links_resource");
        });

        builder.OwnsMany(communityEvent => communityEvent.ModerationNotes, note =>
        {
            note.ToTable(
                CommunityLifePersistenceConstants.EventNotesTable,
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

    private static void ConfigureIndexes(EntityTypeBuilder<CommunityEvent> builder)
    {
        // The organizer's own dashboard, and the abuse bound's count.
        builder.HasIndex(communityEvent =>
                new { communityEvent.OrganizerUserId, communityEvent.Status })
            .HasDatabaseName("ix_community_life_events_organizer_status");

        // Everything one organization is running.
        builder.HasIndex(communityEvent =>
                new { communityEvent.OrganizationId, communityEvent.Status })
            .HasDatabaseName("ix_community_life_events_organization_status");

        // The public browse predicate, in the order it narrows.
        builder.HasIndex(communityEvent =>
                new
                {
                    communityEvent.Status,
                    communityEvent.Visibility,
                    communityEvent.ModerationState
                })
            .HasDatabaseName("ix_community_life_events_discovery");

        builder.HasIndex(communityEvent => communityEvent.CategoryKey)
            .HasDatabaseName("ix_community_life_events_category");

        builder.HasIndex(communityEvent => communityEvent.PublishedAt)
            .HasDatabaseName("ix_community_life_events_published_at");

        builder.HasIndex(communityEvent => communityEvent.ReportCount)
            .HasDatabaseName("ix_community_life_events_report_count");

        // B12.1. Every occurrence read — the page, the rewrite list, the sequences already written
        // — narrows on the series first and orders on the sequence, so the composite serves all
        // three and the ordering never needs a sort. It is also the index that keeps the sweep's
        // idempotency check off the events table's other paths entirely.
        builder.HasIndex(communityEvent =>
                new { communityEvent.SeriesId, communityEvent.SeriesSequence })
            .HasDatabaseName("ix_community_life_events_series_sequence");
    }
}
