using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Jobs.Domain.Errors;

/// <summary>
/// Every failure this module can return, as constructed <see cref="Error"/> values.
/// <para>
/// One instance per failure, built once, so a handler returns <c>JobErrors.PostingNotFound</c>
/// rather than assembling a code and a message at the failure site. That keeps the message
/// consistent between the places the same failure arises, and makes the set enumerable — a test
/// asserts every constant here has a matching entry in <see cref="JobErrorCodes"/> and vice
/// versa.
/// </para>
/// <para>
/// The <see cref="ErrorType"/> on each is what decides the HTTP status, through
/// <c>PlatformResults</c>. No place in the module names a number. Two choices are worth
/// stating: <see cref="RevisionConflict"/> is a conflict so an editor can offer a merge rather
/// than an error page, and everything a caller must not be able to probe for —
/// <see cref="PostingHidden"/>, <see cref="PostingDeleted"/>, <see cref="DraftNotVisible"/> —
/// is a not-found rather than a forbidden, because a 403 on a hidden posting confirms it
/// exists.
/// </para>
/// <para>
/// The messages address the reader in the second person and say what to do next where there is
/// something to do. They are user-facing strings, not log lines.
/// </para>
/// </summary>
public static class JobErrors
{
    // ── Existence & visibility ──────────────────────────────────────────────────────────────

    public static readonly Error PostingNotFound = Error.NotFound(
        JobErrorCodes.PostingNotFound,
        "That job does not exist, or you cannot see it.");

    public static readonly Error EmployerNotFound = Error.NotFound(
        JobErrorCodes.EmployerNotFound,
        "That employer does not exist.");

    public static readonly Error CandidateNotFound = Error.NotFound(
        JobErrorCodes.CandidateNotFound,
        "That candidate profile does not exist.");

    public static readonly Error ApplicationNotFound = Error.NotFound(
        JobErrorCodes.ApplicationNotFound,
        "That application does not exist, or you cannot see it.");

    public static readonly Error InterviewNotFound = Error.NotFound(
        JobErrorCodes.InterviewNotFound,
        "That interview does not exist.");

    public static readonly Error OfferNotFound = Error.NotFound(
        JobErrorCodes.OfferNotFound,
        "That offer does not exist.");

    public static readonly Error ResumeNotFound = Error.NotFound(
        JobErrorCodes.ResumeNotFound,
        "That résumé does not exist.");

    public static readonly Error CategoryNotFound = Error.NotFound(
        JobErrorCodes.CategoryNotFound,
        "That job category does not exist.");

    public static readonly Error SkillNotFound = Error.NotFound(
        JobErrorCodes.SkillNotFound,
        "That skill is not in the catalogue.");

    public static readonly Error AlertNotFound = Error.NotFound(
        JobErrorCodes.AlertNotFound,
        "That job alert does not exist.");

    public static readonly Error SavedJobNotFound = Error.NotFound(
        JobErrorCodes.SavedJobNotFound,
        "That job is not in your saved list.");

    public static readonly Error PipelineNotFound = Error.NotFound(
        JobErrorCodes.PipelineNotFound,
        "That job has no hiring pipeline.");

    public static readonly Error StageNotFound = Error.NotFound(
        JobErrorCodes.StageNotFound,
        "That hiring stage does not exist on this pipeline.");

    // Not-found rather than forbidden: a 403 on a hidden posting confirms it exists.
    public static readonly Error PostingHidden = Error.NotFound(
        JobErrorCodes.PostingHidden,
        "That job does not exist, or you cannot see it.");

    public static readonly Error PostingDeleted = Error.NotFound(
        JobErrorCodes.PostingDeleted,
        "That job has been deleted.");

    public static readonly Error DraftNotVisible = Error.NotFound(
        JobErrorCodes.DraftNotVisible,
        "That job does not exist, or you cannot see it.");

    // ── Authorization & ownership ───────────────────────────────────────────────────────────

    public static readonly Error NotAuthenticated = Error.Unauthorized(
        JobErrorCodes.NotAuthenticated,
        "Sign in to continue.");

    public static readonly Error NotEmployer = Error.Forbidden(
        JobErrorCodes.NotEmployer,
        "You are not a member of this employer.");

    public static readonly Error NotRecruiter = Error.Forbidden(
        JobErrorCodes.NotRecruiter,
        "You do not have permission to recruit for this employer.");

    public static readonly Error NotCandidate = Error.Forbidden(
        JobErrorCodes.NotCandidate,
        "Create a candidate profile before doing that.");

    public static readonly Error NotApplicant = Error.Forbidden(
        JobErrorCodes.NotApplicant,
        "This is not your application.");

    public static readonly Error NotModerator = Error.Forbidden(
        JobErrorCodes.NotModerator,
        "Only moderators can do that.");

    public static readonly Error EmployerNotOwned = Error.Forbidden(
        JobErrorCodes.EmployerNotOwned,
        "Only an owner of this employer can do that.");

    public static readonly Error PostingNotOwned = Error.Forbidden(
        JobErrorCodes.PostingNotOwned,
        "This job belongs to another employer.");

    // ── Concurrency ─────────────────────────────────────────────────────────────────────────

    public static readonly Error RevisionConflict = Error.Conflict(
        JobErrorCodes.RevisionConflict,
        "Someone else changed this job while you were editing. Reload and try again.");

    // ── Posting lifecycle ───────────────────────────────────────────────────────────────────

    public static readonly Error PostingNotPublishable = Error.Validation(
        JobErrorCodes.PostingNotPublishable,
        "A job needs a title, a description, a category and a location before it can be published.");

    public static readonly Error PostingAlreadyPublished = Error.Conflict(
        JobErrorCodes.PostingAlreadyPublished,
        "This job is already published.");

    public static readonly Error PostingNotPublished = Error.Conflict(
        JobErrorCodes.PostingNotPublished,
        "This job has not been published.");

    public static readonly Error PostingNotOpen = Error.Conflict(
        JobErrorCodes.PostingNotOpen,
        "This job is not open for applications.");

    public static readonly Error PostingClosed = Error.Conflict(
        JobErrorCodes.PostingClosed,
        "This job is no longer accepting applications.");

    public static readonly Error PostingExpired = Error.Conflict(
        JobErrorCodes.PostingExpired,
        "The closing date for this job has passed.");

    public static readonly Error PostingFilled = Error.Conflict(
        JobErrorCodes.PostingFilled,
        "This job has been filled.");

    public static readonly Error PostingNotArchived = Error.Conflict(
        JobErrorCodes.PostingNotArchived,
        "This job is not archived.");

    public static readonly Error PostingArchivedImmutable = Error.Conflict(
        JobErrorCodes.PostingArchivedImmutable,
        "An archived job cannot be edited. Restore it first.");

    public static readonly Error PostingNotDraft = Error.Conflict(
        JobErrorCodes.PostingNotDraft,
        "Only a draft can do that.");

    public static readonly Error PostingNotDeleted = Error.Conflict(
        JobErrorCodes.PostingNotDeleted,
        "This job has not been deleted.");

    public static readonly Error ClosingDateInPast = Error.Validation(
        JobErrorCodes.ClosingDateInPast,
        "The closing date must be in the future.");

    public static readonly Error NoVacanciesRemaining = Error.Conflict(
        JobErrorCodes.NoVacanciesRemaining,
        "Every vacancy on this job has been filled.");

    // ── Posting content ─────────────────────────────────────────────────────────────────────

    public static readonly Error TitleInvalid = Error.Validation(
        JobErrorCodes.TitleInvalid,
        "The job title is missing or too long.");

    public static readonly Error SlugInvalid = Error.Validation(
        JobErrorCodes.SlugInvalid,
        "That link handle is not valid.");

    public static readonly Error SlugTaken = Error.Conflict(
        JobErrorCodes.SlugTaken,
        "That link handle is already in use.");

    public static readonly Error DescriptionInvalid = Error.Validation(
        JobErrorCodes.DescriptionInvalid,
        "The job description is missing or too long.");

    public static readonly Error SummaryTooLong = Error.Validation(
        JobErrorCodes.SummaryTooLong,
        "The summary is too long.");

    public static readonly Error ResponsibilityInvalid = Error.Validation(
        JobErrorCodes.ResponsibilityInvalid,
        "A responsibility is empty or too long.");

    public static readonly Error ResponsibilityLimit = Error.Validation(
        JobErrorCodes.ResponsibilityLimit,
        "This job has too many responsibilities listed.");

    public static readonly Error RequirementInvalid = Error.Validation(
        JobErrorCodes.RequirementInvalid,
        "A requirement is empty or too long.");

    public static readonly Error RequirementLimit = Error.Validation(
        JobErrorCodes.RequirementLimit,
        "This job has too many requirements listed.");

    public static readonly Error BenefitInvalid = Error.Validation(
        JobErrorCodes.BenefitInvalid,
        "A benefit is empty or too long.");

    public static readonly Error BenefitLimit = Error.Validation(
        JobErrorCodes.BenefitLimit,
        "This job has too many benefits listed.");

    public static readonly Error SkillInvalid = Error.Validation(
        JobErrorCodes.SkillInvalid,
        "A skill name is empty or too long.");

    public static readonly Error SkillLimit = Error.Validation(
        JobErrorCodes.SkillLimit,
        "This job names too many skills.");

    public static readonly Error TagInvalid = Error.Validation(
        JobErrorCodes.TagInvalid,
        "A tag is empty or contains characters that are not allowed.");

    public static readonly Error TagLimit = Error.Validation(
        JobErrorCodes.TagLimit,
        "This job has too many tags.");

    public static readonly Error MediaInvalid = Error.Validation(
        JobErrorCodes.MediaInvalid,
        "That media reference is not valid.");

    public static readonly Error MediaLimit = Error.Validation(
        JobErrorCodes.MediaLimit,
        "This job has too many attachments.");

    public static readonly Error CountryInvalid = Error.Validation(
        JobErrorCodes.CountryInvalid,
        "That is not a valid two-letter country code.");

    public static readonly Error LocationInvalid = Error.Validation(
        JobErrorCodes.LocationInvalid,
        "The location on this job is not valid.");

    public static readonly Error CurrencyInvalid = Error.Validation(
        JobErrorCodes.CurrencyInvalid,
        "That is not a valid three-letter currency code.");

    public static readonly Error SalaryInvalid = Error.Validation(
        JobErrorCodes.SalaryInvalid,
        "The salary figures are not valid.");

    public static readonly Error SalaryRangeInverted = Error.Validation(
        JobErrorCodes.SalaryRangeInverted,
        "The minimum salary cannot be higher than the maximum.");

    public static readonly Error VacanciesInvalid = Error.Validation(
        JobErrorCodes.VacanciesInvalid,
        "A job must advertise at least one vacancy.");

    public static readonly Error WorkplaceInvalid = Error.Validation(
        JobErrorCodes.WorkplaceInvalid,
        "That workplace arrangement is not valid for this job.");

    public static readonly Error CategoryKeyInvalid = Error.Validation(
        JobErrorCodes.CategoryKeyInvalid,
        "That category key is not valid.");

    public static readonly Error ModerationNoteInvalid = Error.Validation(
        JobErrorCodes.ModerationNoteInvalid,
        "A moderation note is required and must not be too long.");

    public static readonly Error ProviderReferenceInvalid = Error.Validation(
        JobErrorCodes.ProviderReferenceInvalid,
        "That provider reference is not valid.");

    public static readonly Error ProviderReferenceLimit = Error.Validation(
        JobErrorCodes.ProviderReferenceLimit,
        "This job references too many providers.");

    public static readonly Error GuideReferenceInvalid = Error.Validation(
        JobErrorCodes.GuideReferenceInvalid,
        "That guide reference is not valid.");

    public static readonly Error GuideReferenceLimit = Error.Validation(
        JobErrorCodes.GuideReferenceLimit,
        "This job links too many guides.");

    // ── Employer ────────────────────────────────────────────────────────────────────────────

    public static readonly Error EmployerNameInvalid = Error.Validation(
        JobErrorCodes.EmployerNameInvalid,
        "The employer name is missing or too long.");

    public static readonly Error EmployerSlugTaken = Error.Conflict(
        JobErrorCodes.EmployerSlugTaken,
        "That employer link handle is already in use.");

    public static readonly Error EmployerAlreadyExists = Error.Conflict(
        JobErrorCodes.EmployerAlreadyExists,
        "You already have an employer profile with that name.");

    public static readonly Error EmployerNotVerified = Error.Forbidden(
        JobErrorCodes.EmployerNotVerified,
        "This employer must be verified before it can do that.");

    public static readonly Error EmployerSuspended = Error.Forbidden(
        JobErrorCodes.EmployerSuspended,
        "This employer is suspended and cannot post or recruit.");

    public static readonly Error EmployerContactInvalid = Error.Validation(
        JobErrorCodes.EmployerContactInvalid,
        "The contact details are not valid.");

    public static readonly Error EmployerRegionInvalid = Error.Validation(
        JobErrorCodes.EmployerRegionInvalid,
        "That hiring region is not valid.");

    public static readonly Error EmployerRegionLimit = Error.Validation(
        JobErrorCodes.EmployerRegionLimit,
        "This employer lists too many hiring regions.");

    public static readonly Error RecruiterAlreadyMember = Error.Conflict(
        JobErrorCodes.RecruiterAlreadyMember,
        "That member is already part of this employer.");

    public static readonly Error RecruiterNotMember = Error.NotFound(
        JobErrorCodes.RecruiterNotMember,
        "That member is not part of this employer.");

    public static readonly Error CannotRemoveLastOwner = Error.Conflict(
        JobErrorCodes.CannotRemoveLastOwner,
        "An employer must always have at least one owner.");

    // ── Candidate ───────────────────────────────────────────────────────────────────────────

    public static readonly Error CandidateAlreadyExists = Error.Conflict(
        JobErrorCodes.CandidateAlreadyExists,
        "You already have a candidate profile.");

    public static readonly Error HeadlineInvalid = Error.Validation(
        JobErrorCodes.HeadlineInvalid,
        "The headline is missing or too long.");

    public static readonly Error ExperienceInvalid = Error.Validation(
        JobErrorCodes.ExperienceInvalid,
        "A work history entry is missing a title, an employer or valid dates.");

    public static readonly Error ExperienceLimit = Error.Validation(
        JobErrorCodes.ExperienceLimit,
        "Your work history has too many entries.");

    public static readonly Error EducationInvalid = Error.Validation(
        JobErrorCodes.EducationInvalid,
        "An education entry is missing an institution or valid dates.");

    public static readonly Error EducationLimit = Error.Validation(
        JobErrorCodes.EducationLimit,
        "Your education history has too many entries.");

    public static readonly Error CertificationInvalid = Error.Validation(
        JobErrorCodes.CertificationInvalid,
        "A certification is missing a name or issuer.");

    public static readonly Error CertificationLimit = Error.Validation(
        JobErrorCodes.CertificationLimit,
        "You have listed too many certifications.");

    public static readonly Error LanguageInvalid = Error.Validation(
        JobErrorCodes.LanguageInvalid,
        "That is not a valid two-letter language code.");

    public static readonly Error LanguageLimit = Error.Validation(
        JobErrorCodes.LanguageLimit,
        "You have listed too many languages.");

    public static readonly Error WorkAuthorizationInvalid = Error.Validation(
        JobErrorCodes.WorkAuthorizationInvalid,
        "That work authorisation entry is not valid.");

    public static readonly Error WorkAuthorizationLimit = Error.Validation(
        JobErrorCodes.WorkAuthorizationLimit,
        "You have listed too many work authorisations.");

    public static readonly Error PreferredLocationInvalid = Error.Validation(
        JobErrorCodes.PreferredLocationInvalid,
        "That preferred location is not valid.");

    public static readonly Error PreferredLocationLimit = Error.Validation(
        JobErrorCodes.PreferredLocationLimit,
        "You have listed too many preferred locations.");

    public static readonly Error PortfolioLinkInvalid = Error.Validation(
        JobErrorCodes.PortfolioLinkInvalid,
        "That portfolio link is not valid.");

    public static readonly Error PortfolioLimit = Error.Validation(
        JobErrorCodes.PortfolioLimit,
        "You have listed too many portfolio items.");

    public static readonly Error PeriodInverted = Error.Validation(
        JobErrorCodes.PeriodInverted,
        "The end date cannot be before the start date.");

    public static readonly Error ResumeLimit = Error.Validation(
        JobErrorCodes.ResumeLimit,
        "You have reached the limit on saved résumés. Delete one to add another.");

    public static readonly Error ResumeLabelInvalid = Error.Validation(
        JobErrorCodes.ResumeLabelInvalid,
        "The résumé label is missing or too long.");

    public static readonly Error ResumeNotOwned = Error.Forbidden(
        JobErrorCodes.ResumeNotOwned,
        "That résumé is not yours.");

    // ── Applications ────────────────────────────────────────────────────────────────────────

    public static readonly Error AlreadyApplied = Error.Conflict(
        JobErrorCodes.AlreadyApplied,
        "You have already applied to this job.");

    public static readonly Error ApplicationsClosed = Error.Conflict(
        JobErrorCodes.ApplicationsClosed,
        "This job is not accepting applications.");

    public static readonly Error ApplicationNotWithdrawable = Error.Conflict(
        JobErrorCodes.ApplicationNotWithdrawable,
        "This application has already been decided and cannot be withdrawn.");

    public static readonly Error ApplicationAlreadyWithdrawn = Error.Conflict(
        JobErrorCodes.ApplicationAlreadyWithdrawn,
        "You have already withdrawn this application.");

    public static readonly Error ApplicationAlreadyDecided = Error.Conflict(
        JobErrorCodes.ApplicationAlreadyDecided,
        "This application has already been decided.");

    public static readonly Error ApplicationTransitionInvalid = Error.Conflict(
        JobErrorCodes.ApplicationTransitionInvalid,
        "An application cannot move to that state from where it is now.");

    public static readonly Error CoverLetterTooLong = Error.Validation(
        JobErrorCodes.CoverLetterTooLong,
        "Your cover letter is too long.");

    public static readonly Error AnswerInvalid = Error.Validation(
        JobErrorCodes.AnswerInvalid,
        "An answer is empty or too long.");

    public static readonly Error AnswerLimit = Error.Validation(
        JobErrorCodes.AnswerLimit,
        "This application has too many answers.");

    public static readonly Error AttachmentInvalid = Error.Validation(
        JobErrorCodes.AttachmentInvalid,
        "That attachment is not valid.");

    public static readonly Error AttachmentLimit = Error.Validation(
        JobErrorCodes.AttachmentLimit,
        "This application has too many attachments.");

    public static readonly Error SelfApplication = Error.Validation(
        JobErrorCodes.SelfApplication,
        "You cannot apply to a job posted by an employer you recruit for.");

    public static readonly Error ResumeRequired = Error.Validation(
        JobErrorCodes.ResumeRequired,
        "This job requires a résumé with your application.");

    // ── Interviews ──────────────────────────────────────────────────────────────────────────

    public static readonly Error InterviewInPast = Error.Validation(
        JobErrorCodes.InterviewInPast,
        "An interview must be scheduled for a future time.");

    public static readonly Error InterviewNotScheduled = Error.Conflict(
        JobErrorCodes.InterviewNotScheduled,
        "That interview has not been scheduled.");

    public static readonly Error InterviewAlreadyCompleted = Error.Conflict(
        JobErrorCodes.InterviewAlreadyCompleted,
        "That interview already has a recorded outcome.");

    public static readonly Error InterviewCancelled = Error.Conflict(
        JobErrorCodes.InterviewCancelled,
        "That interview was cancelled.");

    public static readonly Error InterviewDurationInvalid = Error.Validation(
        JobErrorCodes.InterviewDurationInvalid,
        "That interview length is not valid.");

    public static readonly Error InterviewLocationInvalid = Error.Validation(
        JobErrorCodes.InterviewLocationInvalid,
        "An on-site interview needs an address, and a remote one needs a joining link.");

    public static readonly Error InterviewLimit = Error.Validation(
        JobErrorCodes.InterviewLimit,
        "This application has too many interview rounds.");

    // ── Offers ──────────────────────────────────────────────────────────────────────────────

    public static readonly Error OfferAlreadyExtended = Error.Conflict(
        JobErrorCodes.OfferAlreadyExtended,
        "An offer is already with this candidate.");

    public static readonly Error OfferNotExtended = Error.Conflict(
        JobErrorCodes.OfferNotExtended,
        "That offer has not been sent to the candidate yet.");

    public static readonly Error OfferAlreadyDecided = Error.Conflict(
        JobErrorCodes.OfferAlreadyDecided,
        "That offer has already been answered.");

    public static readonly Error OfferExpired = Error.Conflict(
        JobErrorCodes.OfferExpired,
        "That offer has expired.");

    public static readonly Error OfferExpiryInPast = Error.Validation(
        JobErrorCodes.OfferExpiryInPast,
        "The response deadline must be in the future.");

    public static readonly Error OfferStartDateInPast = Error.Validation(
        JobErrorCodes.OfferStartDateInPast,
        "The start date must be in the future.");

    public static readonly Error OfferTermsInvalid = Error.Validation(
        JobErrorCodes.OfferTermsInvalid,
        "The offer terms are not valid.");

    // ── Pipeline ────────────────────────────────────────────────────────────────────────────

    public static readonly Error StageKeyInvalid = Error.Validation(
        JobErrorCodes.StageKeyInvalid,
        "That stage key is not valid.");

    public static readonly Error StageKeyTaken = Error.Conflict(
        JobErrorCodes.StageKeyTaken,
        "This pipeline already has a stage with that key.");

    public static readonly Error StageLimit = Error.Validation(
        JobErrorCodes.StageLimit,
        "This pipeline has too many stages.");

    public static readonly Error StageNotRemovable = Error.Conflict(
        JobErrorCodes.StageNotRemovable,
        "The applied and hired stages cannot be removed.");

    public static readonly Error PipelineEmpty = Error.Validation(
        JobErrorCodes.PipelineEmpty,
        "A hiring pipeline needs at least one stage.");

    // ── Catalogues ──────────────────────────────────────────────────────────────────────────

    public static readonly Error CategoryInactive = Error.Validation(
        JobErrorCodes.CategoryInactive,
        "That job category is no longer in use.");

    public static readonly Error CategoryKeyTaken = Error.Conflict(
        JobErrorCodes.CategoryKeyTaken,
        "A category with that key already exists.");

    public static readonly Error SkillKeyInvalid = Error.Validation(
        JobErrorCodes.SkillKeyInvalid,
        "That skill key is not valid.");

    public static readonly Error SkillKeyTaken = Error.Conflict(
        JobErrorCodes.SkillKeyTaken,
        "A skill with that key already exists.");

    // ── Alerts & saved jobs ─────────────────────────────────────────────────────────────────

    public static readonly Error AlertLimit = Error.Validation(
        JobErrorCodes.AlertLimit,
        "You have reached the limit on job alerts. Delete one to add another.");

    public static readonly Error AlertCriteriaEmpty = Error.Validation(
        JobErrorCodes.AlertCriteriaEmpty,
        "A job alert needs at least one thing to watch for.");

    public static readonly Error AlertNameInvalid = Error.Validation(
        JobErrorCodes.AlertNameInvalid,
        "The alert name is missing or too long.");

    public static readonly Error SavedJobExists = Error.Conflict(
        JobErrorCodes.SavedJobExists,
        "That job is already saved.");

    public static readonly Error SavedJobLimit = Error.Validation(
        JobErrorCodes.SavedJobLimit,
        "You have reached the limit on saved jobs.");

    // ── Verification ────────────────────────────────────────────────────────────────────────

    public static readonly Error VerificationUnavailable = Error.ServiceUnavailable(
        JobErrorCodes.VerificationUnavailable,
        "Verification is not available right now. Try again later.");

    public static readonly Error VerificationAlreadyPending = Error.Conflict(
        JobErrorCodes.VerificationAlreadyPending,
        "A verification request is already being reviewed.");

    public static readonly Error VerificationRefused = Error.Validation(
        JobErrorCodes.VerificationRefused,
        "That verification request was refused.");

    // ── Rate limiting & abuse ───────────────────────────────────────────────────────────────

    public static readonly Error TooManyPostings = Error.RateLimited(
        JobErrorCodes.TooManyPostings,
        "This employer has reached its limit on open jobs.");

    public static readonly Error TooManyApplications = Error.RateLimited(
        JobErrorCodes.TooManyApplications,
        "You have applied to a lot of jobs today. Try again tomorrow.");

    public static readonly Error SearchQueryInvalid = Error.Validation(
        JobErrorCodes.SearchQueryInvalid,
        "That search is not valid.");
}
