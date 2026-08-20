using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Neighborhoods;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Housing.Persistence.Configurations;

/// <summary>
/// Maps insight endorsements.
/// <para>
/// <b>The unique index is the entire point of the table.</b> An endorsement count a member could
/// raise by pressing a button twice would rank the most persistent contributor above the
/// observation eleven residents actually agree with — and the profile ranks by that number.
/// </para>
/// <para>
/// Not soft-deletable, deliberately. Withdrawing an endorsement is a member saying they no longer
/// agree, and a tombstone row still naming them would be the platform keeping a record of a
/// retracted opinion.
/// </para>
/// </summary>
public sealed class NeighborhoodInsightEndorsementConfiguration
    : IEntityTypeConfiguration<NeighborhoodInsightEndorsement>
{
    public void Configure(EntityTypeBuilder<NeighborhoodInsightEndorsement> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            HousingPersistenceConstants.InsightEndorsementsTable,
            HousingModuleMetadata.Schema);

        builder.HasKey(endorsement => endorsement.Id);

        builder.Property(endorsement => endorsement.Id).ValueGeneratedNever();

        builder.Property(endorsement => endorsement.InsightId)
            .HasConversion(HousingValueConverters.Id<NeighborhoodInsightId>())
            .IsRequired();

        builder.Property(endorsement => endorsement.MemberId)
            .HasConversion(HousingValueConverters.Id<UserId>())
            .IsRequired();

        // One endorsement per member per insight. See the class summary.
        builder.HasIndex(endorsement =>
                new { endorsement.InsightId, endorsement.MemberId })
            .IsUnique()
            .HasDatabaseName("ix_housing_insight_endorsements_unique");

        builder.Ignore(endorsement => endorsement.DomainEvents);
    }
}
