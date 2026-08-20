using Diaspora.Modules.Recommendations.Domain.Identifiers;
using Diaspora.Modules.Recommendations.Domain.Signals;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Recommendations.Persistence.Configurations;

/// <summary>
/// Maps the immutable signal record.
/// <para>
/// <b>The largest table in the schema by two orders of magnitude, and every index here exists for a
/// query that runs on a hot path.</b> A platform with a hundred thousand active members generates
/// millions of rows a month, and this table is written on every impression and read on every profile
/// build.
/// </para>
/// <para>
/// <b>The unprocessed index is partial in spirit and cannot be in fact.</b> The profile builder wants
/// "this member's signals where <c>ProcessedAt</c> is null, oldest first", which on PostgreSQL would
/// be a partial index and on SQLite would not — so it is declared as an ordinary composite and the
/// planner does the rest. Declaring it provider-specifically would make the model unbuildable on the
/// integration suite's provider, which B1 established is not a trade this platform makes.
/// </para>
/// <para>
/// <b>The context is owned rather than a child table.</b> Four nullable strings read together on
/// every fold; a join for them would double the cost of the busiest read in the module.
/// </para>
/// <para>
/// <b>No concurrency token, deliberately.</b> A signal is written once and never contended. Adding a
/// token would cost a column on the largest table in the schema to protect against a race that
/// cannot happen.
/// </para>
/// </summary>
public sealed class UserSignalConfiguration : IEntityTypeConfiguration<UserSignal>
{
    public void Configure(EntityTypeBuilder<UserSignal> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            RecommendationPersistenceConstants.SignalsTable,
            RecommendationsModuleMetadata.Schema);

        builder.HasKey(signal => signal.Id);

        builder.Property(signal => signal.Id)
            .HasConversion(RecommendationValueConverters.Id<UserSignalId>());

        builder.Property(signal => signal.UserId)
            .HasConversion(RecommendationValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(signal => signal.Kind)
            .HasConversion<string>()
            .HasMaxLength(RecommendationPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(signal => signal.Source)
            .HasConversion<string>()
            .HasMaxLength(RecommendationPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(signal => signal.TargetType)
            .HasConversion<string>()
            .HasMaxLength(RecommendationPersistenceConstants.EnumLength);

        builder.Property(signal => signal.KeyKind)
            .HasConversion<string>()
            .HasMaxLength(RecommendationPersistenceConstants.EnumLength);

        builder.Property(signal => signal.Surface)
            .HasConversion<string>()
            .HasMaxLength(RecommendationPersistenceConstants.EnumLength);

        builder.Property(signal => signal.Key)
            .HasMaxLength(RecommendationPersistenceConstants.KeyLength);

        builder.Property(signal => signal.SourceEventName)
            .HasMaxLength(RecommendationPersistenceConstants.EventNameLength);

        builder.Property(signal => signal.Weight)
            .HasPrecision(
                RecommendationPersistenceConstants.ScorePrecision,
                RecommendationPersistenceConstants.ScoreScale);

        // Four nullable strings, read together on every fold. See the class summary.
        builder.OwnsOne(signal => signal.Context, context =>
        {
            context.Property(value => value.CountryCode)
                .HasColumnName("country_code")
                .HasMaxLength(RecommendationPersistenceConstants.CountryLength);

            context.Property(value => value.CityName)
                .HasColumnName("city_name")
                .HasMaxLength(RecommendationPersistenceConstants.PlaceLength);

            context.Property(value => value.NeighborhoodName)
                .HasColumnName("neighborhood_name")
                .HasMaxLength(RecommendationPersistenceConstants.PlaceLength);

            context.Property(value => value.LanguageCode)
                .HasColumnName("language_code")
                .HasMaxLength(RecommendationPersistenceConstants.LanguageLength);
        });

        builder.Navigation(signal => signal.Context).IsRequired();

        // The profile builder's read: one member, unfolded, oldest first.
        builder.HasIndex(signal => new { signal.UserId, signal.ProcessedAt, signal.OccurredAt })
            .HasDatabaseName("ix_user_signals_unprocessed");

        // The recent-history read, for similarity references and the engaged-target set.
        builder.HasIndex(signal => new { signal.UserId, signal.OccurredAt })
            .HasDatabaseName("ix_user_signals_recent");

        // The "has this member engaged with this thing" read.
        builder.HasIndex(signal => new { signal.UserId, signal.TargetType, signal.TargetId })
            .HasDatabaseName("ix_user_signals_target");

        // The retention sweep. Without it the sweep sorts the largest table in the schema to find
        // the oldest thousand rows.
        builder.HasIndex(signal => signal.ExpiresAt)
            .HasDatabaseName("ix_user_signals_expiry");

        builder.Ignore(signal => signal.DomainEvents);
    }
}
