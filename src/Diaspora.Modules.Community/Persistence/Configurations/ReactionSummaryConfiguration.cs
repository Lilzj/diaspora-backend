using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.Reactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Community.Persistence.Configurations;

/// <summary>
/// Maps per-content reaction summaries.
/// <para>
/// The denormalised content key is what makes one-summary-per-content a real unique index: an
/// index cannot span an owned type's columns and the aggregate's own, which is the same
/// constraint B5 recorded for its collection owner key.
/// </para>
/// </summary>
public sealed class ReactionSummaryConfiguration : IEntityTypeConfiguration<ReactionSummary>
{
    public void Configure(EntityTypeBuilder<ReactionSummary> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            CommunityPersistenceConstants.ReactionSummariesTable, CommunityModuleMetadata.Schema);

        builder.HasKey(summary => summary.Id);

        builder.Property(summary => summary.Id)
            .HasConversion(CommunityValueConverters.Id<ReactionSummaryId>());

        builder.Property(summary => summary.ContentKey)
            .HasMaxLength(CommunityPersistenceConstants.ContentKeyLength)
            .IsRequired();

        builder.OwnsOne(summary => summary.Content, content =>
        {
            content.Property(value => value.Type)
                .HasColumnName("content_type")
                .HasConversion<string>()
                .HasMaxLength(CommunityPersistenceConstants.EnumLength)
                .IsRequired();

            content.Property(value => value.Id).HasColumnName("content_id").IsRequired();
            content.Ignore(value => value.Key);
        });

        builder.Navigation(summary => summary.Content).IsRequired();

        builder.OwnsMany(summary => summary.Tallies, tally =>
        {
            tally.ToTable(
                CommunityPersistenceConstants.ReactionTalliesTable, CommunityModuleMetadata.Schema);

            tally.HasKey(value => value.Id);

            tally.Property(value => value.Id)
                .HasConversion(CommunityValueConverters.Id<ReactionTallyId>());

            tally.Property(value => value.ReactionKey)
                .HasMaxLength(ReactionDefinition.MaxKeyLength)
                .IsRequired();

            tally.Property(value => value.Sentiment)
                .HasConversion<string>()
                .HasMaxLength(CommunityPersistenceConstants.EnumLength)
                .IsRequired();

            tally.WithOwner().HasForeignKey("SummaryId");
            tally.Ignore(value => value.DomainEvents);
        });

        builder.HasIndex(summary => summary.ContentKey)
            .IsUnique()
            .HasDatabaseName("ix_reaction_summaries_content");

        builder.Ignore(summary => summary.DomainEvents);
    }
}
