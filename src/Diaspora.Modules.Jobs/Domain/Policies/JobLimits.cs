namespace Diaspora.Modules.Jobs.Domain.Policies;

/// <summary>
/// The ceilings the aggregates enforce.
/// <para>
/// Constants in the domain rather than options in configuration, for the reason B6, B7 and B8
/// each record for their own limits: a bound that can be raised per deployment is a bound the
/// domain cannot rely on, and every invariant depending on it becomes conditional. These are
/// the numbers the aggregates are correct at.
/// </para>
/// <para>
/// Every one is generous. They exist to bound the size of a single aggregate so loading one
/// posting or one application is a predictable amount of work, and to stop a scripted client
/// writing forty thousand rows before anyone notices. None is a business rule dressed as a
/// limit — the two that come closest, <see cref="MaxActivePostingsPerEmployer"/> and
/// <see cref="MaxApplicationsPerCandidatePerDay"/>, are abuse bounds and live in
/// <c>JobOptions</c> instead, where an operator can raise them for a deployment that needs it.
/// </para>
/// </summary>
public static class JobLimits
{
    // ── Posting text ────────────────────────────────────────────────────────────────────────

    /// <summary>The shortest title that can mean anything.</summary>
    public const int MinTitle = 3;

    /// <summary>Characters in a posting title.</summary>
    public const int MaxTitle = 200;

    /// <summary>Characters in a slug.</summary>
    public const int MaxSlug = 220;

    /// <summary>Characters in the posting body. Long, because some roles genuinely need it.</summary>
    public const int MaxDescription = 30_000;

    /// <summary>Characters in the one-line summary a card renders.</summary>
    public const int MaxSummary = 500;

    /// <summary>Characters in one responsibility, requirement or benefit line.</summary>
    public const int MaxBulletLength = 500;

    /// <summary>Responsibility lines on one posting.</summary>
    public const int MaxResponsibilities = 50;

    /// <summary>Requirement lines on one posting.</summary>
    public const int MaxRequirements = 50;

    /// <summary>Benefit lines on one posting.</summary>
    public const int MaxBenefits = 50;

    /// <summary>Skills named on one posting.</summary>
    public const int MaxSkills = 40;

    /// <summary>Tags on one posting. The client renders these as chips.</summary>
    public const int MaxTags = 20;

    /// <summary>Characters in a tag slug.</summary>
    public const int MaxTagLength = 60;

    /// <summary>Characters in a category key.</summary>
    public const int MaxCategoryKey = 64;

    /// <summary>Characters in a skill key.</summary>
    public const int MaxSkillKey = 64;

    /// <summary>Characters in a display name — a skill, a category, a stage.</summary>
    public const int MaxDisplayName = 120;

    /// <summary>Characters in a region, city or area name Location did not resolve.</summary>
    public const int MaxLocationName = 200;

    /// <summary>Characters in a materialised area path. Prefix-matched with LIKE.</summary>
    public const int MaxAreaPath = 400;

    // ── Posting structure ───────────────────────────────────────────────────────────────────

    /// <summary>Media assets referenced from one posting.</summary>
    public const int MaxMediaReferences = 20;

    /// <summary>Providers referenced from one posting — agencies, coaches, consultants.</summary>
    public const int MaxProviderReferences = 10;

    /// <summary>Guides linked beside one posting — work permits, interview tips.</summary>
    public const int MaxGuideReferences = 10;

    /// <summary>Screening questions on one posting.</summary>
    public const int MaxScreeningQuestions = 20;

    /// <summary>Characters in a screening question.</summary>
    public const int MaxQuestionLength = 500;

    /// <summary>Vacancies one posting may advertise.</summary>
    public const int MaxVacancies = 9_999;

    /// <summary>Moderation notes retained on one posting.</summary>
    public const int MaxModerationNotes = 200;

    /// <summary>Characters in a moderation note.</summary>
    public const int MaxModerationNote = 2_000;

    // ── Employer ────────────────────────────────────────────────────────────────────────────

    /// <summary>Characters in an employer's display name.</summary>
    public const int MaxEmployerName = 200;

    /// <summary>The shortest employer name that can mean anything.</summary>
    public const int MinEmployerName = 2;

    /// <summary>Characters in an employer's description.</summary>
    public const int MaxEmployerDescription = 10_000;

    /// <summary>Characters in a company registration number.</summary>
    public const int MaxRegistrationNumber = 100;

    /// <summary>Countries one employer declares it hires into.</summary>
    public const int MaxHiringRegions = 60;

    /// <summary>Members on one employer.</summary>
    public const int MaxEmployerMembers = 200;

    /// <summary>Characters in a URL — careers page, website, joining link.</summary>
    public const int MaxUrl = 2_000;

    // ── Candidate ───────────────────────────────────────────────────────────────────────────

    /// <summary>Characters in a candidate's headline.</summary>
    public const int MaxHeadline = 200;

    /// <summary>Characters in a candidate's summary.</summary>
    public const int MaxCandidateSummary = 5_000;

    /// <summary>Skills on one candidate profile.</summary>
    public const int MaxCandidateSkills = 60;

    /// <summary>Positions in a work history.</summary>
    public const int MaxExperience = 40;

    /// <summary>Characters in one position's description.</summary>
    public const int MaxExperienceDescription = 4_000;

    /// <summary>Qualifications in an education history.</summary>
    public const int MaxEducation = 20;

    /// <summary>Certifications on one profile.</summary>
    public const int MaxCertifications = 40;

    /// <summary>Languages on one profile.</summary>
    public const int MaxLanguages = 20;

    /// <summary>Work authorisations — one per country the candidate can work in.</summary>
    public const int MaxWorkAuthorizations = 30;

    /// <summary>Preferred locations on one profile.</summary>
    public const int MaxPreferredLocations = 20;

    /// <summary>Portfolio links on one profile.</summary>
    public const int MaxPortfolioItems = 20;

    /// <summary>Résumé versions one candidate may keep.</summary>
    public const int MaxResumes = 10;

    /// <summary>Characters in a résumé label.</summary>
    public const int MaxResumeLabel = 120;

    /// <summary>Characters in an organisation, institution or role name.</summary>
    public const int MaxOrganizationName = 200;

    // ── Applications ────────────────────────────────────────────────────────────────────────

    /// <summary>Characters in a typed cover letter.</summary>
    public const int MaxCoverLetter = 10_000;

    /// <summary>Answers to screening questions on one application.</summary>
    public const int MaxAnswers = 20;

    /// <summary>Characters in one answer.</summary>
    public const int MaxAnswerLength = 2_000;

    /// <summary>Attachments on one application.</summary>
    public const int MaxAttachments = 10;

    /// <summary>
    /// History entries retained on one application. Reached only by a pathological process;
    /// a real hiring cycle produces a few dozen.
    /// </summary>
    public const int MaxHistoryEntries = 1_000;

    /// <summary>Characters in an internal note on an application.</summary>
    public const int MaxApplicationNote = 4_000;

    // ── Interviews & offers ─────────────────────────────────────────────────────────────────

    /// <summary>Interview rounds against one application.</summary>
    public const int MaxInterviewsPerApplication = 20;

    /// <summary>The shortest interview the platform will record, in minutes.</summary>
    public const int MinInterviewMinutes = 5;

    /// <summary>The longest interview the platform will record, in minutes — a full day.</summary>
    public const int MaxInterviewMinutes = 8 * 60;

    /// <summary>Characters in interview instructions or an outcome note.</summary>
    public const int MaxInterviewNote = 4_000;

    /// <summary>Interviewers named on one interview.</summary>
    public const int MaxInterviewers = 20;

    /// <summary>Characters in an offer note.</summary>
    public const int MaxOfferNote = 4_000;

    // ── Pipeline ────────────────────────────────────────────────────────────────────────────

    /// <summary>Stages in one hiring pipeline.</summary>
    public const int MaxStages = 20;

    /// <summary>Characters in a stage key.</summary>
    public const int MaxStageKey = 64;

    // ── Alerts & saved jobs ─────────────────────────────────────────────────────────────────

    /// <summary>Standing alerts one candidate may keep.</summary>
    public const int MaxAlertsPerCandidate = 20;

    /// <summary>Characters in an alert's name.</summary>
    public const int MaxAlertName = 120;

    /// <summary>Keywords in one alert.</summary>
    public const int MaxAlertKeywords = 10;

    /// <summary>Countries one alert watches.</summary>
    public const int MaxAlertCountries = 10;

    /// <summary>Jobs one candidate may save.</summary>
    public const int MaxSavedJobs = 500;

    /// <summary>Characters in a note on a saved job.</summary>
    public const int MaxSavedJobNote = 1_000;

    // ── Paging ──────────────────────────────────────────────────────────────────────────────

    /// <summary>The largest page any listing endpoint will return.</summary>
    public const int MaxPageSize = 50;

    /// <summary>The page size a listing returns when the caller does not ask.</summary>
    public const int DefaultPageSize = 20;

    /// <summary>The shortest search term worth running.</summary>
    public const int MinSearchTerm = 2;

    /// <summary>Characters in a search term.</summary>
    public const int MaxSearchTerm = 200;
}
