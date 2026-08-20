using Diaspora.Modules.Analytics.Domain.Dimensions;
using Diaspora.Modules.Analytics.Domain.Events;
using Diaspora.Modules.Analytics.Domain.Identifiers;
using Diaspora.Modules.Analytics.Domain.Projection;
using Diaspora.Modules.Analytics.Domain.Rollups;
using Diaspora.Modules.Analytics.Domain.Schema;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Diaspora.Modules.Analytics.Persistence.Configurations;

/// <summary>The module's table names and column widths, in one place.</summary>
internal static class AnalyticsPersistenceConstants
{
    public const string EventsTable = "analytics_events";
    public const string ContentDimensionsTable = "analytics_content_dimensions";
    public const string ContentDailyTable = "analytics_content_daily";
    public const string OwnerDailyTable = "analytics_owner_daily";
    public const string SchemasTable = "analytics_event_schemas";
    public const string CheckpointTable = "analytics_projection_checkpoint";
    public const string BackfillRunsTable = "analytics_backfill_runs";

    /// <summary>Enum names are stored as strings, so a member added later reads correctly on old rows.</summary>
    public const int EnumLength = 32;

    /// <summary>An event type. Registry-checked, so the width is generous rather than tight.</summary>
    public const int EventTypeLength = 96;

    /// <summary>
    /// The deduplication key.
    /// <para>
    /// Composed from an event type, two identifiers and a date, so 200 is comfortable. It is
    /// uniquely indexed, and a bounded width is what lets a provider index it without a hash.
    /// </para>
    /// </summary>
    public const int DedupKeyLength = 200;

    /// <summary>A base64 SHA-256, so exactly 44 characters. Widened slightly for a future algorithm.</summary>
    public const int ActorKeyLength = 64;

    public const int CountryLength = 8;
    public const int LabelLength = 200;
    public const int ShortLabelLength = 64;
    public const int DescriptionLength = 500;
}

/// <summary>The module's identifier converters. The platform's generic converter, never a second encoding.</summary>
internal static class AnalyticsValueConverters
{
    public static ValueConverter Id<TId>() where TId : StronglyTypedId =>
        new StronglyTypedIdValueConverter<TId>();
}

/// <summary>
/// Maps the raw event store.
/// <para>
/// <b>Five indexes, and each one answers a query this module actually makes.</b> §24 of the phase
/// brief forbids indexing every column; what it asks for is that the expensive paths have one, and
/// the expensive paths here are known because there are only four of them: the projection walks
/// ingestion order, the deduplication check probes a key, the recomputation reads one cell, and the
/// distinct-viewer count scans one owner's window.
/// </para>
/// <para>
/// <b>The unique index on the deduplication key is the idempotency barrier and not a hint.</b> A
/// pre-read tells the common case cheaply; this is what stops two workers racing one redelivery from
/// both writing. Without it the module would be correct under a single instance and wrong under the
/// horizontal scaling §29 requires.
/// </para>
/// </summary>
public sealed class AnalyticsEventConfiguration : IEntityTypeConfiguration<AnalyticsEvent>
{
    public void Configure(EntityTypeBuilder<AnalyticsEvent> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(AnalyticsPersistenceConstants.EventsTable, AnalyticsModuleMetadata.Schema);

        builder.HasKey(row => row.Id);

        builder.Property(row => row.Id)
            .HasConversion(AnalyticsValueConverters.Id<AnalyticsEventId>())
            .ValueGeneratedNever();

        builder.Property(row => row.EventType)
            .HasMaxLength(AnalyticsPersistenceConstants.EventTypeLength)
            .IsRequired();

        builder.Property(row => row.EventVersion).IsRequired();

        builder.Property(row => row.TrustClass)
            .HasConversion<string>()
            .HasMaxLength(AnalyticsPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(row => row.SourceModule)
            .HasMaxLength(AnalyticsPersistenceConstants.ShortLabelLength)
            .IsRequired();

        builder.Property(row => row.DedupKey)
            .HasMaxLength(AnalyticsPersistenceConstants.DedupKeyLength)
            .IsRequired();

        builder.Property(row => row.ContentType)
            .HasConversion<string>()
            .HasMaxLength(AnalyticsPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(row => row.ActorKey)
            .HasMaxLength(AnalyticsPersistenceConstants.ActorKeyLength);

        builder.Property(row => row.CountryCode)
            .HasMaxLength(AnalyticsPersistenceConstants.CountryLength);

        builder.Property(row => row.Surface)
            .HasMaxLength(AnalyticsPersistenceConstants.ShortLabelLength);

        builder.Property(row => row.Qualifier)
            .HasMaxLength(AnalyticsPersistenceConstants.ShortLabelLength);

        // The idempotency barrier. See the class summary.
        builder.HasIndex(row => row.DedupKey)
            .IsUnique()
            .HasDatabaseName("ux_analytics_events_dedup");

        // The projection's walk: ingestion order, with the identifier for the tie at the boundary.
        builder.HasIndex(row => new { row.ReceivedAt, row.Id })
            .HasDatabaseName("ix_analytics_events_received");

        // One cell's recomputation: every row for one item on one day.
        builder.HasIndex(row => new { row.ContentType, row.ContentId, row.OccurredOn })
            .HasDatabaseName("ix_analytics_events_content_day");

        // The owner's window: the distinct-viewer count and the owner-cell rebuild.
        builder.HasIndex(row => new { row.OwnerUserId, row.OccurredOn })
            .HasDatabaseName("ix_analytics_events_owner_day");

        // Retention and erasure. Both delete by a predicate rather than by key, so both need one.
        builder.HasIndex(row => row.OccurredOn)
            .HasDatabaseName("ix_analytics_events_occurred_on");

        builder.HasIndex(row => row.ActorKey)
            .HasDatabaseName("ix_analytics_events_actor");

        builder.Ignore(row => row.DomainEvents);
    }
}

/// <summary>
/// Maps the content dimension.
/// <para>
/// The unique index on <c>(ContentType, ContentId)</c> is what makes the pair the natural key: two
/// modules can legitimately publish the same Guid for different things, and a unique index on the
/// identifier alone would make one of them invisible.
/// </para>
/// </summary>
public sealed class AnalyticsContentDimensionConfiguration
    : IEntityTypeConfiguration<AnalyticsContentDimension>
{
    public void Configure(EntityTypeBuilder<AnalyticsContentDimension> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            AnalyticsPersistenceConstants.ContentDimensionsTable, AnalyticsModuleMetadata.Schema);

        builder.HasKey(row => row.Id);

        builder.Property(row => row.Id)
            .HasConversion(AnalyticsValueConverters.Id<AnalyticsContentDimensionId>())
            .ValueGeneratedNever();

        builder.Property(row => row.Revision).IsConcurrencyToken();

        builder.Property(row => row.ContentType)
            .HasConversion<string>()
            .HasMaxLength(AnalyticsPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(row => row.Title)
            .HasMaxLength(AnalyticsPersistenceConstants.LabelLength);

        builder.Property(row => row.Status)
            .HasMaxLength(AnalyticsPersistenceConstants.EnumLength);

        builder.Property(row => row.CategoryKey)
            .HasMaxLength(AnalyticsPersistenceConstants.ShortLabelLength);

        builder.Property(row => row.CountryCode)
            .HasMaxLength(AnalyticsPersistenceConstants.CountryLength);

        builder.HasIndex(row => new { row.ContentType, row.ContentId })
            .IsUnique()
            .HasDatabaseName("ux_analytics_content_dimension");

        // Everything one owner has published — the content-performance list's universe.
        builder.HasIndex(row => new { row.OwnerUserId, row.ContentType })
            .HasDatabaseName("ix_analytics_content_dimension_owner");

        builder.Ignore(row => row.DomainEvents);
    }
}

/// <summary>
/// Maps one day's figures for one piece of content.
/// <para>
/// <b><c>Engagements</c> is ignored deliberately.</b> It is a computed property on the owned counts
/// record — the sum of four columns — and mapping it would store a derived value that a definition
/// change would leave stale in every historical row. Computing it on read costs nothing and cannot
/// go out of date.
/// </para>
/// </summary>
public sealed class ContentDailyRollupConfiguration : IEntityTypeConfiguration<ContentDailyRollup>
{
    public void Configure(EntityTypeBuilder<ContentDailyRollup> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(AnalyticsPersistenceConstants.ContentDailyTable, AnalyticsModuleMetadata.Schema);

        builder.HasKey(row => row.Id);

        builder.Property(row => row.Id)
            .HasConversion(AnalyticsValueConverters.Id<ContentDailyRollupId>())
            .ValueGeneratedNever();

        builder.Property(row => row.ContentType)
            .HasConversion<string>()
            .HasMaxLength(AnalyticsPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(row => row.CountryCode)
            .HasMaxLength(AnalyticsPersistenceConstants.CountryLength);

        builder.OwnsOne(row => row.Counts, counts =>
        {
            counts.Property(value => value.Views).HasColumnName("views");
            counts.Property(value => value.Saves).HasColumnName("saves");
            counts.Property(value => value.Shares).HasColumnName("shares");
            counts.Property(value => value.Comments).HasColumnName("comments");
            counts.Property(value => value.Reactions).HasColumnName("reactions");
            counts.Property(value => value.HelpfulVotes).HasColumnName("helpful_votes");
            counts.Property(value => value.Conversions).HasColumnName("conversions");
            counts.Property(value => value.Published).HasColumnName("published");
            counts.Property(value => value.FollowsGained).HasColumnName("follows_gained");
            counts.Property(value => value.FollowsLost).HasColumnName("follows_lost");
            counts.Ignore(value => value.Engagements);
        });

        builder.Navigation(row => row.Counts).IsRequired();

        // One cell per item per day, refused at the database. Two rows would mean two answers.
        builder.HasIndex(row => new { row.ContentType, row.ContentId, row.Day })
            .IsUnique()
            .HasDatabaseName("ux_analytics_content_daily");

        // The creator's per-item breakdown for a period.
        builder.HasIndex(row => new { row.OwnerUserId, row.Day })
            .HasDatabaseName("ix_analytics_content_daily_owner");

        // Retention deletes by day.
        builder.HasIndex(row => row.Day)
            .HasDatabaseName("ix_analytics_content_daily_day");

        builder.Ignore(row => row.DomainEvents);
    }
}

/// <summary>Maps one day's figures for one owner.</summary>
public sealed class OwnerDailyRollupConfiguration : IEntityTypeConfiguration<OwnerDailyRollup>
{
    public void Configure(EntityTypeBuilder<OwnerDailyRollup> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(AnalyticsPersistenceConstants.OwnerDailyTable, AnalyticsModuleMetadata.Schema);

        builder.HasKey(row => row.Id);

        builder.Property(row => row.Id)
            .HasConversion(AnalyticsValueConverters.Id<OwnerDailyRollupId>())
            .ValueGeneratedNever();

        builder.OwnsOne(row => row.Counts, counts =>
        {
            counts.Property(value => value.Views).HasColumnName("views");
            counts.Property(value => value.Saves).HasColumnName("saves");
            counts.Property(value => value.Shares).HasColumnName("shares");
            counts.Property(value => value.Comments).HasColumnName("comments");
            counts.Property(value => value.Reactions).HasColumnName("reactions");
            counts.Property(value => value.HelpfulVotes).HasColumnName("helpful_votes");
            counts.Property(value => value.Conversions).HasColumnName("conversions");
            counts.Property(value => value.Published).HasColumnName("published");
            counts.Property(value => value.FollowsGained).HasColumnName("follows_gained");
            counts.Property(value => value.FollowsLost).HasColumnName("follows_lost");
            counts.Ignore(value => value.Engagements);
        });

        builder.Navigation(row => row.Counts).IsRequired();

        builder.HasIndex(row => new { row.OwnerUserId, row.Day })
            .IsUnique()
            .HasDatabaseName("ux_analytics_owner_daily");

        builder.HasIndex(row => row.Day)
            .HasDatabaseName("ix_analytics_owner_daily_day");

        builder.Ignore(row => row.DomainEvents);
    }
}

/// <summary>
/// Maps the event schema registry.
/// <para>
/// Reference data. A schema change is a new row rather than an edit, which is why the unique index
/// spans the name <em>and</em> the version.
/// </para>
/// </summary>
public sealed class AnalyticsEventSchemaConfiguration : IEntityTypeConfiguration<AnalyticsEventSchema>
{
    public void Configure(EntityTypeBuilder<AnalyticsEventSchema> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(AnalyticsPersistenceConstants.SchemasTable, AnalyticsModuleMetadata.Schema);

        builder.HasKey(row => row.Id);

        builder.Property(row => row.Id)
            .HasConversion(AnalyticsValueConverters.Id<AnalyticsEventSchemaId>())
            .ValueGeneratedNever();

        builder.Property(row => row.EventType)
            .HasMaxLength(AnalyticsPersistenceConstants.EventTypeLength)
            .IsRequired();

        builder.Property(row => row.TrustClass)
            .HasConversion<string>()
            .HasMaxLength(AnalyticsPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(row => row.DedupStrategy)
            .HasConversion<string>()
            .HasMaxLength(AnalyticsPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(row => row.Description)
            .HasMaxLength(AnalyticsPersistenceConstants.DescriptionLength)
            .IsRequired();

        builder.HasIndex(row => new { row.EventType, row.Version })
            .IsUnique()
            .HasDatabaseName("ux_analytics_event_schemas");

        builder.Ignore(row => row.DomainEvents);
    }
}

/// <summary>Maps the projection cursor. Exactly one row.</summary>
public sealed class AnalyticsProjectionCheckpointConfiguration
    : IEntityTypeConfiguration<AnalyticsProjectionCheckpoint>
{
    public void Configure(EntityTypeBuilder<AnalyticsProjectionCheckpoint> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(AnalyticsPersistenceConstants.CheckpointTable, AnalyticsModuleMetadata.Schema);

        builder.HasKey(row => row.Id);

        builder.Property(row => row.Id)
            .HasConversion(AnalyticsValueConverters.Id<AnalyticsCheckpointId>())
            .ValueGeneratedNever();

        builder.Property(row => row.Revision).IsConcurrencyToken();

        builder.Ignore(row => row.DomainEvents);
    }
}

/// <summary>Maps administrative backfill runs.</summary>
public sealed class AnalyticsBackfillRunConfiguration : IEntityTypeConfiguration<AnalyticsBackfillRun>
{
    public void Configure(EntityTypeBuilder<AnalyticsBackfillRun> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(AnalyticsPersistenceConstants.BackfillRunsTable, AnalyticsModuleMetadata.Schema);

        builder.HasKey(row => row.Id);

        builder.Property(row => row.Id)
            .HasConversion(AnalyticsValueConverters.Id<AnalyticsBackfillRunId>())
            .ValueGeneratedNever();

        builder.Property(row => row.Revision).IsConcurrencyToken();

        builder.Property(row => row.Status)
            .HasConversion<string>()
            .HasMaxLength(AnalyticsPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(row => row.FailureReason)
            .HasMaxLength(AnalyticsPersistenceConstants.DescriptionLength);

        // At most one run is queued or running; the handler checks this index's predicate first and
        // the status column is what makes that check a seek rather than a scan.
        builder.HasIndex(row => row.Status)
            .HasDatabaseName("ix_analytics_backfill_runs_status");

        builder.Ignore(row => row.DomainEvents);
    }
}
