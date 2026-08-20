using Diaspora.Modules.Guides.Domain.Authors;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Guides.Persistence.Configurations;

/// <summary>
/// Maps the author standing projection and its badges.
/// <para>
/// <b>The unique index on the user is the get-or-create arbiter</b>: two Trust events about the
/// same author racing both try the insert, and the index lets exactly one win. The badges get a
/// surrogate key because their domain identity — (badge, country) — includes a nullable
/// country, and a nullable column cannot sit in a primary key; the aggregate's own idempotency
/// guard is what actually enforces the pair.
/// </para>
/// </summary>
public sealed class GuideAuthorStandingConfiguration : IEntityTypeConfiguration<GuideAuthorStanding>
{
    public void Configure(EntityTypeBuilder<GuideAuthorStanding> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            GuidePersistenceConstants.GuideAuthorStandingsTable, GuidesModuleMetadata.Schema);

        builder.HasKey(standing => standing.Id);

        builder.Property(standing => standing.Id)
            .HasConversion(GuideValueConverters.Id<GuideAuthorStandingId>());

        builder.Property(standing => standing.UserId)
            .HasConversion(GuideValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(standing => standing.TrustLevel)
            .HasMaxLength(GuidePersistenceConstants.TrustLevelLength);

        builder.OwnsMany(standing => standing.Badges, badge =>
        {
            badge.ToTable(
                GuidePersistenceConstants.GuideAuthorBadgesTable, GuidesModuleMetadata.Schema);

            badge.WithOwner().HasForeignKey("StandingId");

            // Surrogate, because the domain identity (badge, country) has a nullable half.
            badge.Property<int>("Id").ValueGeneratedOnAdd();
            badge.HasKey("Id");

            badge.Property(value => value.Badge)
                .HasMaxLength(GuidePersistenceConstants.BadgeLength)
                .IsRequired();

            badge.Property(value => value.CountryCode)
                .HasMaxLength(GuidePersistenceConstants.CountryCodeLength);
        });

        builder.HasIndex(standing => standing.UserId)
            .IsUnique()
            .HasDatabaseName("ux_guide_author_standing_user");

        builder.Ignore(standing => standing.DomainEvents);
    }
}
