using Diaspora.Modules.Media.Domain.Identifiers;
using Diaspora.Modules.Media.Domain.Processing;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Media.Persistence.Configurations;

/// <summary>Maps the <see cref="ProcessingJob"/> aggregate and its steps.</summary>
public sealed class ProcessingJobConfiguration : IEntityTypeConfiguration<ProcessingJob>
{
    public void Configure(EntityTypeBuilder<ProcessingJob> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(MediaPersistenceConstants.ProcessingJobsTable, MediaModuleMetadata.Schema);

        builder.HasKey(job => job.Id);

        builder.Property(job => job.Id)
            .HasConversion(MediaValueConverters.Id<ProcessingJobId>());

        builder.Property(job => job.MediaId)
            .HasConversion(MediaValueConverters.Id<MediaId>())
            .IsRequired();

        builder.Property(job => job.Status)
            .HasConversion<string>()
            .HasMaxLength(MediaPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(job => job.FailureReason)
            .HasMaxLength(MediaPersistenceConstants.DetailLength);

        builder.OwnsMany(job => job.Tasks, task =>
        {
            task.ToTable(MediaPersistenceConstants.ProcessingTasksTable, MediaModuleMetadata.Schema);

            task.HasKey(value => value.Id);

            task.Property(value => value.Id)
                .HasConversion(MediaValueConverters.Id<ProcessingTaskId>());

            task.Property(value => value.JobId)
                .HasConversion(MediaValueConverters.Id<ProcessingJobId>())
                .IsRequired();

            task.Property(value => value.Operation)
                .HasConversion<string>()
                .HasMaxLength(MediaPersistenceConstants.EnumLength)
                .IsRequired();

            task.Property(value => value.Status)
                .HasConversion<string>()
                .HasMaxLength(MediaPersistenceConstants.EnumLength)
                .IsRequired();

            task.Property(value => value.Detail)
                .HasMaxLength(MediaPersistenceConstants.DetailLength);

            task.WithOwner().HasForeignKey(value => value.JobId);

            task.HasIndex(value => new { value.JobId, value.Operation })
                .IsUnique()
                .HasDatabaseName("ix_processing_tasks_job_operation");

            task.Ignore(value => value.DomainEvents);
        });

        // "The latest job for this asset", which is what the diagnostic endpoint reads.
        builder.HasIndex(job => new { job.MediaId, job.CreatedAt })
            .HasDatabaseName("ix_processing_jobs_media_created");

        // The worker's queue scan. Oldest pending first, so a busy hour does not starve the
        // member who has already waited longest.
        builder.HasIndex(job => new { job.Status, job.CreatedAt })
            .HasDatabaseName("ix_processing_jobs_status_created");

        builder.Ignore(job => job.DomainEvents);
        builder.Ignore(job => job.PendingTasks);
        builder.Ignore(job => job.CanRetry);
    }
}
