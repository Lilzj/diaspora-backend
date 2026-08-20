using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Modules.Jobs.Domain.Pipelines;
using Diaspora.Modules.Jobs.Domain.Policies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Jobs.Persistence.Configurations;

/// <summary>
/// Maps the hiring pipeline and its stages — two tables.
/// <para>
/// The stage table keys on (pipeline, key) rather than a surrogate, because the key is what an
/// application stores and what a rename must not disturb. Two stages with one key on one board
/// would make an application's stage ambiguous, which is a state the database should refuse
/// rather than one the aggregate merely avoids.
/// </para>
/// </summary>
public sealed class HiringPipelineConfiguration : IEntityTypeConfiguration<HiringPipeline>
{
    public void Configure(EntityTypeBuilder<HiringPipeline> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(JobPersistenceConstants.PipelinesTable, JobsModuleMetadata.Schema);

        builder.HasKey(pipeline => pipeline.Id);

        builder.Property(pipeline => pipeline.Id)
            .HasConversion(JobValueConverters.Id<HiringPipelineId>());

        builder.Property(pipeline => pipeline.PostingId)
            .HasConversion(JobValueConverters.Id<JobPostingId>())
            .IsRequired();

        builder.Property(pipeline => pipeline.EmployerId)
            .HasConversion(JobValueConverters.Id<EmployerProfileId>())
            .IsRequired();

        builder.Property(pipeline => pipeline.Revision).IsConcurrencyToken();

        builder.OwnsMany(pipeline => pipeline.Stages, stage =>
        {
            stage.ToTable(
                JobPersistenceConstants.PipelineStagesTable, JobsModuleMetadata.Schema);

            stage.WithOwner().HasForeignKey("pipeline_id");
            stage.HasKey("pipeline_id", nameof(HiringStage.Key));

            stage.Property(value => value.Key)
                .HasMaxLength(JobLimits.MaxStageKey)
                .IsRequired();

            stage.Property(value => value.Name)
                .HasMaxLength(JobLimits.MaxDisplayName)
                .IsRequired();

            stage.Property(value => value.Kind)
                .HasConversion<string>()
                .HasMaxLength(JobPersistenceConstants.EnumLength)
                .IsRequired();

            stage.Property(value => value.Order).IsRequired();
            stage.Property(value => value.IsRemovable).IsRequired();
        });

        // One board per posting.
        builder.HasIndex(pipeline => pipeline.PostingId)
            .IsUnique()
            .HasDatabaseName("ux_job_pipelines_posting");

        builder.Ignore(pipeline => pipeline.DomainEvents);
        builder.Ignore(pipeline => pipeline.OwnerId);
        builder.Ignore(pipeline => pipeline.EntryStage);
        builder.Ignore(pipeline => pipeline.HiredStage);
    }
}
