using Diaspora.Modules.Trust.Domain;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Trust.Persistence.Configurations;

/// <summary>Maps the <see cref="Appeal"/> aggregate.</summary>
public sealed class AppealConfiguration : IEntityTypeConfiguration<Appeal>
{
    public void Configure(EntityTypeBuilder<Appeal> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("appeals", TrustModuleMetadata.Schema);

        builder.HasKey(appeal => appeal.Id);

        builder.Property(appeal => appeal.Id)
            .HasConversion(new StronglyTypedIdValueConverter<AppealId>());

        builder.Property(appeal => appeal.UserId).IsRequired();

        builder.Property(appeal => appeal.Type)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(appeal => appeal.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(appeal => appeal.TargetId)
            .HasMaxLength(Appeal.MaxTargetIdLength)
            .IsRequired();

        builder.Property(appeal => appeal.Reason)
            .HasMaxLength(Appeal.MaxReasonLength)
            .IsRequired();

        builder.Property(appeal => appeal.Explanation)
            .HasMaxLength(Appeal.MaxExplanationLength);

        builder.Property(appeal => appeal.OriginalDeciderId);
        builder.Property(appeal => appeal.AssignedReviewerId);

        builder.Property(appeal => appeal.SubmittedAt).IsRequired();

        builder.Property<List<string>>("_evidenceKeys")
            .HasField("_evidenceKeys")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasColumnName("evidence_keys")
            .HasConversion(
                keys => string.Join(TrustPersistenceConstants.ListSeparator, keys),
                value => value
                    .Split(TrustPersistenceConstants.ListSeparator, StringSplitOptions.RemoveEmptyEntries)
                    .ToList(),
                new ValueComparer<List<string>>(
                    (left, right) => left != null && right != null && left.SequenceEqual(right),
                    keys => keys.Aggregate(0, (hash, key) => HashCode.Combine(hash, key.GetHashCode())),
                    keys => keys.ToList()))
            .HasMaxLength((VerificationDocument.MaxStorageKeyLength + 1) * Appeal.MaxEvidenceItems);

        builder.Ignore(appeal => appeal.EvidenceKeys);
        builder.Ignore(appeal => appeal.IsOpen);

        builder.HasMany(appeal => appeal.Reviews)
            .WithOne()
            .HasForeignKey(review => review.AppealId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(appeal => appeal.Reviews)
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasField("_reviews");

        // Backs the one-open-appeal-per-target check on submit.
        builder.HasIndex(appeal => new { appeal.UserId, appeal.TargetId, appeal.Status })
            .HasDatabaseName("ix_appeals_user_target_status");

        // The staff queue.
        builder.HasIndex(appeal => new { appeal.Status, appeal.ReviewDueAt })
            .HasDatabaseName("ix_appeals_status_due");

        builder.Ignore(appeal => appeal.DomainEvents);
    }
}

public sealed class AppealReviewConfiguration : IEntityTypeConfiguration<AppealReview>
{
    public void Configure(EntityTypeBuilder<AppealReview> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("appeal_reviews", TrustModuleMetadata.Schema);

        builder.HasKey(review => review.Id);

        builder.Property(review => review.Id)
            .HasConversion(new StronglyTypedIdValueConverter<AppealReviewId>());

        builder.Property(review => review.AppealId)
            .HasConversion(new StronglyTypedIdValueConverter<AppealId>())
            .IsRequired();

        builder.Property(review => review.ReviewerId).IsRequired();

        builder.Property(review => review.FromStatus)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(review => review.ToStatus)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(review => review.Notes)
            .HasMaxLength(AppealReview.MaxNotesLength);

        builder.Property(review => review.ReviewedAt).IsRequired();

        builder.HasIndex(review => review.AppealId)
            .HasDatabaseName("ix_appeal_reviews_appeal");

        builder.Ignore(review => review.DomainEvents);
    }
}

/// <summary>Maps the <see cref="TrustEvent"/> timeline.</summary>
public sealed class TrustEventConfiguration : IEntityTypeConfiguration<TrustEvent>
{
    public void Configure(EntityTypeBuilder<TrustEvent> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("trust_events", TrustModuleMetadata.Schema);

        builder.HasKey(trustEvent => trustEvent.Id);

        builder.Property(trustEvent => trustEvent.Id)
            .HasConversion(new StronglyTypedIdValueConverter<TrustEventId>());

        builder.Property(trustEvent => trustEvent.UserId).IsRequired();

        builder.Property(trustEvent => trustEvent.Type)
            .HasMaxLength(TrustEvent.MaxTypeLength)
            .IsRequired();

        builder.Property(trustEvent => trustEvent.Label)
            .HasMaxLength(TrustEvent.MaxLabelLength)
            .IsRequired();

        builder.Property(trustEvent => trustEvent.Description)
            .HasMaxLength(TrustEvent.MaxDescriptionLength);

        builder.Property(trustEvent => trustEvent.Icon)
            .HasMaxLength(TrustEvent.MaxIconLength);

        builder.Property(trustEvent => trustEvent.CountryCode).HasMaxLength(2);

        builder.Property(trustEvent => trustEvent.OccurredAt).IsRequired();

        // The timeline read: one member's entries, newest first, filtered by visibility.
        builder.HasIndex(trustEvent => new
        {
            trustEvent.UserId,
            trustEvent.IsMemberVisible,
            trustEvent.OccurredAt
        })
            .HasDatabaseName("ix_trust_events_user_visible_occurred");

        builder.Ignore(trustEvent => trustEvent.DomainEvents);
    }
}

/// <summary>Maps the <see cref="ModerationItem"/> aggregate.</summary>
public sealed class ModerationItemConfiguration : IEntityTypeConfiguration<ModerationItem>
{
    public void Configure(EntityTypeBuilder<ModerationItem> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("moderation_items", TrustModuleMetadata.Schema);

        builder.HasKey(item => item.Id);

        builder.Property(item => item.Id)
            .HasConversion(new StronglyTypedIdValueConverter<ModerationItemId>());

        builder.Property(item => item.ContentType)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(item => item.ContentId)
            .HasMaxLength(ModerationItem.MaxContentIdLength)
            .IsRequired();

        builder.Property(item => item.Title)
            .HasMaxLength(ModerationItem.MaxTitleLength);

        builder.Property(item => item.AuthorId).IsRequired();

        builder.Property(item => item.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(item => item.Priority)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(item => item.AiConfidence).HasPrecision(4, 3);

        builder.Property(item => item.AssignedModeratorId);

        builder.Property<List<string>>("_aiFlags")
            .HasField("_aiFlags")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasColumnName("ai_flags")
            .HasConversion(
                flags => string.Join(TrustPersistenceConstants.ListSeparator, flags),
                value => value
                    .Split(TrustPersistenceConstants.ListSeparator, StringSplitOptions.RemoveEmptyEntries)
                    .ToList(),
                new ValueComparer<List<string>>(
                    (left, right) => left != null && right != null && left.SequenceEqual(right),
                    flags => flags.Aggregate(0, (hash, flag) => HashCode.Combine(hash, flag.GetHashCode())),
                    flags => flags.ToList()))
            .HasMaxLength((ModerationItem.MaxFlagLength + 1) * ModerationItem.MaxFlags);

        builder.Ignore(item => item.AiFlags);
        builder.Ignore(item => item.IsOpen);

        builder.HasMany(item => item.Decisions)
            .WithOne()
            .HasForeignKey(decision => decision.ItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(item => item.Decisions)
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasField("_decisions");

        // One open item per piece of content — reporting the same guide twice adds a report
        // to the existing item rather than queueing it again.
        builder.HasIndex(item => new { item.ContentType, item.ContentId, item.Status })
            .HasDatabaseName("ix_moderation_items_content_status");

        // The queue read: priority first, then age.
        builder.HasIndex(item => new { item.Status, item.Priority, item.CreatedAt })
            .HasDatabaseName("ix_moderation_items_status_priority_created");

        builder.Ignore(item => item.DomainEvents);
    }
}

public sealed class ModerationDecisionConfiguration : IEntityTypeConfiguration<ModerationDecision>
{
    public void Configure(EntityTypeBuilder<ModerationDecision> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("moderation_decisions", TrustModuleMetadata.Schema);

        builder.HasKey(decision => decision.Id);

        builder.Property(decision => decision.Id)
            .HasConversion(new StronglyTypedIdValueConverter<ModerationDecisionId>());

        builder.Property(decision => decision.ItemId)
            .HasConversion(new StronglyTypedIdValueConverter<ModerationItemId>())
            .IsRequired();

        builder.Property(decision => decision.ModeratorId).IsRequired();

        builder.Property(decision => decision.Action)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(decision => decision.Reason)
            .HasMaxLength(ModerationDecision.MaxReasonLength);

        builder.Property(decision => decision.PreviousStatus)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(decision => decision.NewStatus)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(decision => decision.DecidedAt).IsRequired();

        builder.HasIndex(decision => decision.ItemId)
            .HasDatabaseName("ix_moderation_decisions_item");

        builder.Ignore(decision => decision.DomainEvents);
    }
}

/// <summary>Maps the <see cref="CommunityContribution"/> aggregate.</summary>
public sealed class CommunityContributionConfiguration
    : IEntityTypeConfiguration<CommunityContribution>
{
    public void Configure(EntityTypeBuilder<CommunityContribution> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("community_contributions", TrustModuleMetadata.Schema);

        builder.HasKey(contribution => contribution.Id);

        builder.Property(contribution => contribution.Id)
            .HasConversion(new StronglyTypedIdValueConverter<CommunityContributionId>());

        builder.Property(contribution => contribution.ConfirmedByUserId).IsRequired();
        builder.Property(contribution => contribution.AuthorUserId).IsRequired();

        builder.Property(contribution => contribution.TargetType)
            .HasMaxLength(CommunityContribution.MaxTargetTypeLength)
            .IsRequired();

        builder.Property(contribution => contribution.TargetId)
            .HasMaxLength(CommunityContribution.MaxTargetIdLength)
            .IsRequired();

        builder.Property(contribution => contribution.CountryCode).HasMaxLength(2);

        builder.Property(contribution => contribution.Note)
            .HasMaxLength(CommunityContribution.MaxNoteLength);

        builder.Property(contribution => contribution.ConfirmedAt).IsRequired();

        // One confirmation per member per target. The unique index is what makes the signal
        // worth counting — without it, confirming the same guide fifty times is a valid way
        // to hand someone 250 trust points.
        builder.HasIndex(contribution => new
        {
            contribution.ConfirmedByUserId,
            contribution.TargetType,
            contribution.TargetId
        })
            .IsUnique()
            .HasDatabaseName("ix_community_contributions_confirmer_target");

        builder.HasIndex(contribution => new { contribution.TargetType, contribution.TargetId })
            .HasDatabaseName("ix_community_contributions_target");

        builder.Ignore(contribution => contribution.DomainEvents);
    }
}
