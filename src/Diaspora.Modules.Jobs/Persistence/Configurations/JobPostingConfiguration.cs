using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Modules.Jobs.Domain.Policies;
using Diaspora.Modules.Jobs.Domain.Postings;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Jobs.Persistence.Configurations;

/// <summary>
/// Maps the posting aggregate and everything it owns — nine tables.
/// <para>
/// <b>Every owned reference is declared required and its scalars nullable where the domain says
/// so.</b> That combination is the only EF Core shape that works for table-sharing dependents,
/// per B6's lesson and the four-fold repeat by B7 and B8. There is no optional owned reference
/// on this aggregate at all, which sidesteps the round-trip caveat entirely.
/// </para>
/// <para>
/// <b>No index spans an owned type's columns and the aggregate's own.</b> EF cannot build one —
/// B6's lesson, recorded again by B7 and B8 — so indexes on owned columns (slug, country,
/// workplace type, salary, verification state) are declared inside their <c>OwnsOne</c>
/// builders, and indexes on aggregate columns outside.
/// </para>
/// <para>
/// <b>Every owned collection keys on a caller-supplied identifier with
/// <c>ValueGeneratedNever</c>.</b> Not tidiness: EF's convention makes a Guid key
/// store-generated, and a store-generated key already holding a non-default value tells EF the
/// row exists — it emits an UPDATE instead of an INSERT, matches nothing, and surfaces as a
/// bogus concurrency conflict on the employer's first save.
/// </para>
/// <para>
/// <b>Soft delete plus owned types is the combination B1.0.1 exists for.</b> This aggregate is
/// both — eight owned collections — and <c>SoftDeleteConverter</c> handles it; nothing here
/// declares the filter or the cascade, deliberately.
/// </para>
/// </summary>
public sealed class JobPostingConfiguration : IEntityTypeConfiguration<JobPosting>
{
    public void Configure(EntityTypeBuilder<JobPosting> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(JobPersistenceConstants.JobPostingsTable, JobsModuleMetadata.Schema);

        builder.HasKey(posting => posting.Id);

        builder.Property(posting => posting.Id)
            .HasConversion(JobValueConverters.Id<JobPostingId>());

        builder.Property(posting => posting.EmployerId)
            .HasConversion(JobValueConverters.Id<EmployerProfileId>())
            .IsRequired();

        builder.Property(posting => posting.CreatedByUserId)
            .HasConversion(JobValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(posting => posting.CategoryKey)
            .HasMaxLength(JobLimits.MaxCategoryKey)
            .IsRequired();

        builder.Property(posting => posting.Status)
            .HasConversion<string>()
            .HasMaxLength(JobPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(posting => posting.Visibility)
            .HasConversion<string>()
            .HasMaxLength(JobPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(posting => posting.ModerationState)
            .HasConversion<string>()
            .HasMaxLength(JobPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(posting => posting.JobType)
            .HasConversion<string>()
            .HasMaxLength(JobPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(posting => posting.ExperienceLevel)
            .HasConversion<string>()
            .HasMaxLength(JobPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(posting => posting.EducationLevel)
            .HasConversion<string>()
            .HasMaxLength(JobPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(posting => posting.ClosureReason)
            .HasMaxLength(JobLimits.MaxBulletLength);

        // The optimistic concurrency token. The aggregate bumps it on every content mutation
        // and EnsureRevision rejects a stale editor before the database has to; the token is
        // the backstop for the write that raced anyway.
        builder.Property(posting => posting.Revision).IsConcurrencyToken();

        ConfigureTitle(builder);
        ConfigureSlug(builder);
        ConfigureDescription(builder);
        ConfigureLocation(builder);
        ConfigureWorkArrangement(builder);
        ConfigureSalary(builder);
        ConfigureTrustMark(builder);
        ConfigureEngagement(builder);

        ConfigureDetailLines(builder);
        ConfigureSkills(builder);
        ConfigureTags(builder);
        ConfigureMediaReferences(builder);
        ConfigureProviderReferences(builder);
        ConfigureGuideReferences(builder);
        ConfigureScreeningQuestions(builder);
        ConfigureModerationNotes(builder);

        ConfigureIndexes(builder);

        builder.Ignore(posting => posting.DomainEvents);
        builder.Ignore(posting => posting.OwnerId);
        builder.Ignore(posting => posting.IsPublishable);
        builder.Ignore(posting => posting.IsDiscoverable);
        builder.Ignore(posting => posting.RemainingVacancies);
        builder.Ignore(posting => posting.Responsibilities);
        builder.Ignore(posting => posting.Requirements);
        builder.Ignore(posting => posting.Benefits);
    }

    private static void ConfigureTitle(EntityTypeBuilder<JobPosting> builder)
    {
        builder.OwnsOne(posting => posting.Title, title =>
        {
            title.Property(value => value.Value)
                .HasColumnName("title")
                .HasMaxLength(JobLimits.MaxTitle)
                .IsRequired();

            // Free-text search's primary target. Indexed here because the column is owned.
            title.HasIndex(value => value.Value).HasDatabaseName("ix_job_postings_title");
        });

        builder.Navigation(posting => posting.Title).IsRequired();
    }

    private static void ConfigureSlug(EntityTypeBuilder<JobPosting> builder)
    {
        builder.OwnsOne(posting => posting.Slug, slug =>
        {
            slug.Property(value => value.Value)
                .HasColumnName("slug")
                .HasMaxLength(JobLimits.MaxSlug)
                .IsRequired();

            // Unique, because it is the deep-link key: a duplicate makes one of two postings
            // permanently unreachable by link. It is also the cursor tie-break — the primary
            // key is a value-converted JobPostingId, which EF cannot compare in SQL.
            slug.HasIndex(value => value.Value)
                .IsUnique()
                .HasDatabaseName("ux_job_postings_slug");
        });

        builder.Navigation(posting => posting.Slug).IsRequired();
    }

    private static void ConfigureDescription(EntityTypeBuilder<JobPosting> builder)
    {
        builder.OwnsOne(posting => posting.Description, description =>
        {
            description.Property(value => value.Body)
                .HasColumnName("description")
                .HasMaxLength(JobLimits.MaxDescription)
                .IsRequired();

            description.Property(value => value.Summary)
                .HasColumnName("summary")
                .HasMaxLength(JobLimits.MaxSummary)
                .IsRequired();
        });

        builder.Navigation(posting => posting.Description).IsRequired();
    }

    private static void ConfigureLocation(EntityTypeBuilder<JobPosting> builder)
    {
        builder.OwnsOne(posting => posting.Location, location =>
        {
            location.Property(value => value.CountryCode)
                .HasColumnName("country_code")
                .HasMaxLength(JobPersistenceConstants.CountryCodeLength)
                .IsRequired();

            location.Property(value => value.RegionName)
                .HasColumnName("region_name")
                .HasMaxLength(JobLimits.MaxLocationName);

            location.Property(value => value.CityName)
                .HasColumnName("city_name")
                .HasMaxLength(JobLimits.MaxLocationName);

            location.Property(value => value.AreaId).HasColumnName("area_id");

            // A plain string, prefix-matchable with LIKE 'prefix%' — B4's EF lesson. The
            // repository's scope filter runs StartsWith against this column.
            location.Property(value => value.AreaPath)
                .HasColumnName("area_path")
                .HasMaxLength(JobLimits.MaxAreaPath);

            // "Jobs in Germany" is the single most common browse in the module.
            location.HasIndex(value => value.CountryCode)
                .HasDatabaseName("ix_job_postings_country");

            location.HasIndex(value => value.CityName)
                .HasDatabaseName("ix_job_postings_city");
        });

        builder.Navigation(posting => posting.Location).IsRequired();
    }

    private static void ConfigureWorkArrangement(EntityTypeBuilder<JobPosting> builder)
    {
        builder.OwnsOne(posting => posting.WorkArrangement, arrangement =>
        {
            arrangement.Property(value => value.WorkplaceType)
                .HasColumnName("workplace_type")
                .HasConversion<string>()
                .HasMaxLength(JobPersistenceConstants.EnumLength)
                .IsRequired();

            arrangement.Property(value => value.OffersVisaSponsorship)
                .HasColumnName("offers_visa_sponsorship")
                .IsRequired();

            arrangement.Property(value => value.OffersRelocationAssistance)
                .HasColumnName("offers_relocation_assistance")
                .IsRequired();

            arrangement.Property(value => value.OnSiteDaysPerWeek)
                .HasColumnName("on_site_days_per_week");

            arrangement.Property(value => value.RemotePolicy)
                .HasColumnName("remote_policy")
                .HasMaxLength(1_000);

            // The two filters this platform's members reach for first, and both are owned
            // columns — so the indexes live here, where EF can actually build them.
            arrangement.HasIndex(value => value.WorkplaceType)
                .HasDatabaseName("ix_job_postings_workplace_type");

            arrangement.HasIndex(value => value.OffersVisaSponsorship)
                .HasDatabaseName("ix_job_postings_visa_sponsorship");
        });

        builder.Navigation(posting => posting.WorkArrangement).IsRequired();
    }

    private static void ConfigureSalary(EntityTypeBuilder<JobPosting> builder)
    {
        builder.OwnsOne(posting => posting.Salary, salary =>
        {
            salary.Property(value => value.IsDisclosed)
                .HasColumnName("salary_disclosed")
                .IsRequired();

            salary.Property(value => value.Minimum)
                .HasColumnName("salary_minimum")
                .HasColumnType(JobPersistenceConstants.MoneyColumnType);

            salary.Property(value => value.Maximum)
                .HasColumnName("salary_maximum")
                .HasColumnType(JobPersistenceConstants.MoneyColumnType);

            salary.Property(value => value.CurrencyCode)
                .HasColumnName("salary_currency")
                .HasMaxLength(JobPersistenceConstants.CurrencyCodeLength);

            salary.Property(value => value.Period)
                .HasColumnName("salary_period")
                .HasConversion<string>()
                .HasMaxLength(JobPersistenceConstants.EnumLength);

            salary.Property(value => value.IsNegotiable)
                .HasColumnName("salary_negotiable")
                .IsRequired();

            // A salary-range filter is always paired with a currency, because nothing in this
            // module converts between them. The composite index is what makes that pairing
            // cheap rather than a scan.
            salary.HasIndex(value => new { value.CurrencyCode, value.Maximum })
                .HasDatabaseName("ix_job_postings_salary");
        });

        builder.Navigation(posting => posting.Salary).IsRequired();
    }

    private static void ConfigureTrustMark(EntityTypeBuilder<JobPosting> builder)
    {
        builder.OwnsOne(posting => posting.TrustMark, mark =>
        {
            mark.Property(value => value.State)
                .HasColumnName("verification_state")
                .HasConversion<string>()
                .HasMaxLength(JobPersistenceConstants.EnumLength)
                .IsRequired();

            mark.Property(value => value.VerificationId).HasColumnName("verification_id");

            mark.Property(value => value.Badge)
                .HasColumnName("verification_badge")
                .HasMaxLength(JobPersistenceConstants.BadgeLength);

            mark.Property(value => value.Reason)
                .HasColumnName("verification_reason")
                .HasMaxLength(JobPersistenceConstants.ReasonLength);

            mark.Property(value => value.VerifiedAt).HasColumnName("verified_at");

            // The staleness guard for at-least-once, unordered Trust deliveries. Persisted,
            // because the guard is only as durable as the timestamp it compares against.
            mark.Property(value => value.ChangedAt).HasColumnName("verification_changed_at");

            // How a Trust outcome finds the posting that raised the claim.
            mark.HasIndex(value => value.VerificationId)
                .HasDatabaseName("ix_job_postings_verification_id");
        });

        builder.Navigation(posting => posting.TrustMark).IsRequired();
    }

    private static void ConfigureEngagement(EntityTypeBuilder<JobPosting> builder)
    {
        builder.OwnsOne(posting => posting.Engagement, engagement =>
        {
            engagement.Property(value => value.ViewCount).HasColumnName("view_count");
            engagement.Property(value => value.UniqueViewCount).HasColumnName("unique_view_count");
            engagement.Property(value => value.ApplicationCount).HasColumnName("application_count");
            engagement.Property(value => value.SaveCount).HasColumnName("save_count");
            engagement.Property(value => value.ShareCount).HasColumnName("share_count");
            engagement.Property(value => value.SearchImpressions).HasColumnName("search_impressions");
            engagement.Property(value => value.SearchClicks).HasColumnName("search_clicks");

            // The "most applied" sort.
            engagement.HasIndex(value => value.ApplicationCount)
                .HasDatabaseName("ix_job_postings_application_count");
        });

        builder.Navigation(posting => posting.Engagement).IsRequired();
    }

    /// <summary>
    /// Responsibilities, requirements and benefits, in one table with a kind discriminator.
    /// See <c>JobDetailKind</c> for why the three lists share a type.
    /// </summary>
    private static void ConfigureDetailLines(EntityTypeBuilder<JobPosting> builder) =>
        builder.OwnsMany(posting => posting.DetailLines, line =>
        {
            line.ToTable(
                JobPersistenceConstants.JobDetailLinesTable, JobsModuleMetadata.Schema);

            line.HasKey(value => value.LineId);
            line.Property(value => value.LineId).ValueGeneratedNever();

            line.Property(value => value.Kind)
                .HasConversion<string>()
                .HasMaxLength(JobPersistenceConstants.EnumLength)
                .IsRequired();

            line.Property(value => value.Text)
                .HasMaxLength(JobLimits.MaxBulletLength)
                .IsRequired();

            line.Property(value => value.Order).IsRequired();
            line.Property(value => value.IsEssential).IsRequired();
        });

    private static void ConfigureSkills(EntityTypeBuilder<JobPosting> builder) =>
        builder.OwnsMany(posting => posting.Skills, skill =>
        {
            skill.ToTable(
                JobPersistenceConstants.JobSkillRequirementsTable, JobsModuleMetadata.Schema);

            // The key is (posting, skill) rather than a surrogate: a posting cannot ask for the
            // same skill twice, and the composite key is what makes that true in the database
            // as well as in the aggregate.
            skill.WithOwner().HasForeignKey("posting_id");
            skill.HasKey("posting_id", nameof(Domain.Postings.JobSkillRequirement.SkillKey));

            skill.Property(value => value.SkillKey)
                .HasMaxLength(JobLimits.MaxSkillKey)
                .IsRequired();

            skill.Property(value => value.DisplayName)
                .HasMaxLength(JobLimits.MaxDisplayName)
                .IsRequired();

            skill.Property(value => value.MinimumProficiency)
                .HasConversion<string>()
                .HasMaxLength(JobPersistenceConstants.EnumLength)
                .IsRequired();

            skill.Property(value => value.IsEssential).IsRequired();
            skill.Property(value => value.Order).IsRequired();

            // "Jobs needing this skill" is a first-class browse.
            skill.HasIndex(value => value.SkillKey)
                .HasDatabaseName("ix_job_skill_requirements_key");
        });

    private static void ConfigureTags(EntityTypeBuilder<JobPosting> builder) =>
        builder.OwnsMany(posting => posting.Tags, tag =>
        {
            tag.ToTable(JobPersistenceConstants.JobTagsTable, JobsModuleMetadata.Schema);

            tag.WithOwner().HasForeignKey("posting_id");
            tag.HasKey("posting_id", nameof(Domain.Postings.JobTagAssignment.Slug));

            tag.Property(value => value.Slug)
                .HasMaxLength(JobLimits.MaxTagLength)
                .IsRequired();

            tag.Property(value => value.Order).IsRequired();

            tag.HasIndex(value => value.Slug).HasDatabaseName("ix_job_tags_slug");
        });

    private static void ConfigureMediaReferences(EntityTypeBuilder<JobPosting> builder) =>
        builder.OwnsMany(posting => posting.MediaReferences, reference =>
        {
            reference.ToTable(
                JobPersistenceConstants.JobMediaReferencesTable, JobsModuleMetadata.Schema);

            // (media, role) is the identity the aggregate treats as unique, so it is the key.
            reference.WithOwner().HasForeignKey("posting_id");
            reference.HasKey(
                "posting_id",
                nameof(Domain.Postings.JobMediaReference.MediaId),
                nameof(Domain.Postings.JobMediaReference.Role));

            reference.Property(value => value.Role)
                .HasConversion<string>()
                .HasMaxLength(JobPersistenceConstants.EnumLength)
                .IsRequired();

            reference.Property(value => value.Caption).HasMaxLength(JobLimits.MaxDisplayName);
            reference.Property(value => value.Order).IsRequired();
        });

    private static void ConfigureProviderReferences(EntityTypeBuilder<JobPosting> builder) =>
        builder.OwnsMany(posting => posting.ProviderReferences, reference =>
        {
            reference.ToTable(
                JobPersistenceConstants.JobProviderReferencesTable, JobsModuleMetadata.Schema);

            reference.WithOwner().HasForeignKey("posting_id");
            reference.HasKey(
                "posting_id", nameof(Domain.Postings.JobProviderReference.ProviderId));

            // The identifier half of this key is supplied by the caller, and EF marks a Guid key
            // property store-generated by convention. Left alone, EF reads a set key as "this row
            // already exists" and emits an UPDATE where an INSERT was meant, which surfaces as a
            // bogus concurrency conflict rather than a missing row.
            reference.Property(value => value.ProviderId).ValueGeneratedNever();

            reference.Property(value => value.Role).HasMaxLength(JobLimits.MaxDisplayName);
            reference.Property(value => value.Note).HasMaxLength(JobLimits.MaxBulletLength);
            reference.Property(value => value.Order).IsRequired();
        });

    private static void ConfigureGuideReferences(EntityTypeBuilder<JobPosting> builder) =>
        builder.OwnsMany(posting => posting.GuideReferences, reference =>
        {
            reference.ToTable(
                JobPersistenceConstants.JobGuideReferencesTable, JobsModuleMetadata.Schema);

            reference.WithOwner().HasForeignKey("posting_id");
            reference.HasKey("posting_id", nameof(Domain.Postings.JobGuideReference.GuideId));

            // Caller-supplied key half. See the provider references above.
            reference.Property(value => value.GuideId).ValueGeneratedNever();

            reference.Property(value => value.Label).HasMaxLength(JobLimits.MaxDisplayName);
            reference.Property(value => value.IsAutomatic).IsRequired();
            reference.Property(value => value.Order).IsRequired();
        });

    private static void ConfigureScreeningQuestions(EntityTypeBuilder<JobPosting> builder) =>
        builder.OwnsMany(posting => posting.ScreeningQuestions, question =>
        {
            question.ToTable(
                JobPersistenceConstants.JobScreeningQuestionsTable, JobsModuleMetadata.Schema);

            // The identifier is editor-supplied and stable across reorders — an answer points
            // at it — so it is the key, and ValueGeneratedNever is load-bearing. See the class
            // summary.
            question.HasKey(value => value.QuestionId);
            question.Property(value => value.QuestionId).ValueGeneratedNever();

            question.Property(value => value.Text)
                .HasMaxLength(JobLimits.MaxQuestionLength)
                .IsRequired();

            question.Property(value => value.IsRequired).IsRequired();
            question.Property(value => value.Order).IsRequired();
        });

    private static void ConfigureModerationNotes(EntityTypeBuilder<JobPosting> builder) =>
        builder.OwnsMany(posting => posting.ModerationNotes, note =>
        {
            note.ToTable(
                JobPersistenceConstants.JobModerationNotesTable, JobsModuleMetadata.Schema);

            note.HasKey(value => value.NoteId);
            note.Property(value => value.NoteId).ValueGeneratedNever();

            note.Property(value => value.AuthorId)
                .HasConversion(JobValueConverters.Id<UserId>())
                .IsRequired();

            note.Property(value => value.Note)
                .HasMaxLength(JobLimits.MaxModerationNote)
                .IsRequired();

            note.Property(value => value.CreatedAt).IsRequired();
        });

    private static void ConfigureIndexes(EntityTypeBuilder<JobPosting> builder)
    {
        // The employer dashboard's every tab.
        builder.HasIndex(posting => new { posting.EmployerId, posting.Status })
            .HasDatabaseName("ix_job_postings_employer_status");

        // The default browse: newest published first, within a status.
        builder.HasIndex(posting => new { posting.Status, posting.PublishedAt })
            .HasDatabaseName("ix_job_postings_status_published");

        builder.HasIndex(posting => posting.CategoryKey)
            .HasDatabaseName("ix_job_postings_category");

        // The expiry sweep's work list.
        builder.HasIndex(posting => posting.ClosingAt)
            .HasDatabaseName("ix_job_postings_closing_at");

        // The alert sweep reads by publication instant.
        builder.HasIndex(posting => posting.PublishedAt)
            .HasDatabaseName("ix_job_postings_published_at");

        builder.HasIndex(posting => posting.IsFeatured)
            .HasDatabaseName("ix_job_postings_featured");

        builder.HasIndex(posting => posting.ModerationState)
            .HasDatabaseName("ix_job_postings_moderation_state");
    }
}
