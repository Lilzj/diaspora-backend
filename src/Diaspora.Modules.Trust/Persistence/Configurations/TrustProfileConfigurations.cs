using Diaspora.Modules.Trust.Domain;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Diaspora.Modules.Trust.Persistence.Configurations;

/// <summary>Maps the <see cref="TrustProfile"/> aggregate.</summary>
public sealed class TrustProfileConfiguration : IEntityTypeConfiguration<TrustProfile>
{
    public void Configure(EntityTypeBuilder<TrustProfile> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("trust_profiles", TrustModuleMetadata.Schema);

        builder.HasKey(profile => profile.Id);

        builder.Property(profile => profile.Id)
            .HasConversion(new StronglyTypedIdValueConverter<TrustProfileId>());

        builder.Property(profile => profile.UserId).IsRequired();

        builder.Property(profile => profile.Tier)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(profile => profile.Health)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(profile => profile.HealthReasons)
            .HasMaxLength(TrustProfile.MaxReasonLength);

        builder.Property(profile => profile.JoinedAt).IsRequired();

        // The grant set drives every tier derivation, so it is read on the hot ITrustService
        // path. Stored inline with the profile rather than derived by scanning the
        // verification table, which would turn "is this member verified?" into a join.
        builder.Property<HashSet<VerificationType>>("_grants")
            .HasField("_grants")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasColumnName("grants")
            .HasConversion(
                grants => string.Join(
                    TrustPersistenceConstants.ListSeparator,
                    grants.Select(grant => grant.ToString())),
                value => value
                    .Split(TrustPersistenceConstants.ListSeparator, StringSplitOptions.RemoveEmptyEntries)
                    .Select(Enum.Parse<VerificationType>)
                    .ToHashSet(),
                new ValueComparer<HashSet<VerificationType>>(
                    (left, right) => left != null && right != null && left.SetEquals(right),
                    grants => grants.Aggregate(0, (hash, grant) => HashCode.Combine(hash, grant)),
                    grants => grants.ToHashSet()))
            .HasMaxLength(512);

        builder.Ignore(profile => profile.Grants);

        // Risk is a computed opinion, not an entity — stored as an owned value so it lives on
        // the profile row and cannot be queried into existence separately from it.
        builder.OwnsOne(profile => profile.Risk, risk =>
        {
            risk.Property(assessment => assessment.Score)
                .HasColumnName("risk_score")
                .IsRequired();

            risk.Property(assessment => assessment.Level)
                .HasColumnName("risk_level")
                .HasConversion<string>()
                .HasMaxLength(16)
                .IsRequired();

            risk.Property(assessment => assessment.AssessedAt)
                .HasColumnName("risk_assessed_at")
                .IsRequired();

            risk.Property(assessment => assessment.Factors)
                .HasColumnName("risk_factors")
                .HasConversion(
                    factors => string.Join(
                        TrustPersistenceConstants.ListSeparator,
                        factors.Select(factor => factor.ToString())),
                    value => value
                        .Split(TrustPersistenceConstants.ListSeparator, StringSplitOptions.RemoveEmptyEntries)
                        .Select(Enum.Parse<RiskFactor>)
                        .ToList(),
                    new ValueComparer<IReadOnlyList<RiskFactor>>(
                        (left, right) => left != null && right != null && left.SequenceEqual(right),
                        factors => factors.Aggregate(0, (hash, factor) => HashCode.Combine(hash, factor)),
                        factors => factors.ToList()))
                .HasMaxLength(256);
        });

        builder.HasMany(profile => profile.Badges)
            .WithOne()
            .HasForeignKey(badge => badge.ProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(profile => profile.Countries)
            .WithOne()
            .HasForeignKey(standing => standing.ProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(profile => profile.Badges)
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasField("_badges");

        builder.Navigation(profile => profile.Countries)
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasField("_countries");

        // One profile per member. The unique index is the invariant made physical — two
        // profiles for one member would give them two different trust levels at once.
        builder.HasIndex(profile => profile.UserId)
            .IsUnique()
            .HasDatabaseName("ix_trust_profiles_user");

        builder.Ignore(profile => profile.DomainEvents);
    }
}

public sealed class BadgeAwardConfiguration : IEntityTypeConfiguration<BadgeAward>
{
    public void Configure(EntityTypeBuilder<BadgeAward> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("badge_awards", TrustModuleMetadata.Schema);

        builder.HasKey(award => award.Id);

        builder.Property(award => award.Id)
            .HasConversion(new StronglyTypedIdValueConverter<BadgeAwardId>());

        builder.Property(award => award.ProfileId)
            .HasConversion(new StronglyTypedIdValueConverter<TrustProfileId>())
            .IsRequired();

        builder.Property(award => award.Badge)
            .HasConversion<string>()
            .HasMaxLength(48)
            .IsRequired();

        builder.Property(award => award.CountryCode)
            .HasMaxLength(BadgeAward.MaxCountryCodeLength);

        builder.Property(award => award.RevokedReason)
            .HasMaxLength(BadgeAward.MaxReasonLength);

        builder.Property(award => award.AwardedBy).HasMaxLength(200);

        builder.Property(award => award.AwardedAt).IsRequired();

        // One award per (profile, badge, country). Re-earning a revoked badge reinstates the
        // existing row rather than adding a second, and the index is what enforces that.
        builder.HasIndex(award => new { award.ProfileId, award.Badge, award.CountryCode })
            .IsUnique()
            .HasDatabaseName("ix_badge_awards_profile_badge_country");

        builder.Ignore(award => award.DomainEvents);
    }
}

public sealed class CountryStandingConfiguration : IEntityTypeConfiguration<CountryStanding>
{
    public void Configure(EntityTypeBuilder<CountryStanding> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("country_standings", TrustModuleMetadata.Schema);

        builder.HasKey(standing => standing.Id);

        builder.Property(standing => standing.Id)
            .HasConversion(new StronglyTypedIdValueConverter<CountryStandingId>());

        builder.Property(standing => standing.ProfileId)
            .HasConversion(new StronglyTypedIdValueConverter<TrustProfileId>())
            .IsRequired();

        builder.Property(standing => standing.CountryCode)
            .HasMaxLength(CountryStanding.MaxCountryCodeLength)
            .IsRequired();

        builder.Property(standing => standing.Level)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(standing => standing.Role)
            .HasMaxLength(CountryStanding.MaxRoleLength);

        builder.Property(standing => standing.UpdatedAt).IsRequired();

        builder.Ignore(standing => standing.Stars);
        builder.Ignore(standing => standing.HasDeparted);

        builder.HasIndex(standing => new { standing.ProfileId, standing.CountryCode })
            .IsUnique()
            .HasDatabaseName("ix_country_standings_profile_country");

        builder.Ignore(standing => standing.DomainEvents);
    }
}

/// <summary>Maps the <see cref="ReputationProfile"/> aggregate.</summary>
public sealed class ReputationProfileConfiguration : IEntityTypeConfiguration<ReputationProfile>
{
    public void Configure(EntityTypeBuilder<ReputationProfile> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("reputation_profiles", TrustModuleMetadata.Schema);

        builder.HasKey(reputation => reputation.Id);

        builder.Property(reputation => reputation.Id)
            .HasConversion(new StronglyTypedIdValueConverter<ReputationProfileId>());

        builder.Property(reputation => reputation.UserId).IsRequired();

        builder.Property(reputation => reputation.Level)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(reputation => reputation.NoViolationSince).IsRequired();
        builder.Property(reputation => reputation.LastCalculatedAt).IsRequired();

        // The same int token Financial's aggregates use. The aggregate advances it; there is no
        // interceptor. A conflict surfaces as ConcurrencyConflictException — a 409 to an HTTP
        // caller, a retry to an outbox consumer.
        builder.Property(reputation => reputation.Revision).IsConcurrencyToken();

        // Derived, never stored: a persisted copy would go stale the moment the benefit table
        // or the thresholds changed, and would then grant the wrong thing silently.
        builder.Ignore(reputation => reputation.CombinedScore);
        builder.Ignore(reputation => reputation.Benefits);
        builder.Ignore(reputation => reputation.NextLevel);

        builder.HasMany(reputation => reputation.Events)
            .WithOne()
            .HasForeignKey(reputationEvent => reputationEvent.ProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(reputation => reputation.Events)
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasField("_events");

        builder.HasIndex(reputation => reputation.UserId)
            .IsUnique()
            .HasDatabaseName("ix_reputation_profiles_user");

        builder.Ignore(reputation => reputation.DomainEvents);
    }
}

public sealed class ReputationEventConfiguration : IEntityTypeConfiguration<ReputationEvent>
{
    public void Configure(EntityTypeBuilder<ReputationEvent> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("reputation_events", TrustModuleMetadata.Schema);

        builder.HasKey(reputationEvent => reputationEvent.Id);

        builder.Property(reputationEvent => reputationEvent.Id)
            .HasConversion(new StronglyTypedIdValueConverter<ReputationEventId>());

        builder.Property(reputationEvent => reputationEvent.ProfileId)
            .HasConversion(new StronglyTypedIdValueConverter<ReputationProfileId>())
            .IsRequired();

        builder.Property(reputationEvent => reputationEvent.ActionType)
            .HasMaxLength(ReputationEvent.MaxActionTypeLength)
            .IsRequired();

        builder.Property(reputationEvent => reputationEvent.Dimension)
            .HasConversion<string>()
            .HasMaxLength(24)
            .IsRequired();

        builder.Property(reputationEvent => reputationEvent.SourceType)
            .HasMaxLength(ReputationEvent.MaxSourceTypeLength);

        builder.Property(reputationEvent => reputationEvent.SourceId)
            .HasMaxLength(ReputationEvent.MaxSourceIdLength);

        builder.Property(reputationEvent => reputationEvent.CountryCode).HasMaxLength(2);

        builder.Property(reputationEvent => reputationEvent.Description)
            .HasMaxLength(ReputationEvent.MaxDescriptionLength);

        builder.Property(reputationEvent => reputationEvent.CreatedAt).IsRequired();

        builder.Property(reputationEvent => reputationEvent.AwardKey)
            .HasMaxLength(ReputationAwardKey.MaxLength);

        // Through the non-generic overload, as FinancialValueConverters.Id does: the generic one
        // wants a converter of ReputationEventId?, and null never reaches a converter anyway.
        builder.Property(reputationEvent => reputationEvent.ReversesEventId)
            .HasConversion((ValueConverter)new StronglyTypedIdValueConverter<ReputationEventId>());

        // What reversal reads (D-9): every award with a given source, across profiles.
        builder.HasIndex(reputationEvent => new
        {
            reputationEvent.SourceType,
            reputationEvent.SourceId
        })
            .HasDatabaseName("ix_reputation_events_source");

        // What "already reversed?" reads.
        builder.HasIndex(reputationEvent => reputationEvent.ReversesEventId)
            .HasDatabaseName("ix_reputation_events_reverses");

        // The guarantee behind ReputationAwardKey: one award per business fact per member, held
        // by the database rather than by a read that two outbox workers can both pass. Rows from
        // before awards were keyed are NULL, and NULLs are distinct on Postgres and SQLite alike,
        // so history needs no rewrite for this to apply.
        builder.HasIndex(reputationEvent => new
        {
            reputationEvent.ProfileId,
            reputationEvent.AwardKey
        })
            .IsUnique()
            .HasDatabaseName("ix_reputation_events_profile_award_key");

        // Backs the MaxOccurrences and CooldownHours caps, which read count-and-latest for
        // one (profile, action) pair on every scoring attempt.
        builder.HasIndex(reputationEvent => new
        {
            reputationEvent.ProfileId,
            reputationEvent.ActionType,
            reputationEvent.CreatedAt
        })
            .HasDatabaseName("ix_reputation_events_profile_action_created");

        builder.Ignore(reputationEvent => reputationEvent.DomainEvents);
    }
}
