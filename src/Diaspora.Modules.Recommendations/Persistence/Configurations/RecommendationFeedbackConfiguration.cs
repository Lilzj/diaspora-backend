using Diaspora.Modules.Recommendations.Domain.Feedback;
using Diaspora.Modules.Recommendations.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Recommendations.Persistence.Configurations;

/// <summary>
/// Maps the feedback record.
/// <para>
/// <b>The suppression index is the one that matters and it is composite for a reason.</b> Every
/// generation asks "what has this member dismissed that is still suppressed", which is an equality
/// on the member, an equality on the kind and a range on the expiry — so the index is declared in
/// that order and the range column is last, which is the only order a planner can use all three of.
/// </para>
/// <para>
/// <b>No foreign key to the set.</b> A set is swept after thirty days and feedback is kept longer, so
/// a constraint would either block the sweep or cascade away the analytics. The identifier is a soft
/// reference and the aggregate says so — see <c>RecommendationFeedback</c>.
/// </para>
/// </summary>
public sealed class RecommendationFeedbackConfiguration
    : IEntityTypeConfiguration<RecommendationFeedback>
{
    public void Configure(EntityTypeBuilder<RecommendationFeedback> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            RecommendationPersistenceConstants.FeedbackTable,
            RecommendationsModuleMetadata.Schema);

        builder.HasKey(feedback => feedback.Id);

        builder.Property(feedback => feedback.Id)
            .HasConversion(RecommendationValueConverters.Id<RecommendationFeedbackId>());

        builder.Property(feedback => feedback.UserId)
            .HasConversion(RecommendationValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(feedback => feedback.SetId)
            .HasConversion(RecommendationValueConverters.Id<RecommendationSetId>());

        builder.Property(feedback => feedback.Kind)
            .HasConversion<string>()
            .HasMaxLength(RecommendationPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(feedback => feedback.Surface)
            .HasConversion<string>()
            .HasMaxLength(RecommendationPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(feedback => feedback.ReasonCode)
            .HasConversion<string>()
            .HasMaxLength(RecommendationPersistenceConstants.EnumLength);

        builder.Property(feedback => feedback.StrategyKey)
            .HasMaxLength(RecommendationPersistenceConstants.KeyLength);

        builder.Property(feedback => feedback.Score)
            .HasPrecision(
                RecommendationPersistenceConstants.ScorePrecision,
                RecommendationPersistenceConstants.ScoreScale);

        builder.OwnsOne(feedback => feedback.Target, target =>
        {
            target.Property(value => value.TargetType)
                .HasColumnName("target_type")
                .HasConversion<string>()
                .HasMaxLength(RecommendationPersistenceConstants.EnumLength)
                .IsRequired();

            target.Property(value => value.TargetId)
                .HasColumnName("target_id")
                .IsRequired();
        });

        builder.Navigation(feedback => feedback.Target).IsRequired();

        // The suppression read. Column order is deliberate — see the class summary.
        builder.HasIndex(feedback => new
            {
                feedback.UserId,
                feedback.Kind,
                feedback.SuppressedUntil
            })
            .HasDatabaseName("ix_recommendation_feedback_suppression");

        // The idempotency read: has this member already reported this on this target.
        builder.HasIndex(feedback => new { feedback.UserId, feedback.Kind, feedback.OccurredAt })
            .HasDatabaseName("ix_recommendation_feedback_user");

        // The analytics rollup: everything not yet folded, oldest first.
        builder.HasIndex(feedback => new { feedback.RolledUpAt, feedback.OccurredAt })
            .HasDatabaseName("ix_recommendation_feedback_unrolled");

        builder.Ignore(feedback => feedback.DomainEvents);
    }
}
