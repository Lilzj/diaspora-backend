using Diaspora.Modules.Recommendations.Domain.Identifiers;
using Diaspora.Modules.Recommendations.Domain.Sets;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Recommendations.Persistence.Configurations;

/// <summary>
/// Maps the generated page, its items, and each item's score breakdown.
/// <para>
/// <b>Two levels of owned collection, which is unusual in this platform and right here.</b> A
/// contribution has no meaning outside its item and an item has none outside its set; three
/// aggregates with foreign keys would let a contribution outlive the recommendation it explains, and
/// an orphaned explanation is worse than no explanation.
/// </para>
/// <para>
/// <b>The explanation is owned by the item, inside the owned collection.</b> Four columns on the
/// item's own row rather than a fifth table — it is read whenever the item is and never queried on
/// its own.
/// </para>
/// <para>
/// <b>Every owned key is declared and marked caller-supplied</b>, at both levels. Nested owned
/// collections are exactly where EF's shadow-key convention is least visible and its failure least
/// legible — an insert that becomes an update reported as a concurrency conflict.
/// </para>
/// </summary>
public sealed class RecommendationSetConfiguration : IEntityTypeConfiguration<RecommendationSet>
{
    public void Configure(EntityTypeBuilder<RecommendationSet> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            RecommendationPersistenceConstants.SetsTable,
            RecommendationsModuleMetadata.Schema);

        builder.HasKey(set => set.Id);

        builder.Property(set => set.Id)
            .HasConversion(RecommendationValueConverters.Id<RecommendationSetId>());

        builder.Property(set => set.UserId)
            .HasConversion(RecommendationValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(set => set.Surface)
            .HasConversion<string>()
            .HasMaxLength(RecommendationPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(set => set.TargetType)
            .HasConversion<string>()
            .HasMaxLength(RecommendationPersistenceConstants.EnumLength);

        builder.Property(set => set.StrategyKeys)
            .HasMaxLength(RecommendationPersistenceConstants.JoinedListLength);

        builder.OwnsMany(set => set.Items, item =>
        {
            item.ToTable(
                RecommendationPersistenceConstants.SetItemsTable,
                RecommendationsModuleMetadata.Schema);

            item.HasKey(value => value.ItemId);
            item.Property(value => value.ItemId).ValueGeneratedNever();

            item.Property(value => value.PrimaryStrategyKey)
                .HasMaxLength(RecommendationPersistenceConstants.KeyLength)
                .IsRequired();

            item.Property(value => value.Score)
                .HasPrecision(
                    RecommendationPersistenceConstants.ScorePrecision,
                    RecommendationPersistenceConstants.ScoreScale);

            item.OwnsOne(value => value.Target, target =>
            {
                target.Property(reference => reference.TargetType)
                    .HasColumnName("target_type")
                    .HasConversion<string>()
                    .HasMaxLength(RecommendationPersistenceConstants.EnumLength)
                    .IsRequired();

                target.Property(reference => reference.TargetId)
                    .HasColumnName("target_id")
                    .IsRequired();
            });

            item.Navigation(value => value.Target).IsRequired();

            item.OwnsOne(value => value.Explanation, explanation =>
            {
                explanation.Property(value => value.ReasonCode)
                    .HasColumnName("reason_code")
                    .HasConversion<string>()
                    .HasMaxLength(RecommendationPersistenceConstants.EnumLength)
                    .IsRequired();

                explanation.Property(value => value.ResourceKey)
                    .HasColumnName("reason_resource_key")
                    .HasMaxLength(RecommendationPersistenceConstants.ResourceKeyLength)
                    .IsRequired();

                explanation.Property(value => value.FallbackText)
                    .HasColumnName("reason_text")
                    .HasMaxLength(RecommendationPersistenceConstants.ReasonLength)
                    .IsRequired();

                explanation.Property(value => value.Confidence)
                    .HasColumnName("confidence")
                    .HasConversion<string>()
                    .HasMaxLength(RecommendationPersistenceConstants.EnumLength)
                    .IsRequired();

                explanation.Property(value => value.SignalKinds)
                    .HasColumnName("signal_kinds")
                    .HasMaxLength(RecommendationPersistenceConstants.JoinedListLength);
            });

            item.Navigation(value => value.Explanation).IsRequired();

            item.OwnsMany(value => value.Contributions, contribution =>
            {
                contribution.ToTable(
                    RecommendationPersistenceConstants.ItemContributionsTable,
                    RecommendationsModuleMetadata.Schema);

                contribution.HasKey(value => value.ContributionId);
                contribution.Property(value => value.ContributionId).ValueGeneratedNever();

                contribution.Property(value => value.StrategyKey)
                    .HasMaxLength(RecommendationPersistenceConstants.KeyLength)
                    .IsRequired();

                contribution.Property(value => value.Kind)
                    .HasConversion<string>()
                    .HasMaxLength(RecommendationPersistenceConstants.EnumLength)
                    .IsRequired();

                contribution.Property(value => value.RawScore)
                    .HasPrecision(
                        RecommendationPersistenceConstants.ScorePrecision,
                        RecommendationPersistenceConstants.ScoreScale);

                contribution.Property(value => value.Weight)
                    .HasPrecision(
                        RecommendationPersistenceConstants.ScorePrecision,
                        RecommendationPersistenceConstants.ScoreScale);

                contribution.Property(value => value.WeightedScore)
                    .HasPrecision(
                        RecommendationPersistenceConstants.ScorePrecision,
                        RecommendationPersistenceConstants.ScoreScale);
            });

            item.HasIndex("RecommendationSetId", "Position")
                .HasDatabaseName("ix_recommendation_items_position");
        });

        // The reuse read: this member, this surface, still fresh.
        builder.HasIndex(set => new { set.UserId, set.Surface, set.ExpiresAt })
            .HasDatabaseName("ix_recommendation_sets_fresh");

        // The analytics rollup's window scan, and the retention sweep's.
        builder.HasIndex(set => set.GeneratedAt)
            .HasDatabaseName("ix_recommendation_sets_generated");

        builder.Ignore(set => set.DomainEvents);
    }
}
