using Diaspora.Modules.Location.Domain.Experience;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Location.Persistence.Configurations;

/// <summary>
/// Maps the <see cref="CountryExperience"/> aggregate and its owned expertise claims.
/// <para>
/// The unique index on (member, country) is the aggregate's identity made physical. One row
/// per member per country is what makes "amend your period" the only way to record a return
/// visit, and what stops two overlapping periods from doubling every derived
/// years-of-residence figure on a profile.
/// </para>
/// <para>
/// Note the absence of a verification column. That is deliberate and load-bearing: the proof
/// lives in <c>verified_residencies</c>, projected from Trust. A boolean here would be a
/// second place the same fact is stored, and it would be the stale one.
/// </para>
/// </summary>
public sealed class CountryExperienceConfiguration : IEntityTypeConfiguration<CountryExperience>
{
    public void Configure(EntityTypeBuilder<CountryExperience> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(LocationPersistenceConstants.ExperiencesTable, LocationModuleMetadata.Schema);

        builder.HasKey(experience => experience.Id);

        builder.Property(experience => experience.Id)
            .HasConversion(LocationValueConverters.Id<CountryExperienceId>());

        builder.Property(experience => experience.UserId)
            .HasConversion(LocationValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(experience => experience.CountryCode)
            .HasConversion(LocationValueConverters.CountryCode)
            .HasMaxLength(LocationPersistenceConstants.CountryCodeLength)
            .IsRequired();

        builder.OwnsOne(experience => experience.Period, period =>
        {
            period.Property(value => value.ArrivedOn)
                .HasColumnName("arrived_on")
                .IsRequired();

            period.Property(value => value.DepartedOn)
                .HasColumnName("departed_on");
        });

        builder.Navigation(experience => experience.Period).IsRequired();

        builder.Property(experience => experience.Role)
            .HasMaxLength(LocationPersistenceConstants.RoleLength);

        builder.Property(experience => experience.LanguageFamiliarity)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(experience => experience.CulturalFamiliarity)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        ConfigureExpertise(builder);

        builder.Property(experience => experience.CreatedAt).IsRequired();

        builder.HasIndex(experience => new { experience.UserId, experience.CountryCode })
            .IsUnique()
            .HasDatabaseName("ix_country_experiences_user_country");

        builder.Ignore(experience => experience.DomainEvents);
        builder.Ignore(experience => experience.IsCurrentResidence);
    }

    private static void ConfigureExpertise(EntityTypeBuilder<CountryExperience> builder)
    {
        builder.OwnsMany(experience => experience.Expertise, expertise =>
        {
            expertise.ToTable(
                LocationPersistenceConstants.ExpertiseTable, LocationModuleMetadata.Schema);

            expertise.HasKey(claim => claim.Id);

            expertise.Property(claim => claim.Id)
                .HasConversion(LocationValueConverters.Id<CountryExpertiseId>());

            expertise.Property(claim => claim.ExperienceId)
                .HasConversion(LocationValueConverters.Id<CountryExperienceId>())
                .IsRequired();

            expertise.WithOwner().HasForeignKey(claim => claim.ExperienceId);

            expertise.Property(claim => claim.Area)
                .HasMaxLength(LocationPersistenceConstants.ExpertiseAreaLength)
                .IsRequired();

            expertise.Property(claim => claim.Confidence)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            expertise.Property(claim => claim.DeclaredAt).IsRequired();

            expertise.HasIndex(claim => new { claim.ExperienceId, claim.Area })
                .IsUnique()
                .HasDatabaseName("ix_country_expertise_experience_area");

            expertise.Ignore(claim => claim.DomainEvents);
        });

        builder.Metadata
            .FindNavigation(nameof(CountryExperience.Expertise))?
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
