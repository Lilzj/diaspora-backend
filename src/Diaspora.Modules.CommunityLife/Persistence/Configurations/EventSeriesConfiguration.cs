using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Policies;
using Diaspora.Modules.CommunityLife.Domain.Series;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.CommunityLife.Persistence.Configurations;

/// <summary>
/// Maps the recurring series aggregate and the one table it owns.
/// <para>
/// <b>The expansion index is the one that earns its keep.</b> The sweep asks a single question every
/// quarter of an hour — which live series have been written up to a date earlier than the horizon —
/// and without a composite over status and the materialisation mark that is a scan of every series
/// on the platform, run ninety-six times a day.
/// </para>
/// <para>
/// <b>The rule's day lists are columns, not tables.</b> Two joins on every read of a rule that is at
/// most seven small integers would be a poor trade, and the canonical sorted form is what makes rule
/// equality work — which is what stops a no-op edit regenerating a hundred occurrences.
/// </para>
/// <para>
/// <b>The exception collection declares its own key.</b> Without one EF invents a store-generated
/// shadow integer inside a composite primary key, and SQLite refuses the insert the moment a second
/// row exists — the trap B11 paid for and this file does not repeat.
/// </para>
/// </summary>
public sealed class EventSeriesConfiguration : IEntityTypeConfiguration<EventSeries>
{
    public void Configure(EntityTypeBuilder<EventSeries> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            CommunityLifePersistenceConstants.EventSeriesTable,
            CommunityLifeModuleMetadata.Schema);

        builder.HasKey(series => series.Id);

        builder.Property(series => series.Revision).IsConcurrencyToken();

        builder.Property(series => series.Id)
            .HasConversion(CommunityLifeValueConverters.Id<EventSeriesId>());

        builder.Property(series => series.OrganizationId)
            .HasConversion(CommunityLifeValueConverters.Id<OrganizationId>());

        builder.Property(series => series.OrganizerUserId)
            .HasConversion(CommunityLifeValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(series => series.CategoryKey)
            .HasMaxLength(CommunityLifeLimits.MaxCategoryKey)
            .IsRequired();

        builder.Property(series => series.HostName)
            .HasMaxLength(CommunityLifeLimits.MaxDisplayName);

        builder.Property(series => series.CancellationReason)
            .HasMaxLength(CommunityLifeLimits.MaxNoteLength);

        builder.Property(series => series.Status)
            .HasConversion<string>()
            .HasMaxLength(CommunityLifePersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(series => series.Visibility)
            .HasConversion<string>()
            .HasMaxLength(CommunityLifePersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(series => series.AttendanceMode)
            .HasConversion<string>()
            .HasMaxLength(CommunityLifePersistenceConstants.EnumLength)
            .IsRequired();

        ConfigureOwnedValues(builder);
        ConfigureExceptions(builder);
        ConfigureIndexes(builder);

        builder.Ignore(series => series.DomainEvents);
        builder.Ignore(series => series.GeneratesOccurrences);
        builder.Ignore(series => series.PublishesOccurrences);
        builder.Ignore(series => series.ExceptionDates);
        builder.Ignore(series => series.IsPublishable);
    }

    private static void ConfigureOwnedValues(EntityTypeBuilder<EventSeries> builder)
    {
        builder.OwnsOne(series => series.Title, title =>
        {
            title.Property(value => value.Value)
                .HasColumnName("title")
                .HasMaxLength(CommunityLifeLimits.MaxTitle)
                .IsRequired();

            title.HasIndex(value => value.Value)
                .HasDatabaseName("ix_community_life_event_series_title");
        });

        builder.Navigation(series => series.Title).IsRequired();

        builder.OwnsOne(series => series.Slug, slug =>
        {
            slug.Property(value => value.Value)
                .HasColumnName("slug")
                .HasMaxLength(CommunityLifeLimits.MaxSlug)
                .IsRequired();

            // Unique across deleted rows too, for the same reason an event's is: a link shared in a
            // group chat outlives the thing it points at, and a reused handle silently redirects it.
            slug.HasIndex(value => value.Value)
                .IsUnique()
                .HasDatabaseName("ix_community_life_event_series_slug");
        });

        builder.Navigation(series => series.Slug).IsRequired();

        builder.OwnsOne(series => series.Description, description =>
        {
            description.Property(value => value.Summary)
                .HasColumnName("summary")
                .HasMaxLength(CommunityLifeLimits.MaxSummary)
                .IsRequired();

            description.Property(value => value.Body)
                .HasColumnName("description")
                .HasMaxLength(CommunityLifeLimits.MaxDescription);
        });

        builder.Navigation(series => series.Description).IsRequired();

        builder.OwnsOne(series => series.Recurrence, recurrence =>
        {
            recurrence.Property(value => value.Frequency)
                .HasColumnName("recurrence_frequency")
                .HasConversion<string>()
                .HasMaxLength(CommunityLifePersistenceConstants.EnumLength)
                .IsRequired();

            recurrence.Property(value => value.Interval)
                .HasColumnName("recurrence_interval")
                .IsRequired();

            recurrence.Property(value => value.ByWeekday)
                .HasColumnName("recurrence_by_weekday")
                .HasMaxLength(CommunityLifePersistenceConstants.RecurrenceDayListLength)
                .IsRequired();

            recurrence.Property(value => value.ByMonthDay)
                .HasColumnName("recurrence_by_month_day")
                .HasMaxLength(CommunityLifePersistenceConstants.RecurrenceDayListLength)
                .IsRequired();

            recurrence.Property(value => value.EndKind)
                .HasColumnName("recurrence_end_kind")
                .HasConversion<string>()
                .HasMaxLength(CommunityLifePersistenceConstants.EnumLength)
                .IsRequired();

            recurrence.Property(value => value.Until).HasColumnName("recurrence_until");
            recurrence.Property(value => value.Count).HasColumnName("recurrence_count");

            recurrence.Ignore(value => value.Weekdays);
            recurrence.Ignore(value => value.MonthDays);
            recurrence.Ignore(value => value.TotalOccurrences);
        });

        builder.Navigation(series => series.Recurrence).IsRequired();

        builder.OwnsOne(series => series.Schedule, schedule =>
        {
            schedule.Property(value => value.FirstLocalDate)
                .HasColumnName("first_local_date")
                .IsRequired();

            schedule.Property(value => value.LocalStartTime)
                .HasColumnName("local_start_time")
                .IsRequired();

            schedule.Property(value => value.DurationMinutes)
                .HasColumnName("duration_minutes")
                .IsRequired();

            schedule.Property(value => value.TimeZoneId)
                .HasColumnName("time_zone_id")
                .HasMaxLength(CommunityLifePersistenceConstants.TimeZoneIdLength)
                .IsRequired();

            schedule.Ignore(value => value.Duration);
        });

        builder.Navigation(series => series.Schedule).IsRequired();

        builder.OwnsOne(series => series.Location, location =>
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

            location.HasIndex(value => new { value.CountryCode, value.CityName })
                .HasDatabaseName("ix_community_life_event_series_country_city");
        });

        builder.Navigation(series => series.Location).IsRequired();

        builder.OwnsOne(series => series.Venue, venue =>
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
        });

        builder.Navigation(series => series.Venue).IsRequired();

        builder.OwnsOne(series => series.Audience, audience =>
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
        });

        builder.Navigation(series => series.Audience).IsRequired();

        builder.OwnsOne(series => series.Contact, contact =>
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

        builder.Navigation(series => series.Contact).IsRequired();
    }

    private static void ConfigureExceptions(EntityTypeBuilder<EventSeries> builder) =>
        builder.OwnsMany(series => series.Exceptions, exception =>
        {
            exception.ToTable(
                CommunityLifePersistenceConstants.EventSeriesExceptionsTable,
                CommunityLifeModuleMetadata.Schema);

            // The date is the identity, and the aggregate already refuses a second exception on one
            // date. Declaring it beats letting EF invent a shadow integer.
            exception.WithOwner().HasForeignKey("EventSeriesId");
            exception.HasKey("EventSeriesId", nameof(SeriesDateException.LocalDate));

            exception.Property(value => value.LocalDate).ValueGeneratedNever();

            exception.Property(value => value.Reason)
                .HasMaxLength(CommunityLifeLimits.MaxExceptionReason)
                .IsRequired();

            exception.Property(value => value.CreatedAt).IsRequired();
        });

    private static void ConfigureIndexes(EntityTypeBuilder<EventSeries> builder)
    {
        builder.HasIndex(series => new { series.OrganizerUserId, series.Status })
            .HasDatabaseName("ix_community_life_event_series_organizer_status");

        builder.HasIndex(series => new { series.OrganizationId, series.Status })
            .HasDatabaseName("ix_community_life_event_series_organization_status");

        // The expansion sweep's whole work list, in the order it narrows. See the class summary.
        builder.HasIndex(series => new { series.Status, series.MaterialisedThrough })
            .HasDatabaseName("ix_community_life_event_series_expansion");

        builder.HasIndex(series => series.CategoryKey)
            .HasDatabaseName("ix_community_life_event_series_category");
    }
}
