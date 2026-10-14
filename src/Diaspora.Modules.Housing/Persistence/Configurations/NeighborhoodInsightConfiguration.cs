using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Neighborhoods;
using Diaspora.Modules.Housing.Domain.Policies;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Housing.Persistence.Configurations;

/// <summary>
/// Maps community insights.
/// <para>
/// <b>The index on (area, status) is the one that keeps unapproved insights invisible cheaply.</b>
/// Every neighbourhood page filters on it, and it is the index that makes "approved insights for
/// this place" a seek rather than a scan of everything anybody has ever said about anywhere.
/// </para>
/// <para>
/// <b>The fingerprint column is stored rather than computed.</b> Duplicate detection compares
/// normalised text, and a query that normalised in SQL could not use an index — which would make
/// the duplicate check a table scan on the one path a member hits repeatedly when their first
/// submission has not appeared because it is waiting for a moderator.
/// </para>
/// </summary>
public sealed class NeighborhoodInsightConfiguration
    : IEntityTypeConfiguration<NeighborhoodInsight>
{
    public void Configure(EntityTypeBuilder<NeighborhoodInsight> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            HousingPersistenceConstants.InsightsTable, HousingModuleMetadata.Schema);

        builder.HasKey(insight => insight.Id);

        builder.Property(insight => insight.Id)
            .HasConversion(HousingValueConverters.Id<NeighborhoodInsightId>());

        // Nullable since 14 Oct 2026: an insight written at a point belongs to no catalogue
        // neighbourhood and so to no profile. Every insight written before then keeps both.
        builder.Property(insight => insight.ProfileId)
            .HasConversion(HousingValueConverters.Id<NeighborhoodProfileId>());

        builder.Property(insight => insight.AreaId);

        // The point an insight is about — the area around a point (14 Oct 2026).
        builder.Property(insight => insight.Latitude).HasColumnName("latitude");

        builder.Property(insight => insight.Longitude).HasColumnName("longitude");

        builder.Property(insight => insight.PlaceName)
            .HasColumnName("place_name")
            .HasMaxLength(HousingLimits.MaxInsightPlaceName);

        builder.Property(insight => insight.PlaceId)
            .HasColumnName("place_id")
            .HasMaxLength(HousingLimits.MaxInsightPlaceId);

        builder.Property(insight => insight.CountryCode)
            .HasColumnName("country_code")
            .HasMaxLength(2);

        builder.Property(insight => insight.AuthorId)
            .HasConversion(HousingValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(insight => insight.Category)
            .HasConversion<string>()
            .HasMaxLength(HousingPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(insight => insight.Status)
            .HasConversion<string>()
            .HasMaxLength(HousingPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(insight => insight.Body)
            .HasMaxLength(HousingLimits.MaxInsightBody)
            .IsRequired();

        builder.Property(insight => insight.Label).HasMaxLength(HousingLimits.MaxInsightLabel);

        builder.Property(insight => insight.DecisionNote)
            .HasMaxLength(HousingLimits.MaxInsightDecisionNote);

        builder.Property(insight => insight.DecidedBy)
            .HasConversion(HousingValueConverters.Id<UserId>());

        // Stored rather than computed. See the class summary.
        builder.Property(insight => insight.Fingerprint)
            .HasColumnName("fingerprint")
            .HasMaxLength(HousingLimits.MaxInsightBody)
            .IsRequired();

        // The neighbourhood page's filter, and the moderation queue's.
        builder.HasIndex(insight => new { insight.AreaId, insight.Status })
            .HasDatabaseName("ix_housing_insights_area_status");

        builder.HasIndex(insight => new { insight.ProfileId, insight.Status })
            .HasDatabaseName("ix_housing_insights_profile_status");

        // Duplicate detection, and an author reading their own submissions.
        builder.HasIndex(insight =>
                new { insight.AuthorId, insight.AreaId, insight.Fingerprint })
            .HasDatabaseName("ix_housing_insights_author_fingerprint");

        // The area page's filter: a range over latitude, then longitude, inside the rectangle around
        // the circle. The exact circle is applied over the few rows the range returns.
        builder.HasIndex(insight => new { insight.Latitude, insight.Longitude })
            .HasDatabaseName("ix_housing_insights_point");

        // The profile's ordering: most attested first.
        builder.HasIndex(insight => insight.EndorsementCount)
            .HasDatabaseName("ix_housing_insights_endorsements");

        builder.Ignore(insight => insight.DomainEvents);
        builder.Ignore(insight => insight.IsPublished);
        builder.Ignore(insight => insight.IsAwaitingReview);
    }
}
