using Diaspora.Modules.Automation.Domain.Identifiers;
using Diaspora.Modules.Automation.Domain.Runs;
using Diaspora.Modules.Automation.Domain.SavedSearches;
using Diaspora.Modules.Automation.Domain.Scheduling;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Automation.Persistence.Configurations;

/// <summary>
/// Maps execution history.
/// <para>
/// <b>The unique index on <c>(AutomationId, TriggerEventId)</c> is the idempotency barrier, and it is
/// the barrier rather than the read that precedes it.</b> Delivery is at-least-once and two instances
/// consuming the same redelivered message would both pass a read-then-write; only the database can
/// refuse the second insert. The read exists so the common case answers without provoking a
/// constraint violation, not instead of the constraint.
/// </para>
/// </summary>
internal sealed class AutomationRunConfiguration : IEntityTypeConfiguration<AutomationRun>
{
    public void Configure(EntityTypeBuilder<AutomationRun> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(AutomationPersistenceConstants.RunsTable, AutomationModuleMetadata.Schema);

        builder.HasKey(row => row.Id);

        builder.Property(row => row.Id)
            .HasConversion(AutomationValueConverters.Id<AutomationRunId>())
            .ValueGeneratedNever();

        builder.Property(row => row.AutomationId)
            .HasConversion(AutomationValueConverters.Id<AutomationId>())
            .IsRequired();

        builder.Property(row => row.AutomationName)
            .HasMaxLength(AutomationPersistenceConstants.NameLength)
            .IsRequired();

        builder.Property(row => row.OwnerId).IsRequired();

        builder.Property(row => row.OwnerKind)
            .HasConversion<string>()
            .HasMaxLength(AutomationPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(row => row.Status)
            .HasConversion<string>()
            .HasMaxLength(AutomationPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(row => row.Reason)
            .HasConversion<string>()
            .HasMaxLength(AutomationPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(row => row.TriggerEventId).HasMaxLength(128).IsRequired();

        builder.Property(row => row.TriggerKey)
            .HasMaxLength(AutomationPersistenceConstants.KeyLength)
            .IsRequired();

        builder.Property(row => row.TriggerSummary)
            .HasMaxLength(AutomationPersistenceConstants.SummaryLength)
            .IsRequired();

        builder.Property(row => row.FailureKind)
            .HasConversion<string>()
            .HasMaxLength(AutomationPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(row => row.FailureMessage)
            .HasMaxLength(AutomationPersistenceConstants.SummaryLength);

        builder.Property(row => row.RecoveryHint)
            .HasMaxLength(AutomationPersistenceConstants.SummaryLength);

        builder.Property(row => row.CorrelationId).HasMaxLength(32).IsRequired();

        builder.Property(row => row.ChainId).HasMaxLength(64).IsRequired();

        builder.HasMany(row => row.ConditionOutcomes)
            .WithOne()
            .HasForeignKey(child => child.RunId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(row => row.ConditionOutcomes)
            .HasField("_conditionOutcomes")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(row => row.ActionOutcomes)
            .WithOne()
            .HasForeignKey(child => child.RunId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(row => row.ActionOutcomes)
            .HasField("_actionOutcomes")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // The idempotency barrier.
        builder.HasIndex(row => new { row.AutomationId, row.TriggerEventId }).IsUnique();

        // Execution history, cursor-paged. Owner first because it is the filter every read applies
        // and the one §66 requires — a member must not be able to read another member's runs by
        // supplying a run id, and the route takes no owner parameter for that reason.
        builder.HasIndex(row => new { row.OwnerId, row.StartedAt });

        builder.HasIndex(row => new { row.AutomationId, row.StartedAt });

        // The sweeper's stale-run scan, and the rate-ceiling counts.
        builder.HasIndex(row => new { row.Status, row.StartedAt });

        builder.Ignore(row => row.DomainEvents);
    }
}

internal sealed class AutomationRunConditionOutcomeConfiguration
    : IEntityTypeConfiguration<AutomationRunConditionOutcome>
{
    public void Configure(EntityTypeBuilder<AutomationRunConditionOutcome> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            AutomationPersistenceConstants.ConditionOutcomesTable, AutomationModuleMetadata.Schema);

        builder.HasKey(row => row.Id);

        builder.Property(row => row.Id).ValueGeneratedNever();

        builder.Property(row => row.RunId)
            .HasConversion(AutomationValueConverters.Id<AutomationRunId>())
            .IsRequired();

        builder.Property(row => row.ConditionId)
            .HasConversion(AutomationValueConverters.Id<AutomationConditionId>())
            .IsRequired();

        builder.Property(row => row.Label)
            .HasMaxLength(AutomationPersistenceConstants.NameLength)
            .IsRequired();

        builder.Property(row => row.Explanation)
            .HasMaxLength(AutomationPersistenceConstants.SummaryLength)
            .IsRequired();

        builder.HasIndex(row => row.RunId);

        builder.Ignore(row => row.DomainEvents);
    }
}

internal sealed class AutomationRunActionOutcomeConfiguration
    : IEntityTypeConfiguration<AutomationRunActionOutcome>
{
    public void Configure(EntityTypeBuilder<AutomationRunActionOutcome> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            AutomationPersistenceConstants.ActionOutcomesTable, AutomationModuleMetadata.Schema);

        builder.HasKey(row => row.Id);

        builder.Property(row => row.Id).ValueGeneratedNever();

        builder.Property(row => row.RunId)
            .HasConversion(AutomationValueConverters.Id<AutomationRunId>())
            .IsRequired();

        builder.Property(row => row.ActionId)
            .HasConversion(AutomationValueConverters.Id<AutomationActionId>())
            .IsRequired();

        builder.Property(row => row.ActionKey)
            .HasMaxLength(AutomationPersistenceConstants.KeyLength)
            .IsRequired();

        builder.Property(row => row.Label)
            .HasMaxLength(AutomationPersistenceConstants.NameLength)
            .IsRequired();

        builder.Property(row => row.Status)
            .HasConversion<string>()
            .HasMaxLength(AutomationPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(row => row.SuppressionReason)
            .HasMaxLength(AutomationPersistenceConstants.SummaryLength);

        builder.Property(row => row.FailureKind)
            .HasConversion<string>()
            .HasMaxLength(AutomationPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(row => row.Message)
            .HasMaxLength(AutomationPersistenceConstants.SummaryLength);

        builder.Property(row => row.ResultKind).HasMaxLength(32);

        builder.Property(row => row.ResultId).HasMaxLength(64);

        builder.Property(row => row.DeepLink).HasMaxLength(300);

        builder.HasIndex(row => row.RunId);

        // The settlement receipt's lookup: a notification's answer names a run and an action.
        builder.HasIndex(row => new { row.RunId, row.ActionId });

        builder.Ignore(row => row.DomainEvents);
    }
}

/// <summary>
/// Maps the durable due-store.
/// <para>
/// <b>The unique index on <c>(AutomationId, DueAt)</c> is what stops one moment firing twice.</b> A
/// restart mid-dispatch, a clock adjustment or two instances computing the same next occurrence would
/// otherwise each produce a row and each produce a notification.
/// </para>
/// </summary>
internal sealed class AutomationOccurrenceConfiguration : IEntityTypeConfiguration<AutomationOccurrence>
{
    public void Configure(EntityTypeBuilder<AutomationOccurrence> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            AutomationPersistenceConstants.OccurrencesTable, AutomationModuleMetadata.Schema);

        builder.HasKey(row => row.Id);

        builder.Property(row => row.Id)
            .HasConversion(AutomationValueConverters.Id<AutomationOccurrenceId>())
            .ValueGeneratedNever();

        builder.Property(row => row.AutomationId)
            .HasConversion(AutomationValueConverters.Id<AutomationId>())
            .IsRequired();

        builder.Property(row => row.OwnerId).IsRequired();

        builder.Property(row => row.TimeZoneId)
            .HasMaxLength(AutomationPersistenceConstants.TimeZoneLength)
            .IsRequired();

        builder.Property(row => row.LocalWallClock).HasMaxLength(32).IsRequired();

        builder.Property(row => row.Status)
            .HasConversion<string>()
            .HasMaxLength(AutomationPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(row => row.ClaimedBy).HasMaxLength(128);

        builder.Property(row => row.LastError)
            .HasMaxLength(AutomationPersistenceConstants.SummaryLength);

        builder.Property(row => row.Resolution).HasMaxLength(200);

        builder.Property(row => row.Revision).IsConcurrencyToken().IsRequired();

        builder.HasIndex(row => new { row.AutomationId, row.DueAt }).IsUnique();

        // The sweep. Status first so a pending scan does not walk completed history.
        builder.HasIndex(row => new { row.Status, row.DueAt });

        builder.Ignore(row => row.DomainEvents);
    }
}

/// <summary>
/// Maps the dated-subject store — Wave 12.
/// <para>
/// <b>The unique index is on <c>(TriggerKey, SubjectId)</c> and it is what makes a redelivered
/// publication an update rather than a second notification.</b> A republished listing, a re-scheduled
/// viewing and a redelivered outbox message all name the same subject under the same trigger; without
/// the constraint each would write a row and each would fire.
/// </para>
/// </summary>
internal sealed class AutomationSubjectHorizonConfiguration
    : IEntityTypeConfiguration<AutomationSubjectHorizon>
{
    public void Configure(EntityTypeBuilder<AutomationSubjectHorizon> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            AutomationPersistenceConstants.SubjectHorizonsTable, AutomationModuleMetadata.Schema);

        builder.HasKey(row => row.Id);

        builder.Property(row => row.Id)
            .HasConversion(AutomationValueConverters.Id<AutomationSubjectHorizonId>())
            .ValueGeneratedNever();

        builder.Property(row => row.TriggerKey)
            .HasMaxLength(AutomationPersistenceConstants.KeyLength)
            .IsRequired();

        builder.Property(row => row.SubjectKind).HasMaxLength(32).IsRequired();

        builder.Property(row => row.SubjectId).IsRequired();

        builder.Property(row => row.SubjectTitle).HasMaxLength(200).IsRequired();

        builder.Property(row => row.TimeZoneId)
            .HasMaxLength(AutomationPersistenceConstants.TimeZoneLength)
            .IsRequired();

        builder.Property(row => row.ConcernedOwnerIds)
            .HasMaxLength(AutomationPersistenceConstants.IdListLength)
            .IsRequired();

        builder.Property(row => row.Values)
            .HasMaxLength(AutomationPersistenceConstants.ParameterLength)
            .IsRequired();

        builder.Property(row => row.Summary)
            .HasMaxLength(AutomationPersistenceConstants.SummaryLength)
            .IsRequired();

        builder.Property(row => row.Status)
            .HasConversion<string>()
            .HasMaxLength(AutomationPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(row => row.ClaimedBy).HasMaxLength(128);

        builder.Property(row => row.LastError)
            .HasMaxLength(AutomationPersistenceConstants.SummaryLength);

        builder.Property(row => row.Resolution).HasMaxLength(200);

        builder.Property(row => row.Revision).IsConcurrencyToken().IsRequired();

        // One horizon per subject per trigger. The barrier, not a read.
        builder.HasIndex(row => new { row.TriggerKey, row.SubjectId }).IsUnique();

        // The sweep. Status first so a pending scan does not walk settled history.
        builder.HasIndex(row => new { row.Status, row.DueAt });

        // The cancellation path: a subject that went away cancels every horizon on it.
        builder.HasIndex(row => new { row.SubjectId, row.Status });

        // Retention.
        builder.HasIndex(row => row.CompletedAt);

        // `DeduplicationKey` is derived from two mapped columns and is never stored: a persisted copy
        // of a value the unique index already enforces is a second place for it to be wrong.
        builder.Ignore(row => row.DeduplicationKey);

        builder.Ignore(row => row.DomainEvents);
    }
}

/// <summary>Maps reminders, and their mandatory de-duplication key.</summary>
internal sealed class AutomationReminderConfiguration : IEntityTypeConfiguration<AutomationReminder>
{
    public void Configure(EntityTypeBuilder<AutomationReminder> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(AutomationPersistenceConstants.RemindersTable, AutomationModuleMetadata.Schema);

        builder.HasKey(row => row.Id);

        builder.Property(row => row.Id)
            .HasConversion(AutomationValueConverters.Id<AutomationReminderId>())
            .ValueGeneratedNever();

        builder.Property(row => row.MemberId)
            .HasConversion(AutomationValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(row => row.SubjectKind).HasMaxLength(32).IsRequired();

        builder.Property(row => row.SubjectId).IsRequired();

        builder.Property(row => row.SubjectTitle).HasMaxLength(200).IsRequired();

        builder.Property(row => row.Anchor).HasMaxLength(8).IsRequired();

        builder.Property(row => row.TimeZoneId)
            .HasMaxLength(AutomationPersistenceConstants.TimeZoneLength)
            .IsRequired();

        builder.Property(row => row.State)
            .HasConversion<string>()
            .HasMaxLength(AutomationPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(row => row.FailureReason)
            .HasMaxLength(AutomationPersistenceConstants.SummaryLength);

        builder.Property(row => row.DeduplicationKey).HasMaxLength(200).IsRequired();

        builder.Property(row => row.Revision).IsConcurrencyToken().IsRequired();

        // Tapping "remind me" twice must produce one reminder. Unique, so the second insert fails
        // rather than the second push arriving.
        builder.HasIndex(row => row.DeduplicationKey).IsUnique();

        builder.HasIndex(row => new { row.State, row.ScheduledFor });

        // The re-schedule path: every pending reminder on a subject that moved.
        builder.HasIndex(row => new { row.SubjectId, row.State });

        builder.HasIndex(row => new { row.MemberId, row.SubjectKind, row.SubjectId });

        builder.Ignore(row => row.DomainEvents);
    }
}

/// <summary>Maps saved searches and the once-ever match barrier.</summary>
internal sealed class SavedSearchConfiguration : IEntityTypeConfiguration<SavedSearch>
{
    public void Configure(EntityTypeBuilder<SavedSearch> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            AutomationPersistenceConstants.SavedSearchesTable, AutomationModuleMetadata.Schema);

        builder.HasKey(row => row.Id);

        builder.Property(row => row.Id)
            .HasConversion(AutomationValueConverters.Id<SavedSearchId>())
            .ValueGeneratedNever();

        builder.Property(row => row.MemberId)
            .HasConversion(AutomationValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(row => row.Subject)
            .HasConversion<string>()
            .HasMaxLength(AutomationPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(row => row.Name)
            .HasMaxLength(AutomationPersistenceConstants.NameLength)
            .IsRequired();

        builder.Property(row => row.Criteria)
            .HasMaxLength(AutomationPersistenceConstants.ParameterLength)
            .IsRequired();

        builder.Property(row => row.CriteriaFingerprint).HasMaxLength(64).IsRequired();

        builder.Property(row => row.QuerySummary).HasMaxLength(200).IsRequired();

        builder.Property(row => row.AlertFrequency)
            .HasConversion<string>()
            .HasMaxLength(AutomationPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(row => row.WatchFor).HasMaxLength(200).IsRequired();

        builder.Property(row => row.AutomationId)
            .HasConversion(AutomationValueConverters.Id<AutomationId>());

        builder.Property(row => row.Revision).IsConcurrencyToken().IsRequired();

        // The creation idempotency: saving the same search twice returns the existing one rather than
        // creating a duplicate that would double every alert.
        builder.HasIndex(row => new { row.MemberId, row.Subject, row.CriteriaFingerprint }).IsUnique();

        // The matcher's candidate list.
        builder.HasIndex(row => new { row.Subject, row.AlertsEnabled });

        builder.Ignore(row => row.DomainEvents);
    }
}

internal sealed class SavedSearchMatchConfiguration : IEntityTypeConfiguration<SavedSearchMatch>
{
    public void Configure(EntityTypeBuilder<SavedSearchMatch> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            AutomationPersistenceConstants.SavedSearchMatchesTable, AutomationModuleMetadata.Schema);

        builder.HasKey(row => row.Id);

        builder.Property(row => row.Id).ValueGeneratedNever();

        builder.Property(row => row.SavedSearchId)
            .HasConversion(AutomationValueConverters.Id<SavedSearchId>())
            .IsRequired();

        builder.Property(row => row.MatchedItemId).IsRequired();

        builder.Property(row => row.Change).HasMaxLength(32).IsRequired();

        // "Once, ever." A member must be told about a listing once even if it is republished, edited
        // or re-indexed. A de-duplication window would not do it — a listing republished a month later
        // would notify again — so the barrier is a unique row and the insert is what fails.
        builder.HasIndex(row => new { row.SavedSearchId, row.MatchedItemId }).IsUnique();

        builder.HasIndex(row => new { row.SavedSearchId, row.Notified });

        builder.Ignore(row => row.DomainEvents);
    }
}
