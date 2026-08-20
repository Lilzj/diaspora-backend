using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Modules.Jobs.Domain.Policies;
using Diaspora.Modules.Jobs.Domain.Saved;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Jobs.Persistence.Configurations;

/// <summary>
/// Maps saved jobs. One table.
/// <para>
/// The unique index on (candidate, posting) is what makes saving idempotent and unsaving safe
/// to retry — both of which matter, because the bookmark control is tapped from a scrolling
/// list on a connection that drops.
/// </para>
/// </summary>
public sealed class SavedJobConfiguration : IEntityTypeConfiguration<SavedJob>
{
    public void Configure(EntityTypeBuilder<SavedJob> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(JobPersistenceConstants.SavedJobsTable, JobsModuleMetadata.Schema);

        builder.HasKey(saved => saved.Id);

        builder.Property(saved => saved.Id)
            .HasConversion(JobValueConverters.Id<SavedJobId>());

        builder.Property(saved => saved.CandidateId)
            .HasConversion(JobValueConverters.Id<CandidateProfileId>())
            .IsRequired();

        builder.Property(saved => saved.UserId)
            .HasConversion(JobValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(saved => saved.PostingId)
            .HasConversion(JobValueConverters.Id<JobPostingId>())
            .IsRequired();

        // The candidate's private note. Never visible to the employer.
        builder.Property(saved => saved.Note).HasMaxLength(JobLimits.MaxSavedJobNote);

        builder.Property(saved => saved.RemindBeforeClosing).IsRequired();
        builder.Property(saved => saved.ReminderSentAt);

        builder.HasIndex(saved => new { saved.CandidateId, saved.PostingId })
            .IsUnique()
            .HasDatabaseName("ux_job_saved_candidate_posting");

        // "Which of these twenty postings have I saved" on a results page.
        builder.HasIndex(saved => new { saved.UserId, saved.PostingId })
            .HasDatabaseName("ix_job_saved_user_posting");

        // The reminder sweep.
        builder.HasIndex(saved => new { saved.RemindBeforeClosing, saved.ReminderSentAt })
            .HasDatabaseName("ix_job_saved_reminders");

        builder.Ignore(saved => saved.DomainEvents);
        builder.Ignore(saved => saved.OwnerId);
    }
}
