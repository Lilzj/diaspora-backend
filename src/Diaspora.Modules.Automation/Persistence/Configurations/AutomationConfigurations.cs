using Diaspora.Modules.Automation.Domain.Automations;
using Diaspora.Modules.Automation.Domain.Identifiers;
using Diaspora.Modules.Automation.Domain.Runs;
using Diaspora.Modules.Automation.Domain.SavedSearches;
using Diaspora.Modules.Automation.Domain.Scheduling;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Diaspora.Modules.Automation.Persistence.Configurations;

/// <summary>The module's table names and column widths, in one place.</summary>
internal static class AutomationPersistenceConstants
{
    public const string AutomationsTable = "automations";
    public const string ConditionsTable = "automation_conditions";
    public const string ActionsTable = "automation_actions";
    public const string AuditTable = "automation_audit";
    public const string RunsTable = "automation_runs";
    public const string ConditionOutcomesTable = "automation_run_conditions";
    public const string ActionOutcomesTable = "automation_run_actions";
    public const string OccurrencesTable = "automation_occurrences";

    /// <summary>Wave 12: one dated subject the platform will look at again, not one per watcher.</summary>
    public const string SubjectHorizonsTable = "automation_subject_horizons";

    public const string RemindersTable = "automation_reminders";
    public const string SavedSearchesTable = "automation_saved_searches";
    public const string SavedSearchMatchesTable = "automation_saved_search_matches";

    /// <summary>Enums are stored as strings, so a member added later reads correctly on old rows.</summary>
    public const int EnumLength = 32;

    /// <summary>A dotted catalogue key. Capped, and validated against the catalogue on the way in.</summary>
    public const int KeyLength = 64;

    /// <summary>A serialized scalar bag. The options ceiling is smaller; this is the column's.</summary>
    public const int ParameterLength = 4_000;

    public const int NameLength = 120;
    public const int DescriptionLength = 500;
    public const int SummaryLength = 300;
    public const int TimeZoneLength = 64;

    /// <summary>Delimited identifier lists. Thirty-two 36-character ids plus separators.</summary>
    public const int IdListLength = 1_200;
}

/// <summary>The module's identifier converters. The platform's generic one, never a second encoding.</summary>
internal static class AutomationValueConverters
{
    public static ValueConverter Id<TId>() where TId : StronglyTypedId =>
        new StronglyTypedIdValueConverter<TId>();
}

/// <summary>
/// Maps the registry.
/// <para>
/// <b>No owned type and no owned collection anywhere in this module.</b> Conditions, actions and audit
/// entries are child entities with declared keys; scope lists are delimited columns; parameter bags are
/// serialized strings. That is a deliberate narrowing rather than a stylistic one — this platform has
/// four recorded defects in owned-type mapping and none of them is reachable from a schema with no
/// owned types in it.
/// </para>
/// <para>
/// <b>Nothing here is <c>ISoftDeletable</c>, and that is a decision.</b> A deleted automation is gone:
/// the contract requires deleting twice to answer the same thing, and a soft-deleted row disappears
/// behind the global query filter, which would make the second delete a 404 where the contract
/// requires a 200. Its runs are kept — an execution history that vanished with the rule would remove
/// the evidence of what a member was told — and they carry the denormalised name for exactly that.
/// </para>
/// </summary>
internal sealed class AutomationConfiguration : IEntityTypeConfiguration<AutomationDefinition>
{
    public void Configure(EntityTypeBuilder<AutomationDefinition> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            AutomationPersistenceConstants.AutomationsTable, AutomationModuleMetadata.Schema);

        builder.HasKey(row => row.Id);

        builder.Property(row => row.Id)
            .HasConversion(AutomationValueConverters.Id<AutomationId>())
            .ValueGeneratedNever();

        builder.Property(row => row.OwnerKind)
            .HasConversion<string>()
            .HasMaxLength(AutomationPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(row => row.OwnerId).IsRequired();

        builder.Property(row => row.OwnerLabel)
            .HasMaxLength(AutomationPersistenceConstants.NameLength)
            .IsRequired();

        builder.Property(row => row.CreatedByUserId)
            .HasConversion(AutomationValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(row => row.Name)
            .HasMaxLength(AutomationPersistenceConstants.NameLength)
            .IsRequired();

        builder.Property(row => row.Description)
            .HasMaxLength(AutomationPersistenceConstants.DescriptionLength)
            .IsRequired();

        builder.Property(row => row.Status)
            .HasConversion<string>()
            .HasMaxLength(AutomationPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(row => row.Source)
            .HasConversion<string>()
            .HasMaxLength(AutomationPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(row => row.TemplateId).HasMaxLength(100);

        builder.Property(row => row.TriggerKey)
            .HasMaxLength(AutomationPersistenceConstants.KeyLength)
            .IsRequired();

        builder.Property(row => row.TriggerParameters)
            .HasMaxLength(AutomationPersistenceConstants.ParameterLength)
            .IsRequired();

        builder.Property(row => row.CommunityIds)
            .HasMaxLength(AutomationPersistenceConstants.IdListLength)
            .IsRequired();

        builder.Property(row => row.CountryCodes).HasMaxLength(256).IsRequired();

        builder.Property(row => row.NeighbourhoodIds)
            .HasMaxLength(AutomationPersistenceConstants.IdListLength)
            .IsRequired();

        builder.Property(row => row.Frequency)
            .HasConversion<string>()
            .HasMaxLength(AutomationPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(row => row.TimezoneMode)
            .HasConversion<string>()
            .HasMaxLength(AutomationPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(row => row.TimeZoneId)
            .HasMaxLength(AutomationPersistenceConstants.TimeZoneLength);

        builder.Property(row => row.DaysOfWeek).HasMaxLength(32).IsRequired();

        builder.Property(row => row.EventOffsetAnchor).HasMaxLength(8);

        builder.Property(row => row.CustomDescription).HasMaxLength(200);

        builder.Property(row => row.AttentionReason)
            .HasMaxLength(AutomationPersistenceConstants.SummaryLength);

        builder.Property(row => row.IdempotencyKey).HasMaxLength(128);

        builder.Property(row => row.LastRunStatus)
            .HasConversion<string>()
            .HasMaxLength(AutomationPersistenceConstants.EnumLength);

        // The optimistic-concurrency token. Without IsConcurrencyToken() two writers overwrite each
        // other in silence — the second UPDATE succeeds, matches one row, and discards the first
        // writer's change with no error anywhere. DSP3001 reports the omission.
        builder.Property(row => row.Revision)
            .IsConcurrencyToken()
            .IsRequired();

        // ── Children ────────────────────────────────────────────────────────────────────────────
        //
        // Cascade, because a condition without its automation is not a thing. The backing field is
        // named for EF's convention: a read-only getter forces EF through the field, which it finds by
        // name alone, and a mismatch throws "Collection is read-only" only on read-back.
        builder.HasMany(row => row.Conditions)
            .WithOne()
            .HasForeignKey(child => child.AutomationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(row => row.Conditions)
            .HasField("_conditions")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(row => row.Actions)
            .WithOne()
            .HasForeignKey(child => child.AutomationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(row => row.Actions)
            .HasField("_actions")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(row => row.Audit)
            .WithOne()
            .HasForeignKey(child => child.AutomationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(row => row.Audit)
            .HasField("_audit")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // ── Indexes, and the query each one answers ─────────────────────────────────────────────

        // The engine's hot path: every active automation watching one trigger. Without this, a
        // platform-wide fact is a table scan on every published event.
        builder.HasIndex(row => new { row.TriggerKey, row.Status });

        // The Automation Center's list, and the ceiling check.
        builder.HasIndex(row => new { row.OwnerKind, row.OwnerId, row.Status });

        // The creation idempotency lookup, scoped to the member so one member's key cannot return
        // another member's automation.
        builder.HasIndex(row => new { row.CreatedByUserId, row.IdempotencyKey })
            .HasFilter(null);

        // The scheduler's sweep candidate list.
        builder.HasIndex(row => new { row.Status, row.NextRunAt });

        builder.Ignore(row => row.DomainEvents);
    }
}

internal sealed class AutomationConditionRuleConfiguration
    : IEntityTypeConfiguration<AutomationConditionRule>
{
    public void Configure(EntityTypeBuilder<AutomationConditionRule> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            AutomationPersistenceConstants.ConditionsTable, AutomationModuleMetadata.Schema);

        builder.HasKey(row => row.Id);

        builder.Property(row => row.Id)
            .HasConversion(AutomationValueConverters.Id<AutomationConditionId>())
            .ValueGeneratedNever();

        builder.Property(row => row.AutomationId)
            .HasConversion(AutomationValueConverters.Id<AutomationId>())
            .IsRequired();

        builder.Property(row => row.Key)
            .HasMaxLength(AutomationPersistenceConstants.KeyLength)
            .IsRequired();

        builder.Property(row => row.Operator).HasMaxLength(24).IsRequired();

        builder.Property(row => row.Value).HasMaxLength(512);

        builder.Property(row => row.SecondaryValue).HasMaxLength(512);

        builder.HasIndex(row => row.AutomationId);

        builder.Ignore(row => row.DomainEvents);
    }
}

internal sealed class AutomationActionStepConfiguration : IEntityTypeConfiguration<AutomationActionStep>
{
    public void Configure(EntityTypeBuilder<AutomationActionStep> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(AutomationPersistenceConstants.ActionsTable, AutomationModuleMetadata.Schema);

        builder.HasKey(row => row.Id);

        builder.Property(row => row.Id)
            .HasConversion(AutomationValueConverters.Id<AutomationActionId>())
            .ValueGeneratedNever();

        builder.Property(row => row.AutomationId)
            .HasConversion(AutomationValueConverters.Id<AutomationId>())
            .IsRequired();

        builder.Property(row => row.Key)
            .HasMaxLength(AutomationPersistenceConstants.KeyLength)
            .IsRequired();

        builder.Property(row => row.Parameters)
            .HasMaxLength(AutomationPersistenceConstants.ParameterLength)
            .IsRequired();

        builder.HasIndex(row => row.AutomationId);

        builder.Ignore(row => row.DomainEvents);
    }
}

internal sealed class AutomationAuditRecordConfiguration
    : IEntityTypeConfiguration<AutomationAuditRecord>
{
    public void Configure(EntityTypeBuilder<AutomationAuditRecord> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(AutomationPersistenceConstants.AuditTable, AutomationModuleMetadata.Schema);

        builder.HasKey(row => row.Id);

        builder.Property(row => row.Id)
            .HasConversion(AutomationValueConverters.Id<AutomationAuditId>())
            .ValueGeneratedNever();

        builder.Property(row => row.AutomationId)
            .HasConversion(AutomationValueConverters.Id<AutomationId>())
            .IsRequired();

        builder.Property(row => row.Action)
            .HasConversion<string>()
            .HasMaxLength(AutomationPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(row => row.ActorUserId)
            .HasConversion(AutomationValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(row => row.ActorLabel)
            .HasMaxLength(AutomationPersistenceConstants.NameLength)
            .IsRequired();

        builder.Property(row => row.Detail)
            .HasMaxLength(AutomationPersistenceConstants.SummaryLength);

        builder.HasIndex(row => new { row.AutomationId, row.At });

        builder.Ignore(row => row.DomainEvents);
    }
}
