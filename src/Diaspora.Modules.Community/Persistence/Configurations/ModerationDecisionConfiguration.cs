using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.Moderation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Community.Persistence.Configurations;

/// <summary>Maps the append-only decision history.</summary>
public sealed class ModerationDecisionConfiguration : IEntityTypeConfiguration<ModerationDecision>
{
    public void Configure(EntityTypeBuilder<ModerationDecision> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            CommunityPersistenceConstants.ModerationDecisionsTable, CommunityModuleMetadata.Schema);

        builder.HasKey(entity => entity.Id);

        builder.Property(entity => entity.Id)
            .HasConversion(CommunityValueConverters.Id<ModerationDecisionId>());

        builder.Property(entity => entity.Action)
            .HasConversion<string>()
            .HasMaxLength(CommunityPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(entity => entity.Source)
            .HasConversion<string>()
            .HasMaxLength(CommunityPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(entity => entity.ReportId)
            .HasConversion(CommunityValueConverters.Id<ContentReportId>());

        builder.Property(entity => entity.Reason)
            .HasMaxLength(ModerationDecision.MaxReasonLength);

        builder.Property(entity => entity.PreviousStatus)
            .HasMaxLength(CommunityPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(entity => entity.NewStatus)
            .HasMaxLength(CommunityPersistenceConstants.EnumLength)
            .IsRequired();

        builder.OwnsOne(entity => entity.Content, content =>
        {
            content.Property(value => value.Type)
                .HasColumnName("content_type")
                .HasConversion<string>()
                .HasMaxLength(CommunityPersistenceConstants.EnumLength)
                .IsRequired();

            content.Property(value => value.Id).HasColumnName("content_id").IsRequired();

            content.HasIndex(value => new { value.Type, value.Id })
                .HasDatabaseName("ix_moderation_decisions_content");

            content.Ignore(value => value.Key);
        });

        builder.Navigation(entity => entity.Content).IsRequired();
        builder.HasIndex(entity => entity.DecidedAt)
            .HasDatabaseName("ix_moderation_decisions_decided");

        builder.Ignore(entity => entity.DomainEvents);
    }
}
