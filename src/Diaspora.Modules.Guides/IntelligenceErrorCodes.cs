namespace Diaspora.Modules.Guides;

/// <summary>
/// The stable wire-level error codes the Country Intelligence Platform emits.
/// <para>
/// Prefixed <c>intelligence.</c> rather than <c>guides.</c>, on the same reasoning B8.1 gives for
/// <c>knowledge.</c>: the prefix names the surface a client is talking to, and a client calling
/// <c>/api/v1/intelligence</c> that receives <c>guides.not-found</c> has to know that country
/// intelligence lives inside the Guides module to make sense of it. Where a failure genuinely is
/// about a guide — a notice referencing one that does not exist — the code stays <c>guides.</c>,
/// because that is B8's fact.
/// </para>
/// <para>
/// These strings are frozen. Change a description freely; never change a code. New failures get new
/// codes.
/// </para>
/// </summary>
public static class IntelligenceErrorCodes
{
    // Existence
    public const string NoticeNotFound = "intelligence.notice.not-found";
    public const string CategoryNotFound = "intelligence.category.not-found";
    public const string AuthorityNotFound = "intelligence.authority.not-found";
    public const string EmbassyNotFound = "intelligence.embassy.not-found";
    public const string EmergencyDirectoryNotFound = "intelligence.emergency.not-found";
    public const string VisaAdvisoryNotFound = "intelligence.visa.not-found";
    public const string HolidayNotFound = "intelligence.holiday.not-found";

    // Keys and identity
    public const string CategoryKeyInvalid = "intelligence.category.key-invalid";
    public const string CategoryKeyTaken = "intelligence.category.key-taken";
    public const string CategoryNameInvalid = "intelligence.category.name-invalid";
    public const string CountryInvalid = "intelligence.country.invalid";
    public const string LocaleInvalid = "intelligence.locale.invalid";
    public const string EmergencyDirectoryExists = "intelligence.emergency.exists";

    // Content
    public const string TitleInvalid = "intelligence.notice.title-invalid";
    public const string SummaryInvalid = "intelligence.notice.summary-invalid";
    public const string ContentInvalid = "intelligence.notice.content-invalid";
    public const string AuthorityNameInvalid = "intelligence.authority.name-invalid";
    public const string EmbassyNameInvalid = "intelligence.embassy.name-invalid";
    public const string HolidayNameInvalid = "intelligence.holiday.name-invalid";
    public const string VisaTitleInvalid = "intelligence.visa.title-invalid";
    public const string EmergencyContactInvalid = "intelligence.emergency.contact-invalid";
    public const string SeverityInvalid = "intelligence.notice.severity-invalid";
    public const string AttachmentInvalid = "intelligence.notice.attachment-invalid";
    public const string ReferenceInvalid = "intelligence.notice.reference-invalid";

    // Sources and provenance
    public const string SourceUrlInvalid = "intelligence.source.url-invalid";
    public const string SourceRejected = "intelligence.source.rejected";
    public const string AuthorityCountryMismatch = "intelligence.authority.country-mismatch";
    public const string AuthorityUnverified = "intelligence.authority.unverified";
    public const string AuthorityTypeReserved = "intelligence.authority.type-reserved";

    // Dates
    public const string EffectiveAfterExpiry = "intelligence.dates.effective-after-expiry";
    public const string PublicationInFuture = "intelligence.dates.publication-in-future";
    public const string HolidayDateInvalid = "intelligence.holiday.date-invalid";

    // Lifecycle
    public const string NoticeNotEditable = "intelligence.notice.not-editable";
    public const string NoticeNotSubmittable = "intelligence.notice.not-submittable";
    public const string NoticeNotVerifiable = "intelligence.notice.not-verifiable";
    public const string NoticeNotPublishable = "intelligence.notice.not-publishable";
    public const string NoticeAlreadyPublished = "intelligence.notice.already-published";
    public const string NoticeArchived = "intelligence.notice.archived";
    public const string NoticeNotArchived = "intelligence.notice.not-archived";
    public const string NoticeAlreadySuperseded = "intelligence.notice.already-superseded";
    public const string SupersedeSelf = "intelligence.notice.supersede-self";
    public const string SupersedeNotPublished = "intelligence.notice.supersede-not-published";
    public const string CategoryArchived = "intelligence.category.archived";
    public const string CategoryNotArchived = "intelligence.category.not-archived";
    public const string CategoryInUse = "intelligence.category.in-use";
    public const string AuthorityArchived = "intelligence.authority.archived";
    public const string AuthorityNotArchived = "intelligence.authority.not-archived";
    public const string AuthorityInUse = "intelligence.authority.in-use";
    public const string VisaNotPublishable = "intelligence.visa.not-publishable";
    public const string VisaArchived = "intelligence.visa.archived";
    public const string VisaNotArchived = "intelligence.visa.not-archived";
    public const string EmbassyArchived = "intelligence.embassy.archived";
    public const string EmbassyNotArchived = "intelligence.embassy.not-archived";
    public const string HolidayArchived = "intelligence.holiday.archived";
    public const string HolidayNotArchived = "intelligence.holiday.not-archived";

    // Limits
    public const string AttachmentLimit = "intelligence.notice.attachment-limit";
    public const string ReferenceLimit = "intelligence.notice.reference-limit";
    public const string LanguageLimit = "intelligence.notice.language-limit";
    public const string LocalizationLimit = "intelligence.localization-limit";
    public const string EmergencyContactLimit = "intelligence.emergency.contact-limit";
    public const string EmbassyServiceLimit = "intelligence.embassy.service-limit";
    public const string VisaRequirementLimit = "intelligence.visa.requirement-limit";
    public const string VisaDocumentLimit = "intelligence.visa.document-limit";
    public const string DuplicateReference = "intelligence.notice.duplicate-reference";
    public const string DuplicateEmergencyKind = "intelligence.emergency.duplicate-kind";

    // Concurrency and authorization
    public const string RevisionConflict = "intelligence.revision-conflict";
    public const string NotAuthenticated = "intelligence.not-authenticated";
    public const string NotEditor = "intelligence.not-editor";

    // Discovery and verification
    public const string SearchUnavailable = "intelligence.search.unavailable";
    public const string SearchTermInvalid = "intelligence.search.term-invalid";
    public const string VerificationUnavailable = "intelligence.verification.unavailable";
    public const string VerificationAlreadyPending = "intelligence.verification.already-pending";
}
