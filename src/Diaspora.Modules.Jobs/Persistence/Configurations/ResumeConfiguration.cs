using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Modules.Jobs.Domain.Policies;
using Diaspora.Modules.Jobs.Domain.Resumes;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Jobs.Persistence.Configurations;

/// <summary>
/// Maps the résumé aggregate. One table, no owned types.
/// <para>
/// Every column describing the file — name, size, MIME type — is a copy of what Media reported
/// at upload, kept so an employer's download link can render without a round trip into B5. The
/// bytes themselves are never here, and <c>MediaId</c> is the only thing that can fetch them.
/// </para>
/// </summary>
public sealed class ResumeConfiguration : IEntityTypeConfiguration<Resume>
{
    public void Configure(EntityTypeBuilder<Resume> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(JobPersistenceConstants.ResumesTable, JobsModuleMetadata.Schema);

        builder.HasKey(resume => resume.Id);

        builder.Property(resume => resume.Id)
            .HasConversion(JobValueConverters.Id<ResumeId>());

        builder.Property(resume => resume.CandidateId)
            .HasConversion(JobValueConverters.Id<CandidateProfileId>())
            .IsRequired();

        builder.Property(resume => resume.UserId)
            .HasConversion(JobValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(resume => resume.MediaId).IsRequired();

        builder.Property(resume => resume.Label)
            .HasMaxLength(JobLimits.MaxResumeLabel)
            .IsRequired();

        builder.Property(resume => resume.FileName).HasMaxLength(JobLimits.MaxDisplayName);
        builder.Property(resume => resume.SizeInBytes);

        builder.Property(resume => resume.ContentType)
            .HasMaxLength(JobPersistenceConstants.ContentTypeLength);

        builder.Property(resume => resume.IsDefault).IsRequired();
        builder.Property(resume => resume.UseCount).IsRequired();

        builder.HasIndex(resume => new { resume.CandidateId, resume.IsDefault })
            .HasDatabaseName("ix_job_resumes_candidate_default");

        builder.HasIndex(resume => resume.UserId)
            .HasDatabaseName("ix_job_resumes_user");

        builder.Ignore(resume => resume.DomainEvents);
        builder.Ignore(resume => resume.OwnerId);
    }
}
