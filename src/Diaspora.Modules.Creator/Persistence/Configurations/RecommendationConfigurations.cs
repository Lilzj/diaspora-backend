using Diaspora.Modules.Creator.Domain.Identifiers;
using Diaspora.Modules.Creator.Domain.Recommendations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Creator.Persistence.Configurations;

/// <summary>
/// <c>creator.creator_recommendation_dismissals</c>. One row per member per dismissed key.
/// </summary>
public sealed class CreatorRecommendationDismissalConfiguration
    : IEntityTypeConfiguration<CreatorRecommendationDismissal>
{
    public void Configure(EntityTypeBuilder<CreatorRecommendationDismissal> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            CreatorPersistenceConstants.RecommendationDismissalsTable, CreatorModuleMetadata.Schema);

        builder.HasKey(dismissal => dismissal.Id);

        builder.Property(dismissal => dismissal.Id)
            .HasConversion(CreatorValueConverters.Id<CreatorRecommendationDismissalId>())
            .ValueGeneratedNever();

        builder.Property(dismissal => dismissal.RecommendationId)
            .HasMaxLength(CreatorRecommendationDismissal.MaxRecommendationIdLength)
            .IsRequired();

        // Dismissing twice is one dismissal. The index is the barrier against two taps racing; the
        // handler's existence check only saves the round trip.
        builder.HasIndex(dismissal => new { dismissal.UserId, dismissal.RecommendationId })
            .IsUnique()
            .HasDatabaseName("ux_creator_recommendation_dismissal");
    }
}
