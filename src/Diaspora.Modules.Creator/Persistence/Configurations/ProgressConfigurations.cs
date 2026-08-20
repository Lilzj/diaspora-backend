using Diaspora.Modules.Creator.Domain.Activity;
using Diaspora.Modules.Creator.Domain.Challenges;
using Diaspora.Modules.Creator.Domain.Identifiers;
using Diaspora.Modules.Creator.Domain.Milestones;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Creator.Persistence.Configurations;

/// <summary>Maps the milestone catalogue and the rules that make one weighted and capped.</summary>
public sealed class CreatorMilestoneConfiguration
    : IEntityTypeConfiguration<CreatorMilestoneDefinition>
{
    public void Configure(EntityTypeBuilder<CreatorMilestoneDefinition> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(CreatorPersistenceConstants.MilestonesTable, CreatorModuleMetadata.Schema);

        builder.HasKey(milestone => milestone.Id);

        builder.Property(milestone => milestone.Id)
            .HasConversion(CreatorValueConverters.Id<CreatorMilestoneId>())
            .ValueGeneratedNever();

        builder.Property(milestone => milestone.Key)
            .HasMaxLength(CreatorPersistenceConstants.KeyLength)
            .IsRequired();

        builder.Property(milestone => milestone.Title)
            .HasMaxLength(CreatorPersistenceConstants.LabelLength)
            .IsRequired();

        builder.Property(milestone => milestone.Description)
            .HasMaxLength(CreatorPersistenceConstants.SummaryLength)
            .IsRequired();

        builder.Property(milestone => milestone.Icon)
            .HasMaxLength(CreatorPersistenceConstants.CodeLength);

        builder.Property(milestone => milestone.Category)
            .HasMaxLength(CreatorPersistenceConstants.CodeLength);

        builder.Property(milestone => milestone.Unit)
            .HasMaxLength(CreatorPersistenceConstants.CodeLength);

        builder.Property(milestone => milestone.RewardKind)
            .HasMaxLength(CreatorPersistenceConstants.CodeLength)
            .IsRequired();

        builder.Property(milestone => milestone.RewardLabel)
            .HasMaxLength(CreatorPersistenceConstants.LabelLength)
            .IsRequired();

        builder.Property(milestone => milestone.RewardBadgeKey)
            .HasMaxLength(CreatorPersistenceConstants.KeyLength);

        builder.Property(milestone => milestone.RewardEntitlementKey)
            .HasMaxLength(CreatorPersistenceConstants.KeyLength);

        // The seeder's identity. A re-run updates rather than duplicates.
        builder.HasIndex(milestone => milestone.Key)
            .IsUnique()
            .HasDatabaseName("ux_creator_milestones_key");

        builder.HasIndex(milestone => new { milestone.IsActive, milestone.SortOrder })
            .HasDatabaseName("ix_creator_milestones_catalogue");

        builder.OwnsMany(milestone => milestone.Rules, rule =>
        {
            rule.ToTable(
                CreatorPersistenceConstants.MilestoneRulesTable, CreatorModuleMetadata.Schema);

            rule.HasKey("CreatorMilestoneDefinitionId", nameof(MilestoneRule.ActionType));

            rule.Property(value => value.ActionType)
                .HasMaxLength(CreatorPersistenceConstants.KeyLength)
                .IsRequired();

            // The consumer's own lookup: "which milestones count a published guide".
            rule.HasIndex(value => value.ActionType)
                .HasDatabaseName("ix_creator_milestone_rules_action");
        });

        builder.Ignore(milestone => milestone.DomainEvents);
    }
}

/// <summary>
/// Maps one creator's standing against one milestone.
/// <para>
/// The unique index on (creator, milestone) is the aggregate's identity, and the unique index on the
/// claim key is what decides the race two concurrent claims would otherwise both win.
/// </para>
/// </summary>
public sealed class CreatorMilestoneProgressConfiguration
    : IEntityTypeConfiguration<CreatorMilestoneProgress>
{
    public void Configure(EntityTypeBuilder<CreatorMilestoneProgress> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            CreatorPersistenceConstants.MilestoneProgressTable, CreatorModuleMetadata.Schema);

        builder.HasKey(progress => progress.Id);

        builder.Property(progress => progress.Id)
            .HasConversion(CreatorValueConverters.Id<CreatorMilestoneProgressId>())
            .ValueGeneratedNever();

        builder.Property(progress => progress.CreatorId)
            .HasConversion(CreatorValueConverters.Id<CreatorId>())
            .IsRequired();

        builder.Property(progress => progress.MilestoneId)
            .HasConversion(CreatorValueConverters.Id<CreatorMilestoneId>())
            .IsRequired();

        builder.Property(progress => progress.Revision).IsConcurrencyToken();

        builder.Property(progress => progress.MilestoneKey)
            .HasMaxLength(CreatorPersistenceConstants.KeyLength)
            .IsRequired();

        builder.Property(progress => progress.State)
            .HasConversion<string>()
            .HasMaxLength(CreatorPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(progress => progress.ClaimIdempotencyKey)
            .HasMaxLength(CreatorPersistenceConstants.IdempotencyKeyLength);

        builder.Property(progress => progress.RewardGrantReference)
            .HasMaxLength(CreatorPersistenceConstants.ReferenceLength);

        builder.HasIndex(progress => new { progress.CreatorId, progress.MilestoneId })
            .IsUnique()
            .HasDatabaseName("ux_creator_milestone_progress_creator");

        builder.HasIndex(progress => progress.ClaimIdempotencyKey)
            .IsUnique()
            .HasDatabaseName("ux_creator_milestone_progress_claim_key");

        // "What can I claim" — the dashboard's claimable count.
        builder.HasIndex(progress => new { progress.CreatorId, progress.State })
            .HasDatabaseName("ix_creator_milestone_progress_state");

        builder.OwnsMany(progress => progress.Contributions, contribution =>
        {
            contribution.ToTable(
                CreatorPersistenceConstants.MilestoneProgressRulesTable,
                CreatorModuleMetadata.Schema);

            contribution.HasKey(
                "CreatorMilestoneProgressId", nameof(MilestoneRuleProgress.ActionType));

            contribution.Property(value => value.ActionType)
                .HasMaxLength(CreatorPersistenceConstants.KeyLength)
                .IsRequired();
        });

        builder.Ignore(progress => progress.DomainEvents);
    }
}

/// <summary>Maps the challenge catalogue: its tasks, its rules and where it runs.</summary>
public sealed class CreatorChallengeConfiguration : IEntityTypeConfiguration<CreatorChallenge>
{
    public void Configure(EntityTypeBuilder<CreatorChallenge> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(CreatorPersistenceConstants.ChallengesTable, CreatorModuleMetadata.Schema);

        builder.HasKey(challenge => challenge.Id);

        builder.Property(challenge => challenge.Id)
            .HasConversion(CreatorValueConverters.Id<CreatorChallengeId>())
            .ValueGeneratedNever();

        builder.Property(challenge => challenge.Key)
            .HasMaxLength(CreatorPersistenceConstants.KeyLength)
            .IsRequired();

        builder.Property(challenge => challenge.Title)
            .HasMaxLength(CreatorPersistenceConstants.LabelLength)
            .IsRequired();

        builder.Property(challenge => challenge.Description)
            .HasMaxLength(CreatorPersistenceConstants.DescriptionLength)
            .IsRequired();

        builder.Property(challenge => challenge.Icon)
            .HasMaxLength(CreatorPersistenceConstants.CodeLength);

        builder.Property(challenge => challenge.Category)
            .HasMaxLength(CreatorPersistenceConstants.CodeLength);

        builder.Property(challenge => challenge.RewardKind)
            .HasMaxLength(CreatorPersistenceConstants.CodeLength)
            .IsRequired();

        builder.Property(challenge => challenge.RewardLabel)
            .HasMaxLength(CreatorPersistenceConstants.LabelLength)
            .IsRequired();

        builder.Property(challenge => challenge.RewardBadgeKey)
            .HasMaxLength(CreatorPersistenceConstants.KeyLength);

        builder.Property(challenge => challenge.MinimumStatus)
            .HasConversion<string>()
            .HasMaxLength(CreatorPersistenceConstants.EnumLength)
            .IsRequired();

        builder.HasIndex(challenge => challenge.Key)
            .IsUnique()
            .HasDatabaseName("ux_creator_challenges_key");

        // The discovery page: published challenges, soonest to end first.
        builder.HasIndex(challenge =>
                new { challenge.IsPublished, challenge.IsCancelled, challenge.EndsAt })
            .HasDatabaseName("ix_creator_challenges_discovery");

        builder.OwnsMany(challenge => challenge.Tasks, task =>
        {
            task.ToTable(
                CreatorPersistenceConstants.ChallengeTasksTable, CreatorModuleMetadata.Schema);

            task.HasKey(value => value.TaskId);

            task.Property(value => value.TaskId)
                .HasConversion(CreatorValueConverters.Id<CreatorChallengeTaskId>())
                .ValueGeneratedNever();

            task.Property(value => value.Label)
                .HasMaxLength(CreatorPersistenceConstants.LabelLength)
                .IsRequired();

            task.Property(value => value.Description)
                .HasMaxLength(CreatorPersistenceConstants.SummaryLength);

            task.Property(value => value.ActionType)
                .HasMaxLength(CreatorPersistenceConstants.KeyLength)
                .IsRequired();

            task.Property(value => value.Window)
                .HasConversion<string>()
                .HasMaxLength(CreatorPersistenceConstants.EnumLength)
                .IsRequired();

            task.Property(value => value.ActionRoute)
                .HasMaxLength(CreatorPersistenceConstants.UrlLength);

            task.Property(value => value.ActionLabel)
                .HasMaxLength(CreatorPersistenceConstants.LabelLength);

            // The consumer's own lookup: "which challenges count a published guide".
            task.HasIndex(value => value.ActionType)
                .HasDatabaseName("ix_creator_challenge_tasks_action");
        });

        builder.OwnsMany(challenge => challenge.Rules, rule =>
        {
            rule.ToTable(
                CreatorPersistenceConstants.ChallengeRulesTable, CreatorModuleMetadata.Schema);

            rule.HasKey("CreatorChallengeId", nameof(ChallengeRuleText.SortOrder));

            rule.Property(value => value.SortOrder).ValueGeneratedNever();

            rule.Property(value => value.Text)
                .HasMaxLength(CreatorPersistenceConstants.SummaryLength)
                .IsRequired();
        });

        builder.OwnsMany(challenge => challenge.Countries, country =>
        {
            country.ToTable(
                CreatorPersistenceConstants.ChallengeCountriesTable, CreatorModuleMetadata.Schema);

            country.HasKey("CreatorChallengeId", nameof(ChallengeCountry.CountryCode));

            country.Property(value => value.CountryCode)
                .HasMaxLength(CreatorPersistenceConstants.CountryLength)
                .IsRequired();
        });

        builder.Ignore(challenge => challenge.DomainEvents);
    }
}

/// <summary>Maps one creator's participation, and the two keys that make join and claim idempotent.</summary>
public sealed class CreatorChallengeEnrollmentConfiguration
    : IEntityTypeConfiguration<CreatorChallengeEnrollment>
{
    public void Configure(EntityTypeBuilder<CreatorChallengeEnrollment> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(CreatorPersistenceConstants.EnrollmentsTable, CreatorModuleMetadata.Schema);

        builder.HasKey(enrollment => enrollment.Id);

        builder.Property(enrollment => enrollment.Id)
            .HasConversion(CreatorValueConverters.Id<CreatorChallengeEnrollmentId>())
            .ValueGeneratedNever();

        builder.Property(enrollment => enrollment.ChallengeId)
            .HasConversion(CreatorValueConverters.Id<CreatorChallengeId>())
            .IsRequired();

        builder.Property(enrollment => enrollment.CreatorId)
            .HasConversion(CreatorValueConverters.Id<CreatorId>())
            .IsRequired();

        builder.Property(enrollment => enrollment.Revision).IsConcurrencyToken();

        builder.Property(enrollment => enrollment.State)
            .HasConversion<string>()
            .HasMaxLength(CreatorPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(enrollment => enrollment.JoinIdempotencyKey)
            .HasMaxLength(CreatorPersistenceConstants.IdempotencyKeyLength);

        builder.Property(enrollment => enrollment.ClaimIdempotencyKey)
            .HasMaxLength(CreatorPersistenceConstants.IdempotencyKeyLength);

        builder.Property(enrollment => enrollment.RewardGrantReference)
            .HasMaxLength(CreatorPersistenceConstants.ReferenceLength);

        // One enrolment per creator per challenge. This is what makes a concurrent double-join lose
        // one at the database rather than producing two rows nobody reconciles.
        builder.HasIndex(enrollment => new { enrollment.ChallengeId, enrollment.CreatorId })
            .IsUnique()
            .HasDatabaseName("ux_creator_enrollments_creator");

        builder.HasIndex(enrollment => enrollment.JoinIdempotencyKey)
            .IsUnique()
            .HasDatabaseName("ux_creator_enrollments_join_key");

        builder.HasIndex(enrollment => enrollment.ClaimIdempotencyKey)
            .IsUnique()
            .HasDatabaseName("ux_creator_enrollments_claim_key");

        // The per-challenge leaderboard, ordered in the database.
        builder.HasIndex(enrollment => new { enrollment.ChallengeId, enrollment.Score })
            .HasDatabaseName("ix_creator_enrollments_leaderboard");

        // The member's own list, and the "active challenges" count.
        builder.HasIndex(enrollment => new { enrollment.CreatorId, enrollment.State })
            .HasDatabaseName("ix_creator_enrollments_state");

        builder.OwnsMany(enrollment => enrollment.Tasks, task =>
        {
            task.ToTable(
                CreatorPersistenceConstants.EnrollmentTasksTable, CreatorModuleMetadata.Schema);

            // **The owner's key is part of this one, and leaving it out was a real defect.**
            // A task identifier belongs to the *challenge*, not to the enrolment — every creator who
            // joins carries the same set. Keyed on TaskId alone, the first creator's rows own those
            // identifiers and the second creator's insert violates the primary key. It surfaces as
            // a unique-constraint failure the idempotency guard turns into
            // creator.idempotency.in-flight, several steps from the cause, and only on the *second*
            // participant — so a suite that joined one member per challenge would never see it.
            task.HasKey("CreatorChallengeEnrollmentId", nameof(ChallengeTaskProgress.TaskId));

            task.Property(value => value.TaskId)
                .HasConversion(CreatorValueConverters.Id<CreatorChallengeTaskId>())
                .ValueGeneratedNever();
        });

        builder.Ignore(enrollment => enrollment.DomainEvents);
    }
}

/// <summary>
/// Maps the recognised-activity ledger.
/// <para>
/// <b>Three indexes, and each one is a query the register asks for.</b> The unique
/// (creator, source) index is the at-least-once barrier; (creator, action, occurred) answers a
/// milestone's and a rule's cap; and the leaderboard's own composite is what lets a period ranking
/// be a grouped sum in the database rather than a scan in memory.
/// </para>
/// </summary>
public sealed class CreatorActivityConfiguration : IEntityTypeConfiguration<CreatorActivityEntry>
{
    public void Configure(EntityTypeBuilder<CreatorActivityEntry> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(CreatorPersistenceConstants.ActivityTable, CreatorModuleMetadata.Schema);

        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.Id)
            .HasConversion(CreatorValueConverters.Id<CreatorActivityEntryId>())
            .ValueGeneratedNever();

        builder.Property(entry => entry.CreatorId)
            .HasConversion(CreatorValueConverters.Id<CreatorId>())
            .IsRequired();

        builder.Property(entry => entry.ActionType)
            .HasMaxLength(CreatorPersistenceConstants.KeyLength)
            .IsRequired();

        builder.Property(entry => entry.SubjectType)
            .HasMaxLength(CreatorPersistenceConstants.CodeLength);

        builder.Property(entry => entry.CountryCode)
            .HasMaxLength(CreatorPersistenceConstants.CountryLength);

        builder.Property(entry => entry.SourceReference)
            .HasMaxLength(CreatorPersistenceConstants.ReferenceLength)
            .IsRequired();

        // At-least-once delivery, counted once. Two workers racing one delivery lose one here.
        builder.HasIndex(entry => new { entry.CreatorId, entry.SourceReference })
            .IsUnique()
            .HasDatabaseName("ux_creator_activity_source");

        // A milestone rule's cap, and a point rule's cooldown.
        builder.HasIndex(entry => new { entry.CreatorId, entry.ActionType, entry.OccurredAt })
            .HasDatabaseName("ix_creator_activity_action");

        // The period leaderboard: sum by creator over a window, filtered by country.
        builder.HasIndex(entry => new { entry.OccurredAt, entry.CountryCode })
            .HasDatabaseName("ix_creator_activity_period");

        // The community leaderboard.
        builder.HasIndex(entry => new { entry.CommunityId, entry.OccurredAt })
            .HasDatabaseName("ix_creator_activity_community");

        builder.Ignore(entry => entry.DomainEvents);
    }
}
