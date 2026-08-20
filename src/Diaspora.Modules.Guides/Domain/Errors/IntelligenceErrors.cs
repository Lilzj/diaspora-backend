using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Domain.Errors;

/// <summary>
/// Every failure the Country Intelligence Platform can return, as constructed
/// <see cref="Error"/> values.
/// <para>
/// Kept apart from <see cref="GuideErrors"/> and <see cref="KnowledgeErrors"/> for the reason B8.1
/// gives: ownership rather than tidiness. B8 answers questions about guides, B8.1 about how they are
/// organised, and this file about what an authority said. A single file holding all three would make
/// it impossible to see at a glance which failures a client of the intelligence surface can receive.
/// </para>
/// <para>
/// The <see cref="ErrorType"/> on each decides the HTTP status through <c>PlatformResults</c>; no
/// file in this module names a number. <see cref="SourceRejected"/> is a validation failure rather
/// than a forbidden one on purpose — the editor is allowed to do this, the source is what was
/// refused, and telling them they lack permission would send them to an administrator who cannot
/// help.
/// </para>
/// </summary>
public static class IntelligenceErrors
{
    // ── Existence ───────────────────────────────────────────────────────────────────────────

    public static readonly Error NoticeNotFound = Error.NotFound(
        IntelligenceErrorCodes.NoticeNotFound,
        "That official notice does not exist.");

    public static readonly Error CategoryNotFound = Error.NotFound(
        IntelligenceErrorCodes.CategoryNotFound,
        "That notice category does not exist.");

    public static readonly Error AuthorityNotFound = Error.NotFound(
        IntelligenceErrorCodes.AuthorityNotFound,
        "That issuing authority does not exist.");

    public static readonly Error EmbassyNotFound = Error.NotFound(
        IntelligenceErrorCodes.EmbassyNotFound,
        "That mission is not in the embassy directory.");

    public static readonly Error EmergencyDirectoryNotFound = Error.NotFound(
        IntelligenceErrorCodes.EmergencyDirectoryNotFound,
        "There is no emergency information for that country yet.");

    public static readonly Error VisaAdvisoryNotFound = Error.NotFound(
        IntelligenceErrorCodes.VisaAdvisoryNotFound,
        "That visa advisory does not exist.");

    public static readonly Error HolidayNotFound = Error.NotFound(
        IntelligenceErrorCodes.HolidayNotFound,
        "That public holiday entry does not exist.");

    // ── Keys and identity ───────────────────────────────────────────────────────────────────

    public static readonly Error CategoryKeyInvalid = Error.Validation(
        IntelligenceErrorCodes.CategoryKeyInvalid,
        "A notice category key may contain only lower-case letters, digits and single hyphens.");

    public static readonly Error CategoryKeyTaken = Error.Conflict(
        IntelligenceErrorCodes.CategoryKeyTaken,
        "A notice category with that key already exists.");

    public static readonly Error CategoryNameInvalid = Error.Validation(
        IntelligenceErrorCodes.CategoryNameInvalid,
        "A notice category needs a name of up to 120 characters.");

    public static readonly Error CountryInvalid = Error.Validation(
        IntelligenceErrorCodes.CountryInvalid,
        "That is not a country code we recognise.");

    public static readonly Error LocaleInvalid = Error.Validation(
        IntelligenceErrorCodes.LocaleInvalid,
        "That is not a language tag we recognise.");

    public static readonly Error EmergencyDirectoryExists = Error.Conflict(
        IntelligenceErrorCodes.EmergencyDirectoryExists,
        "This country already has an emergency directory. Edit that one instead.");

    // ── Content ─────────────────────────────────────────────────────────────────────────────

    public static readonly Error TitleInvalid = Error.Validation(
        IntelligenceErrorCodes.TitleInvalid,
        "An official notice needs a title of up to 250 characters.");

    public static readonly Error SummaryInvalid = Error.Validation(
        IntelligenceErrorCodes.SummaryInvalid,
        "An official notice needs a summary of up to 600 characters.");

    public static readonly Error ContentInvalid = Error.Validation(
        IntelligenceErrorCodes.ContentInvalid,
        "An official notice needs a body, and it may not exceed 40,000 characters.");

    public static readonly Error AuthorityNameInvalid = Error.Validation(
        IntelligenceErrorCodes.AuthorityNameInvalid,
        "An issuing authority needs a name of up to 200 characters.");

    public static readonly Error EmbassyNameInvalid = Error.Validation(
        IntelligenceErrorCodes.EmbassyNameInvalid,
        "A mission needs a name of up to 200 characters.");

    public static readonly Error HolidayNameInvalid = Error.Validation(
        IntelligenceErrorCodes.HolidayNameInvalid,
        "A public holiday needs a name of up to 200 characters.");

    public static readonly Error VisaTitleInvalid = Error.Validation(
        IntelligenceErrorCodes.VisaTitleInvalid,
        "A visa advisory needs a title of up to 250 characters.");

    public static readonly Error EmergencyContactInvalid = Error.Validation(
        IntelligenceErrorCodes.EmergencyContactInvalid,
        "An emergency contact needs a kind, a label and a number.");

    public static readonly Error SeverityInvalid = Error.Validation(
        IntelligenceErrorCodes.SeverityInvalid,
        "That is not a severity we recognise.");

    public static readonly Error AttachmentInvalid = Error.Validation(
        IntelligenceErrorCodes.AttachmentInvalid,
        "An attachment must name a media item.");

    public static readonly Error ReferenceInvalid = Error.Validation(
        IntelligenceErrorCodes.ReferenceInvalid,
        "A related item must name a kind we recognise and an identifier.");

    // ── Sources and provenance ──────────────────────────────────────────────────────────────

    public static readonly Error SourceUrlInvalid = Error.Validation(
        IntelligenceErrorCodes.SourceUrlInvalid,
        "An official source must be an absolute http or https URL.");

    /// <summary>
    /// A deployment's source policy refused the link.
    /// <para>
    /// Validation rather than forbidden: the editor is permitted to do this, the source is what was
    /// refused, and an authorisation failure would send them to an administrator who cannot help.
    /// </para>
    /// </summary>
    public static readonly Error SourceRejected = Error.Validation(
        IntelligenceErrorCodes.SourceRejected,
        "That source is not one this deployment accepts for official notices.");

    public static readonly Error AuthorityCountryMismatch = Error.Validation(
        IntelligenceErrorCodes.AuthorityCountryMismatch,
        "That authority does not issue for this country.");

    public static readonly Error AuthorityUnverified = Error.Conflict(
        IntelligenceErrorCodes.AuthorityUnverified,
        "That authority has not been verified, so notices from it cannot be published.");

    // ── Dates ───────────────────────────────────────────────────────────────────────────────

    public static readonly Error EffectiveAfterExpiry = Error.Validation(
        IntelligenceErrorCodes.EffectiveAfterExpiry,
        "A notice cannot expire before it takes effect.");

    public static readonly Error PublicationInFuture = Error.Validation(
        IntelligenceErrorCodes.PublicationInFuture,
        "A publication date records when the authority issued this, so it cannot be in the future.");

    public static readonly Error HolidayDateInvalid = Error.Validation(
        IntelligenceErrorCodes.HolidayDateInvalid,
        "A public holiday needs a date, and an observed range must end on or after it starts.");

    // ── Lifecycle ───────────────────────────────────────────────────────────────────────────

    public static readonly Error NoticeNotEditable = Error.Conflict(
        IntelligenceErrorCodes.NoticeNotEditable,
        "This notice is awaiting verification or has been superseded, so its content is locked.");

    public static readonly Error NoticeNotSubmittable = Error.Conflict(
        IntelligenceErrorCodes.NoticeNotSubmittable,
        "Only a draft with a title, a summary, a body, a category and an authority can be submitted.");

    public static readonly Error NoticeNotVerifiable = Error.Conflict(
        IntelligenceErrorCodes.NoticeNotVerifiable,
        "Only a submitted notice can be verified.");

    public static readonly Error NoticeNotPublishable = Error.Conflict(
        IntelligenceErrorCodes.NoticeNotPublishable,
        "A notice must be verified before it can be published.");

    public static readonly Error NoticeAlreadyPublished = Error.Conflict(
        IntelligenceErrorCodes.NoticeAlreadyPublished,
        "This notice is already published.");

    public static readonly Error NoticeArchived = Error.Conflict(
        IntelligenceErrorCodes.NoticeArchived,
        "This notice is archived. Restore it first.");

    public static readonly Error NoticeNotArchived = Error.Conflict(
        IntelligenceErrorCodes.NoticeNotArchived,
        "This notice is not archived.");

    public static readonly Error NoticeAlreadySuperseded = Error.Conflict(
        IntelligenceErrorCodes.NoticeAlreadySuperseded,
        "This notice has already been superseded.");

    public static readonly Error SupersedeSelf = Error.Validation(
        IntelligenceErrorCodes.SupersedeSelf,
        "A notice cannot supersede itself.");

    public static readonly Error SupersedeNotPublished = Error.Conflict(
        IntelligenceErrorCodes.SupersedeNotPublished,
        "Only a published notice can be superseded, and only by a published one.");

    public static readonly Error CategoryArchived = Error.Conflict(
        IntelligenceErrorCodes.CategoryArchived,
        "That notice category is archived. Restore it before using it.");

    public static readonly Error CategoryNotArchived = Error.Conflict(
        IntelligenceErrorCodes.CategoryNotArchived,
        "That notice category is not archived.");

    public static readonly Error AuthorityNotArchived = Error.Conflict(
        IntelligenceErrorCodes.AuthorityNotArchived,
        "That authority is not archived.");

    public static readonly Error CategoryInUse = Error.Conflict(
        IntelligenceErrorCodes.CategoryInUse,
        "Notices are still filed under that category. Move them before archiving it.");

    public static readonly Error AuthorityArchived = Error.Conflict(
        IntelligenceErrorCodes.AuthorityArchived,
        "That authority is archived. Restore it before using it.");

    public static readonly Error AuthorityInUse = Error.Conflict(
        IntelligenceErrorCodes.AuthorityInUse,
        "Published notices still name that authority. Supersede them before archiving it.");

    public static readonly Error VisaNotPublishable = Error.Conflict(
        IntelligenceErrorCodes.VisaNotPublishable,
        "A visa advisory needs a title, a summary and an authority before it can be published.");

    public static readonly Error VisaArchived = Error.Conflict(
        IntelligenceErrorCodes.VisaArchived,
        "That visa advisory is archived. Restore it first.");

    public static readonly Error VisaNotArchived = Error.Conflict(
        IntelligenceErrorCodes.VisaNotArchived,
        "That visa advisory is not archived.");

    public static readonly Error EmbassyArchived = Error.Conflict(
        IntelligenceErrorCodes.EmbassyArchived,
        "That mission is archived. Restore it first.");

    public static readonly Error EmbassyNotArchived = Error.Conflict(
        IntelligenceErrorCodes.EmbassyNotArchived,
        "That mission is not archived.");

    public static readonly Error HolidayArchived = Error.Conflict(
        IntelligenceErrorCodes.HolidayArchived,
        "That public holiday entry is archived. Restore it first.");

    public static readonly Error HolidayNotArchived = Error.Conflict(
        IntelligenceErrorCodes.HolidayNotArchived,
        "That public holiday entry is not archived.");

    // ── Limits ──────────────────────────────────────────────────────────────────────────────

    public static readonly Error AttachmentLimit = Error.Validation(
        IntelligenceErrorCodes.AttachmentLimit,
        "That is more attachments than one notice can carry.");

    public static readonly Error ReferenceLimit = Error.Validation(
        IntelligenceErrorCodes.ReferenceLimit,
        "That is more related items than one notice can point at.");

    public static readonly Error LanguageLimit = Error.Validation(
        IntelligenceErrorCodes.LanguageLimit,
        "That is more languages than one notice can declare.");

    public static readonly Error LocalizationLimit = Error.Validation(
        IntelligenceErrorCodes.LocalizationLimit,
        "That is more translations than one entry can carry.");

    public static readonly Error EmergencyContactLimit = Error.Validation(
        IntelligenceErrorCodes.EmergencyContactLimit,
        "That is more contacts than one country's emergency directory can hold.");

    public static readonly Error EmbassyServiceLimit = Error.Validation(
        IntelligenceErrorCodes.EmbassyServiceLimit,
        "That is more services than one mission can list.");

    public static readonly Error VisaRequirementLimit = Error.Validation(
        IntelligenceErrorCodes.VisaRequirementLimit,
        "That is more requirements than one advisory can list.");

    public static readonly Error VisaDocumentLimit = Error.Validation(
        IntelligenceErrorCodes.VisaDocumentLimit,
        "That is more documents than one advisory can list.");

    public static readonly Error DuplicateReference = Error.Validation(
        IntelligenceErrorCodes.DuplicateReference,
        "A notice can point at the same item only once.");

    public static readonly Error DuplicateEmergencyKind = Error.Validation(
        IntelligenceErrorCodes.DuplicateEmergencyKind,
        "A country has one contact of each emergency kind.");

    // ── Concurrency and authorization ───────────────────────────────────────────────────────

    public static readonly Error RevisionConflict = Error.Conflict(
        IntelligenceErrorCodes.RevisionConflict,
        "Somebody else changed this while you were editing it. Reload and try again.");

    public static readonly Error NotAuthenticated = Error.Unauthorized(
        IntelligenceErrorCodes.NotAuthenticated,
        "You must be signed in to do this.");

    public static readonly Error NotEditor = Error.Forbidden(
        IntelligenceErrorCodes.NotEditor,
        "Only country intelligence editors can do this.");

    // ── Discovery and verification ──────────────────────────────────────────────────────────

    public static readonly Error SearchUnavailable = Error.ServiceUnavailable(
        IntelligenceErrorCodes.SearchUnavailable,
        "Official notice search is not available on this deployment.");

    public static readonly Error SearchTermInvalid = Error.Validation(
        IntelligenceErrorCodes.SearchTermInvalid,
        "That search phrase is too long.");

    public static readonly Error VerificationUnavailable = Error.ServiceUnavailable(
        IntelligenceErrorCodes.VerificationUnavailable,
        "Authority verification is not available on this deployment.");

    public static readonly Error VerificationAlreadyPending = Error.Conflict(
        IntelligenceErrorCodes.VerificationAlreadyPending,
        "A verification for this authority is already with a reviewer.");
}
