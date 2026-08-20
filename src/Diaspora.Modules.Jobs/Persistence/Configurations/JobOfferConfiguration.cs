using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Modules.Jobs.Domain.Offers;
using Diaspora.Modules.Jobs.Domain.Policies;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Jobs.Persistence.Configurations;

/// <summary>
/// Maps the offer aggregate. One table — an offer owns its terms and nothing else.
/// <para>
/// <b>There is no unique index on the application.</b> An employer who revises terms issues a
/// new offer that supersedes the old one, so several offers per application is the normal case
/// and the constraint that matters — at most one <em>live</em> at a time — is a predicate over a
/// status column rather than a uniqueness rule. The command enforces it; a filtered unique index
/// would express it, but not portably across the platform's providers, and SQLite is what the
/// integration suite runs on.
/// </para>
/// </summary>
public sealed class JobOfferConfiguration : IEntityTypeConfiguration<JobOffer>
{
    public void Configure(EntityTypeBuilder<JobOffer> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(JobPersistenceConstants.OffersTable, JobsModuleMetadata.Schema);

        builder.HasKey(offer => offer.Id);

        builder.Property(offer => offer.Id)
            .HasConversion(JobValueConverters.Id<JobOfferId>());

        builder.Property(offer => offer.ApplicationId)
            .HasConversion(JobValueConverters.Id<JobApplicationId>())
            .IsRequired();

        builder.Property(offer => offer.PostingId)
            .HasConversion(JobValueConverters.Id<JobPostingId>())
            .IsRequired();

        builder.Property(offer => offer.EmployerId)
            .HasConversion(JobValueConverters.Id<EmployerProfileId>())
            .IsRequired();

        builder.Property(offer => offer.CandidateId)
            .HasConversion(JobValueConverters.Id<CandidateProfileId>())
            .IsRequired();

        builder.Property(offer => offer.Status)
            .HasConversion<string>()
            .HasMaxLength(JobPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(offer => offer.AnsweredBy)
            .HasConversion(JobValueConverters.Id<UserId>());

        builder.Property(offer => offer.ResponseReason)
            .HasMaxLength(JobLimits.MaxOfferNote);

        // The revision trail: an amended offer points at the one it replaced, so a candidate
        // can always see what they were originally told.
        builder.Property(offer => offer.SupersedesOfferId)
            .HasConversion(JobValueConverters.Id<JobOfferId>());

        builder.Property(offer => offer.Revision).IsConcurrencyToken();

        ConfigureTerms(builder);

        builder.HasIndex(offer => new { offer.ApplicationId, offer.Status })
            .HasDatabaseName("ix_job_offers_application_status");

        builder.HasIndex(offer => new { offer.EmployerId, offer.Status })
            .HasDatabaseName("ix_job_offers_employer_status");

        builder.HasIndex(offer => new { offer.CandidateId, offer.Status })
            .HasDatabaseName("ix_job_offers_candidate_status");

        builder.Ignore(offer => offer.DomainEvents);
        builder.Ignore(offer => offer.OwnerId);
        builder.Ignore(offer => offer.IsVisibleToCandidate);
    }

    private static void ConfigureTerms(EntityTypeBuilder<JobOffer> builder)
    {
        builder.OwnsOne(offer => offer.Terms, terms =>
        {
            // Required, unlike a posting's salary. A candidate being asked to accept a job —
            // very often to move country for it — is entitled to the number in writing.
            terms.Property(value => value.SalaryAmount)
                .HasColumnName("salary_amount")
                .HasColumnType(JobPersistenceConstants.MoneyColumnType)
                .IsRequired();

            terms.Property(value => value.CurrencyCode)
                .HasColumnName("salary_currency")
                .HasMaxLength(JobPersistenceConstants.CurrencyCodeLength)
                .IsRequired();

            terms.Property(value => value.Period)
                .HasColumnName("salary_period")
                .HasConversion<string>()
                .HasMaxLength(JobPersistenceConstants.EnumLength)
                .IsRequired();

            terms.Property(value => value.StartDate).HasColumnName("start_date");

            terms.Property(value => value.ExpiresAt)
                .HasColumnName("expires_at")
                .IsRequired();

            terms.Property(value => value.BenefitsSummary)
                .HasColumnName("benefits_summary")
                .HasMaxLength(JobLimits.MaxOfferNote);

            terms.Property(value => value.Notes)
                .HasColumnName("notes")
                .HasMaxLength(JobLimits.MaxOfferNote);

            terms.Property(value => value.IncludesRelocation)
                .HasColumnName("includes_relocation")
                .IsRequired();

            terms.Property(value => value.IncludesVisaSponsorship)
                .HasColumnName("includes_visa_sponsorship")
                .IsRequired();

            // The expiry sweep's work list. Owned column, so the index lives here.
            terms.HasIndex(value => value.ExpiresAt)
                .HasDatabaseName("ix_job_offers_expires_at");
        });

        builder.Navigation(offer => offer.Terms).IsRequired();
    }
}
