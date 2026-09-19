namespace Diaspora.Modules.Jobs;

/// <summary>
/// The stable wire-level error codes the Jobs module emits.
/// <para>
/// These strings are frozen contract: clients switch on them, support searches logs by them,
/// and translations key off them. Change the description on an <c>Error</c> freely; never
/// change a code. New failures get new codes.
/// </para>
/// </summary>
public static class JobErrorCodes
{
    // ── Existence & visibility ──────────────────────────────────────────────────────────────

    public const string PostingNotFound = "jobs.posting.not-found";
    public const string EmployerNotFound = "jobs.employer.not-found";
    public const string CandidateNotFound = "jobs.candidate.not-found";
    public const string ApplicationNotFound = "jobs.application.not-found";
    public const string InterviewNotFound = "jobs.interview.not-found";
    public const string OfferNotFound = "jobs.offer.not-found";
    public const string ResumeNotFound = "jobs.resume.not-found";
    public const string CategoryNotFound = "jobs.category.not-found";
    public const string SkillNotFound = "jobs.skill.not-found";
    public const string AlertNotFound = "jobs.alert.not-found";
    public const string SavedJobNotFound = "jobs.saved.not-found";
    public const string PipelineNotFound = "jobs.pipeline.not-found";
    public const string StageNotFound = "jobs.stage.not-found";
    public const string PostingHidden = "jobs.posting.hidden";
    public const string PostingDeleted = "jobs.posting.deleted";
    public const string DraftNotVisible = "jobs.draft.not-visible";

    // ── Authorization & ownership ───────────────────────────────────────────────────────────

    public const string NotAuthenticated = "jobs.not-authenticated";
    public const string NotEmployer = "jobs.not-employer";
    public const string NotRecruiter = "jobs.not-recruiter";
    public const string NotCandidate = "jobs.not-candidate";
    public const string NotApplicant = "jobs.not-applicant";
    public const string NotModerator = "jobs.not-moderator";
    public const string EmployerNotOwned = "jobs.employer.not-owned";
    public const string PostingNotOwned = "jobs.posting.not-owned";

    // ── Concurrency ─────────────────────────────────────────────────────────────────────────

    public const string RevisionConflict = "jobs.revision-conflict";

    // ── Posting lifecycle ───────────────────────────────────────────────────────────────────

    public const string PostingNotPublishable = "jobs.posting.not-publishable";
    public const string PostingAlreadyPublished = "jobs.posting.already-published";
    public const string PostingNotPublished = "jobs.posting.not-published";
    public const string PostingNotOpen = "jobs.posting.not-open";
    public const string PostingClosed = "jobs.posting.closed";
    public const string PostingExpired = "jobs.posting.expired";
    public const string PostingFilled = "jobs.posting.filled";
    public const string PostingNotArchived = "jobs.posting.not-archived";
    public const string PostingArchivedImmutable = "jobs.posting.archived-immutable";
    public const string PostingNotDraft = "jobs.posting.not-draft";
    public const string PostingNotDeleted = "jobs.posting.not-deleted";
    public const string ClosingDateInPast = "jobs.posting.closing-date.in-past";
    public const string NoVacanciesRemaining = "jobs.posting.no-vacancies";

    // ── Posting content ─────────────────────────────────────────────────────────────────────

    public const string TitleInvalid = "jobs.title.invalid";
    public const string SlugInvalid = "jobs.slug.invalid";
    public const string SlugTaken = "jobs.slug.taken";
    public const string DescriptionInvalid = "jobs.description.invalid";
    public const string SummaryTooLong = "jobs.summary.too-long";
    public const string ResponsibilityInvalid = "jobs.responsibility.invalid";
    public const string ResponsibilityLimit = "jobs.responsibilities.limit";
    public const string RequirementInvalid = "jobs.requirement.invalid";
    public const string RequirementLimit = "jobs.requirements.limit";
    public const string BenefitInvalid = "jobs.benefit.invalid";
    public const string BenefitLimit = "jobs.benefits.limit";
    public const string SkillInvalid = "jobs.skill.invalid";
    public const string SkillLimit = "jobs.skills.limit";
    public const string TagInvalid = "jobs.tag.invalid";
    public const string TagLimit = "jobs.tags.limit";
    public const string MediaInvalid = "jobs.media.invalid";
    public const string MediaLimit = "jobs.media.limit";
    public const string CountryInvalid = "jobs.country.invalid";
    public const string LocationInvalid = "jobs.location.invalid";
    public const string CurrencyInvalid = "jobs.currency.invalid";
    public const string SalaryInvalid = "jobs.salary.invalid";
    public const string SalaryRangeInverted = "jobs.salary.range-inverted";
    public const string VacanciesInvalid = "jobs.vacancies.invalid";
    public const string WorkplaceInvalid = "jobs.workplace.invalid";
    public const string CategoryKeyInvalid = "jobs.category.key-invalid";
    public const string ModerationNoteInvalid = "jobs.moderation-note.invalid";

    public const string ReportNotFound = "jobs.report.not-found";
    public const string ReportAlreadyResolved = "jobs.report.already-resolved";
    public const string ReportReasonInvalid = "jobs.report.reason-invalid";
    public const string CannotReportOwnPosting = "jobs.report.own-posting";
    public const string AlreadyReported = "jobs.report.duplicate";
    public const string ProviderReferenceInvalid = "jobs.provider-reference.invalid";
    public const string ProviderReferenceLimit = "jobs.provider-references.limit";
    public const string GuideReferenceInvalid = "jobs.guide-reference.invalid";
    public const string GuideReferenceLimit = "jobs.guide-references.limit";

    // ── Employer ────────────────────────────────────────────────────────────────────────────

    public const string EmployerNameInvalid = "jobs.employer.name.invalid";
    public const string EmployerSlugTaken = "jobs.employer.slug.taken";
    public const string EmployerAlreadyExists = "jobs.employer.already-exists";
    public const string EmployerNotVerified = "jobs.employer.not-verified";
    public const string EmployerSuspended = "jobs.employer.suspended";
    public const string EmployerContactInvalid = "jobs.employer.contact.invalid";
    public const string EmployerRegionInvalid = "jobs.employer.region.invalid";
    public const string EmployerRegionLimit = "jobs.employer.regions.limit";
    public const string RecruiterAlreadyMember = "jobs.employer.recruiter.already-member";
    public const string RecruiterNotMember = "jobs.employer.recruiter.not-member";
    public const string CannotRemoveLastOwner = "jobs.employer.owner.last";

    // ── Candidate ───────────────────────────────────────────────────────────────────────────

    public const string CandidateAlreadyExists = "jobs.candidate.already-exists";
    public const string HeadlineInvalid = "jobs.candidate.headline.invalid";
    public const string ExperienceInvalid = "jobs.candidate.experience.invalid";
    public const string ExperienceLimit = "jobs.candidate.experience.limit";
    public const string EducationInvalid = "jobs.candidate.education.invalid";
    public const string EducationLimit = "jobs.candidate.education.limit";
    public const string CertificationInvalid = "jobs.candidate.certification.invalid";
    public const string CertificationLimit = "jobs.candidate.certifications.limit";
    public const string LanguageInvalid = "jobs.candidate.language.invalid";
    public const string LanguageLimit = "jobs.candidate.languages.limit";
    public const string WorkAuthorizationInvalid = "jobs.candidate.work-authorization.invalid";
    public const string WorkAuthorizationLimit = "jobs.candidate.work-authorizations.limit";
    public const string PreferredLocationInvalid = "jobs.candidate.preferred-location.invalid";
    public const string PreferredLocationLimit = "jobs.candidate.preferred-locations.limit";
    public const string PortfolioLinkInvalid = "jobs.candidate.portfolio.invalid";
    public const string PortfolioLimit = "jobs.candidate.portfolio.limit";
    public const string PeriodInverted = "jobs.candidate.period.inverted";
    public const string ResumeLimit = "jobs.resume.limit";
    public const string ResumeLabelInvalid = "jobs.resume.label.invalid";
    public const string ResumeNotOwned = "jobs.resume.not-owned";

    // ── Applications ────────────────────────────────────────────────────────────────────────

    public const string AlreadyApplied = "jobs.application.already-applied";
    public const string ApplicationsClosed = "jobs.application.closed";
    public const string ApplicationNotWithdrawable = "jobs.application.not-withdrawable";
    public const string ApplicationAlreadyWithdrawn = "jobs.application.already-withdrawn";
    public const string ApplicationAlreadyDecided = "jobs.application.already-decided";
    public const string ApplicationTransitionInvalid = "jobs.application.transition-invalid";
    public const string CoverLetterTooLong = "jobs.application.cover-letter.too-long";
    public const string AnswerInvalid = "jobs.application.answer.invalid";
    public const string AnswerLimit = "jobs.application.answers.limit";
    public const string AttachmentInvalid = "jobs.application.attachment.invalid";
    public const string AttachmentLimit = "jobs.application.attachments.limit";
    public const string SelfApplication = "jobs.application.self";
    public const string ResumeRequired = "jobs.application.resume-required";

    // ── Interviews ──────────────────────────────────────────────────────────────────────────

    public const string InterviewInPast = "jobs.interview.in-past";
    public const string InterviewNotScheduled = "jobs.interview.not-scheduled";
    public const string InterviewAlreadyCompleted = "jobs.interview.already-completed";
    public const string InterviewCancelled = "jobs.interview.cancelled";
    public const string InterviewDurationInvalid = "jobs.interview.duration.invalid";
    public const string InterviewLocationInvalid = "jobs.interview.location.invalid";
    public const string InterviewLimit = "jobs.interviews.limit";

    // ── Offers ──────────────────────────────────────────────────────────────────────────────

    public const string OfferAlreadyExtended = "jobs.offer.already-extended";
    public const string OfferNotExtended = "jobs.offer.not-extended";
    public const string OfferAlreadyDecided = "jobs.offer.already-decided";
    public const string OfferExpired = "jobs.offer.expired";
    public const string OfferExpiryInPast = "jobs.offer.expiry.in-past";
    public const string OfferStartDateInPast = "jobs.offer.start-date.in-past";
    public const string OfferTermsInvalid = "jobs.offer.terms.invalid";

    // ── Pipeline ────────────────────────────────────────────────────────────────────────────

    public const string StageKeyInvalid = "jobs.stage.key-invalid";
    public const string StageKeyTaken = "jobs.stage.key-taken";
    public const string StageLimit = "jobs.stages.limit";
    public const string StageNotRemovable = "jobs.stage.not-removable";
    public const string PipelineEmpty = "jobs.pipeline.empty";

    // ── Catalogues ──────────────────────────────────────────────────────────────────────────

    public const string CategoryInactive = "jobs.category.inactive";
    public const string CategoryKeyTaken = "jobs.category.key-taken";
    public const string SkillKeyInvalid = "jobs.skill.key-invalid";
    public const string SkillKeyTaken = "jobs.skill.key-taken";

    // ── Alerts & saved jobs ─────────────────────────────────────────────────────────────────

    public const string AlertLimit = "jobs.alerts.limit";
    public const string AlertCriteriaEmpty = "jobs.alert.criteria-empty";
    public const string AlertNameInvalid = "jobs.alert.name.invalid";
    public const string SavedJobExists = "jobs.saved.exists";
    public const string SavedJobLimit = "jobs.saved.limit";

    // ── Verification ────────────────────────────────────────────────────────────────────────

    public const string VerificationUnavailable = "jobs.verification.unavailable";
    public const string VerificationAlreadyPending = "jobs.verification.already-pending";
    public const string VerificationRefused = "jobs.verification.refused";

    // ── Rate limiting & abuse ───────────────────────────────────────────────────────────────

    public const string TooManyPostings = "jobs.postings.too-many";
    public const string TooManyApplications = "jobs.applications.too-many";
    public const string SearchQueryInvalid = "jobs.search.query-invalid";
}
