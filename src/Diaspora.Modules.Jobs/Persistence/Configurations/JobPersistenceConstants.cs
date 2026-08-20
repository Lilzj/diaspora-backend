namespace Diaspora.Modules.Jobs.Persistence.Configurations;

/// <summary>
/// Table names and column widths every Jobs configuration shares.
/// <para>
/// <b>Every table name is <c>job</c>-, <c>employer</c>- or <c>candidate</c>-prefixed, and not
/// by accident.</b> SQLite — which the integration suite runs on — ignores schemas entirely, so
/// two modules whose tables differ only by schema collide there and nowhere else, which is the
/// worst possible place for a name clash to first appear. <c>categories</c>, <c>skills</c>,
/// <c>applications</c>, <c>offers</c> and <c>analytics</c> are all names another module will
/// eventually want; the prefix is what stops the collision ever happening. B6, B7 and B8 each
/// record the same trap.
/// </para>
/// </summary>
internal static class JobPersistenceConstants
{
    // ── Posting aggregate ───────────────────────────────────────────────────────────────────

    public const string JobPostingsTable = "job_postings";
    public const string JobDetailLinesTable = "job_detail_lines";
    public const string JobSkillRequirementsTable = "job_skill_requirements";
    public const string JobTagsTable = "job_tags";
    public const string JobMediaReferencesTable = "job_media_references";
    public const string JobProviderReferencesTable = "job_provider_references";
    public const string JobGuideReferencesTable = "job_guide_references";
    public const string JobScreeningQuestionsTable = "job_screening_questions";
    public const string JobModerationNotesTable = "job_moderation_notes";

    // ── Employer aggregate ──────────────────────────────────────────────────────────────────

    public const string EmployerProfilesTable = "job_employer_profiles";
    public const string EmployerMembersTable = "job_employer_members";
    public const string EmployerHiringRegionsTable = "job_employer_hiring_regions";

    // ── Candidate aggregate ─────────────────────────────────────────────────────────────────

    public const string CandidateProfilesTable = "job_candidate_profiles";
    public const string CandidateSkillsTable = "job_candidate_skills";
    public const string CandidateExperienceTable = "job_candidate_experience";
    public const string CandidateEducationTable = "job_candidate_education";
    public const string CandidateCertificationsTable = "job_candidate_certifications";
    public const string CandidateLanguagesTable = "job_candidate_languages";
    public const string CandidateWorkAuthorizationsTable = "job_candidate_work_authorizations";
    public const string CandidatePreferredLocationsTable = "job_candidate_preferred_locations";
    public const string CandidatePortfolioTable = "job_candidate_portfolio";

    // ── Standalone aggregates ───────────────────────────────────────────────────────────────

    public const string ResumesTable = "job_resumes";
    public const string ApplicationsTable = "job_applications";
    public const string ApplicationAnswersTable = "job_application_answers";
    public const string ApplicationAttachmentsTable = "job_application_attachments";
    public const string ApplicationHistoryTable = "job_application_history";
    public const string InterviewsTable = "job_interviews";
    public const string InterviewParticipantsTable = "job_interview_participants";
    public const string OffersTable = "job_offers";
    public const string PipelinesTable = "job_hiring_pipelines";
    public const string PipelineStagesTable = "job_hiring_stages";
    public const string CategoriesTable = "job_categories";
    public const string SkillsTable = "job_skills";
    public const string SkillAliasesTable = "job_skill_aliases";
    public const string SavedJobsTable = "job_saved_jobs";
    public const string AlertsTable = "job_alerts";
    public const string AnalyticsTable = "job_analytics";

    // ── Column widths ───────────────────────────────────────────────────────────────────────

    /// <summary>Every enum is stored as its name, not its number. Readable in a query window,
    /// and safe against someone reordering the enum.</summary>
    public const int EnumLength = 32;

    public const int CountryCodeLength = 2;

    public const int LanguageCodeLength = 2;

    public const int CurrencyCodeLength = 3;

    /// <summary>Trust's badge label, projected. Rendered, never interpreted.</summary>
    public const int BadgeLength = 100;

    /// <summary>The reason on a trust mark. Shown to the owner, never to candidates.</summary>
    public const int ReasonLength = 500;

    /// <summary>A MIME type as Media reported it.</summary>
    public const int ContentTypeLength = 200;

    /// <summary>An IANA time-zone identifier.</summary>
    public const int TimeZoneLength = 100;

    /// <summary>
    /// The applicant country tally on the analytics rollup: up to sixty entries of
    /// <c>XX:nnnnn;</c>. Generous, because running out silently would lose the distribution.
    /// </summary>
    public const int CountryTallyLength = 1_000;

    /// <summary>
    /// The delimited criteria lists on an alert — keywords, countries, skill keys. Each is
    /// bounded by <c>JobLimits</c> at ten or fewer short entries; this leaves room to spare.
    /// </summary>
    public const int AlertListLength = 1_000;

    /// <summary>
    /// Money. 18 digits with 2 decimals: enough for a yearly salary in any currency the
    /// platform will see, including ones with very small units.
    /// </summary>
    public const string MoneyColumnType = "decimal(18,2)";
}
