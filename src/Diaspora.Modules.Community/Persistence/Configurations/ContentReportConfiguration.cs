using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.Moderation;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Community.Persistence.Configurations;

/// <summary>Maps reports. One per reporter per item, enforced by index.</summary>
public sealed class ContentReportConfiguration : IEntityTypeConfiguration<ContentReport>
{
    public void Configure(EntityTypeBuilder<ContentReport> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            CommunityPersistenceConstants.ContentReportsTable, CommunityModuleMetadata.Schema);

        builder.HasKey(entity => entity.Id);

        builder.Property(entity => entity.Id)
            .HasConversion(CommunityValueConverters.Id<ContentReportId>());

        builder.Property(entity => entity.ReporterId)
            .HasConversion(CommunityValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(entity => entity.ReporterKey)
            .HasMaxLength(CommunityPersistenceConstants.CompositeKeyLength)
            .IsRequired();

        builder.Property(entity => entity.Reason)
            .HasConversion<string>()
            .HasMaxLength(CommunityPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(entity => entity.Status)
            .HasConversion<string>()
            .HasMaxLength(CommunityPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(entity => entity.Description)
            .HasMaxLength(ContentReport.MaxDescriptionLength);

        builder.Property(entity => entity.EvidenceUrl)
            .HasMaxLength(ContentReport.MaxEvidenceUrlLength);

        builder.Property(entity => entity.ResolutionNote)
            .HasMaxLength(ContentReport.MaxDescriptionLength);

        builder.OwnsOne(entity => entity.Content, content =>
        {
            content.Property(value => value.Type)
                .HasColumnName("content_type")
                .HasConversion<string>()
                .HasMaxLength(CommunityPersistenceConstants.EnumLength)
                .IsRequired();

            content.Property(value => value.Id).HasColumnName("content_id").IsRequired();

            content.HasIndex(value => new { value.Type, value.Id })
                .HasDatabaseName("ix_content_reports_content");

            content.Ignore(value => value.Key);
        });

        builder.Navigation(entity => entity.Content).IsRequired();
        builder.HasIndex(entity => entity.ReporterKey)
            .IsUnique()
            .HasDatabaseName("ix_content_reports_reporter_content");

        // The moderator queue: open reports, oldest first.
        builder.HasIndex(entity => new { entity.Status, entity.ReportedAt })
            .HasDatabaseName("ix_content_reports_status");

        builder.Ignore(entity => entity.DomainEvents);
        builder.Ignore(entity => entity.IsOpen);
    }
}
