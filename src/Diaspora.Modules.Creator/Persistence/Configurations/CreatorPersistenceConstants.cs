using Diaspora.Persistence;
using Diaspora.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Diaspora.Modules.Creator.Persistence.Configurations;

/// <summary>
/// The module's table names and column widths, in one place.
/// <para>
/// Named here rather than inline so a migration and a query cannot disagree about which table a
/// thing lives in, and so renaming one is a single edit rather than a search.
/// </para>
/// </summary>
internal static class CreatorPersistenceConstants
{
    // ── Creator projection ──────────────────────────────────────────────────────────────────

    public const string CreatorsTable = "creator_profiles";
    public const string CreatorTagsTable = "creator_profile_tags";

    // ── Verification ────────────────────────────────────────────────────────────────────────

    public const string VerificationsTable = "creator_verification_applications";
    public const string VerificationEvidenceTable = "creator_verification_evidence";
    public const string VerificationAcknowledgedTable = "creator_verification_acknowledged";
    public const string VerificationRequestsTable = "creator_verification_information_requests";
    public const string VerificationDecisionsTable = "creator_verification_decisions";

    // ── Milestones ──────────────────────────────────────────────────────────────────────────

    public const string MilestonesTable = "creator_milestones";
    public const string MilestoneRulesTable = "creator_milestone_rules";
    public const string MilestoneProgressTable = "creator_milestone_progress";
    public const string MilestoneProgressRulesTable = "creator_milestone_progress_rules";

    // ── Challenges ──────────────────────────────────────────────────────────────────────────

    public const string ChallengesTable = "creator_challenges";
    public const string ChallengeTasksTable = "creator_challenge_tasks";
    public const string ChallengeRulesTable = "creator_challenge_rules";
    public const string ChallengeCountriesTable = "creator_challenge_countries";
    public const string EnrollmentsTable = "creator_challenge_enrollments";
    public const string EnrollmentTasksTable = "creator_challenge_task_progress";

    // ── Grants ──────────────────────────────────────────────────────────────────────────────

    public const string GrantsTable = "community_grants";
    public const string GrantFieldsTable = "community_grant_fields";
    public const string GrantFieldOptionsTable = "community_grant_field_options";
    public const string GrantDeclarationsTable = "community_grant_declarations";
    public const string GrantDeliverablesTable = "community_grant_deliverables";
    public const string GrantCountriesTable = "community_grant_countries";
    public const string GrantApplicationsTable = "grant_applications";
    public const string GrantAnswersTable = "grant_application_answers";
    public const string GrantAttachmentsTable = "grant_application_attachments";
    public const string GrantAcceptedDeclarationsTable = "grant_application_declarations";
    public const string GrantReportsTable = "grant_application_reports";
    public const string GrantRequestsTable = "grant_application_information_requests";
    public const string GrantDecisionsTable = "grant_application_decisions";

    // ── Academy ─────────────────────────────────────────────────────────────────────────────

    public const string SectionsTable = "academy_sections";
    public const string CoursesTable = "academy_courses";
    public const string CourseTagsTable = "academy_course_tags";
    public const string CoursePrerequisitesTable = "academy_course_prerequisites";
    public const string LessonsTable = "academy_lessons";
    public const string LessonBlocksTable = "academy_lesson_blocks";
    public const string LessonBlockItemsTable = "academy_lesson_block_items";
    public const string QuizzesTable = "academy_quizzes";
    public const string QuizQuestionsTable = "academy_quiz_questions";
    public const string QuizOptionsTable = "academy_quiz_options";
    public const string LearningProgressTable = "creator_learning_progress";
    public const string CompletedLessonsTable = "creator_completed_lessons";
    public const string QuizAttemptsTable = "creator_quiz_attempts";
    public const string CertificatesTable = "creator_certificates";

    // ── Referrals and rewards ───────────────────────────────────────────────────────────────

    public const string ReferralCodesTable = "creator_referral_codes";
    public const string ReferralsTable = "creator_referrals";
    public const string SignupBonusesTable = "creator_signup_bonuses";
    public const string PointRulesTable = "creator_point_rules";
    public const string RewardsTable = "creator_rewards";
    public const string RedemptionsTable = "creator_reward_redemptions";

    // ── Activity ────────────────────────────────────────────────────────────────────────────

    public const string ActivityTable = "creator_activity_entries";

    /// <summary>One recorded earning and what it was for. See <c>CreatorEarning</c>.</summary>
    public const string EarningsTable = "creator_earnings";

    // ── Widths ──────────────────────────────────────────────────────────────────────────────

    /// <summary>Enum names are stored as strings, so a value added later reads correctly on old rows.</summary>
    public const int EnumLength = 32;

    public const int KeyLength = 128;
    public const int CodeLength = 32;
    public const int LabelLength = 200;
    public const int SummaryLength = 1000;
    public const int DescriptionLength = 4000;
    public const int TextLength = 8000;
    public const int UrlLength = 2048;
    public const int CountryLength = 8;
    public const int CurrencyLength = 3;
    public const int ReferenceLength = 256;

    /// <summary>
    /// The idempotency key column. Scoped by operation prefix, so 150 characters of caller key plus
    /// the prefix fits comfortably.
    /// </summary>
    public const int IdempotencyKeyLength = 200;
}

/// <summary>
/// The module's identifier converters.
/// <para>
/// One helper rather than a converter type per identifier, exactly as every module since B7 does.
/// The generic converter is the platform's, so a strongly typed identifier persists as its Guid
/// everywhere on the platform and no module invents a second encoding.
/// </para>
/// </summary>
internal static class CreatorValueConverters
{
    public static ValueConverter Id<TId>() where TId : StronglyTypedId =>
        new StronglyTypedIdValueConverter<TId>();
}
