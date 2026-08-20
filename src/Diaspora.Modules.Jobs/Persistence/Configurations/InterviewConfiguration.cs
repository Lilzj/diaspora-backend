using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Modules.Jobs.Domain.Interviews;
using Diaspora.Modules.Jobs.Domain.Policies;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Jobs.Persistence.Configurations;

/// <summary>
/// Maps the interview aggregate and its participant collection — two tables.
/// <para>
/// <b>The calendar indexes are split rather than composite, and that is EF's constraint rather
/// than a choice.</b> The natural index for a recruiter's calendar is (employer, scheduled_at)
/// — but <c>scheduled_at</c> lives on the owned <c>Schedule</c> and <c>employer_id</c> on the
/// aggregate, and EF cannot build an index spanning the two. That is B6's lesson, which B7 and
/// B8 each record again. So the instant is indexed inside the owned builder, the two owner
/// columns outside, and the query planner intersects them.
/// </para>
/// </summary>
public sealed class InterviewConfiguration : IEntityTypeConfiguration<Interview>
{
    public void Configure(EntityTypeBuilder<Interview> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(JobPersistenceConstants.InterviewsTable, JobsModuleMetadata.Schema);

        builder.HasKey(interview => interview.Id);

        builder.Property(interview => interview.Id)
            .HasConversion(JobValueConverters.Id<InterviewId>());

        builder.Property(interview => interview.ApplicationId)
            .HasConversion(JobValueConverters.Id<JobApplicationId>())
            .IsRequired();

        builder.Property(interview => interview.PostingId)
            .HasConversion(JobValueConverters.Id<JobPostingId>())
            .IsRequired();

        builder.Property(interview => interview.EmployerId)
            .HasConversion(JobValueConverters.Id<EmployerProfileId>())
            .IsRequired();

        builder.Property(interview => interview.CandidateId)
            .HasConversion(JobValueConverters.Id<CandidateProfileId>())
            .IsRequired();

        builder.Property(interview => interview.RoundNumber).IsRequired();

        builder.Property(interview => interview.Title).HasMaxLength(JobLimits.MaxDisplayName);

        builder.Property(interview => interview.Outcome)
            .HasConversion<string>()
            .HasMaxLength(JobPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(interview => interview.CancellationReason)
            .HasMaxLength(JobLimits.MaxInterviewNote);

        // The panel's notes. Internal, and never rendered to the candidate.
        builder.Property(interview => interview.Feedback)
            .HasMaxLength(JobLimits.MaxInterviewNote);

        // The version the employer chose to share. Usually null.
        builder.Property(interview => interview.CandidateFeedback)
            .HasMaxLength(JobLimits.MaxInterviewNote);

        builder.Property(interview => interview.Revision).IsConcurrencyToken();

        ConfigureSchedule(builder);
        ConfigureParticipants(builder);

        // One round per application per number, so two recruiters cannot both create "round 2".
        builder.HasIndex(interview => new { interview.ApplicationId, interview.RoundNumber })
            .IsUnique()
            .HasDatabaseName("ux_job_interviews_application_round");

        // The two calendars. Intersected with the owned scheduled_at index above — see the
        // class summary for why they cannot be one composite index each.
        builder.HasIndex(interview => interview.EmployerId)
            .HasDatabaseName("ix_job_interviews_employer");

        builder.HasIndex(interview => interview.CandidateId)
            .HasDatabaseName("ix_job_interviews_candidate");

        builder.Ignore(interview => interview.DomainEvents);
        builder.Ignore(interview => interview.OwnerId);
    }

    private static void ConfigureSchedule(EntityTypeBuilder<Interview> builder)
    {
        builder.OwnsOne(interview => interview.Schedule, schedule =>
        {
            schedule.Property(value => value.ScheduledAt)
                .HasColumnName("scheduled_at")
                .IsRequired();

            schedule.Property(value => value.DurationMinutes)
                .HasColumnName("duration_minutes")
                .IsRequired();

            // The employer's wall clock, beside the instant. The instant says unambiguously
            // when; the zone says which clock they meant, which is what a candidate three time
            // zones away needs rendered.
            schedule.Property(value => value.TimeZoneId)
                .HasColumnName("time_zone_id")
                .HasMaxLength(JobPersistenceConstants.TimeZoneLength);

            schedule.Property(value => value.Mode)
                .HasColumnName("mode")
                .HasConversion<string>()
                .HasMaxLength(JobPersistenceConstants.EnumLength)
                .IsRequired();

            schedule.Property(value => value.LocationAddress)
                .HasColumnName("location_address")
                .HasMaxLength(JobLimits.MaxLocationName);

            schedule.Property(value => value.JoiningUrl)
                .HasColumnName("joining_url")
                .HasMaxLength(JobLimits.MaxUrl);

            schedule.Property(value => value.Instructions)
                .HasColumnName("instructions")
                .HasMaxLength(JobLimits.MaxInterviewNote);

            // The reminder sweep and both calendars read by this. The column is owned, so the
            // index is declared here — EF cannot build one that spans owned and owner columns.
            schedule.HasIndex(value => value.ScheduledAt)
                .HasDatabaseName("ix_job_interviews_scheduled_at");
        });

        builder.Navigation(interview => interview.Schedule).IsRequired();
    }

    private static void ConfigureParticipants(EntityTypeBuilder<Interview> builder) =>
        builder.OwnsMany(interview => interview.Participants, participant =>
        {
            participant.ToTable(
                JobPersistenceConstants.InterviewParticipantsTable, JobsModuleMetadata.Schema);

            participant.HasKey(value => value.ParticipantId);
            participant.Property(value => value.ParticipantId).ValueGeneratedNever();

            // Optional: many interviewers are not platform members, and requiring an account
            // for each would mean employers simply stopped listing the panel.
            participant.Property(value => value.UserId)
                .HasConversion(JobValueConverters.Id<UserId>());

            participant.Property(value => value.Name)
                .HasMaxLength(JobLimits.MaxDisplayName)
                .IsRequired();

            participant.Property(value => value.Role).HasMaxLength(JobLimits.MaxDisplayName);
            participant.Property(value => value.Order).IsRequired();
        });
}
