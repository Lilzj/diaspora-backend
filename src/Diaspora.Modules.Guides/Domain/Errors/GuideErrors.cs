using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Domain.Errors;

/// <summary>
/// Every failure this module can return, as constructed <see cref="Error"/> values.
/// <para>
/// One instance per failure, built once, so a handler returns <c>GuideErrors.NotFound</c>
/// rather than assembling a code and a message at the throw site. That keeps the message
/// consistent between the places the same failure can arise, and it makes the set enumerable —
/// a test asserts that every constant here has a matching entry in
/// <see cref="GuideErrorCodes"/>.
/// </para>
/// <para>
/// The <see cref="ErrorType"/> on each is what decides the HTTP status, through
/// <c>PlatformResults</c>. Getting it right here is the whole of B8's status-code contract:
/// no place in the module names a number. <see cref="RevisionConflict"/> in particular must be
/// a conflict — it is the signal the editor uses to offer a merge rather than an error page.
/// </para>
/// </summary>
public static class GuideErrors
{
    // ── Existence & visibility ──────────────────────────────────────────────────────────────

    public static readonly Error NotFound = Error.NotFound(
        GuideErrorCodes.NotFound,
        "That guide does not exist, or you cannot see it.");

    public static readonly Error VersionNotFound = Error.NotFound(
        GuideErrorCodes.VersionNotFound,
        "That version of the guide does not exist.");

    public static readonly Error CategoryNotFound = Error.NotFound(
        GuideErrorCodes.CategoryNotFound,
        "That category does not exist.");

    public static readonly Error SuggestionNotFound = Error.NotFound(
        GuideErrorCodes.SuggestionNotFound,
        "That suggestion does not exist.");

    public static readonly Error DraftNotVisible = Error.Forbidden(
        GuideErrorCodes.DraftNotVisible,
        "Only the author and staff can see an unpublished guide.");

    public static readonly Error Hidden = Error.NotFound(
        GuideErrorCodes.Hidden,
        "This guide has been hidden by moderation.");

    public static readonly Error Deleted = Error.NotFound(
        GuideErrorCodes.Deleted,
        "This guide has been removed.");

    // ── Authorship & authorization ──────────────────────────────────────────────────────────

    public static readonly Error NotAuthenticated = Error.Unauthorized(
        GuideErrorCodes.NotAuthenticated,
        "You must be signed in to do this.");

    public static readonly Error NotAuthor = Error.Forbidden(
        GuideErrorCodes.NotAuthor,
        "Only the guide's author can do this.");

    public static readonly Error NotModerator = Error.Forbidden(
        GuideErrorCodes.NotModerator,
        "Only moderation staff can do this.");

    // ── Concurrency & lifecycle ─────────────────────────────────────────────────────────────

    public static readonly Error RevisionConflict = Error.Conflict(
        GuideErrorCodes.RevisionConflict,
        "The guide changed while you were editing it. Reload and merge your changes.");

    public static readonly Error NotPublishable = Error.Conflict(
        GuideErrorCodes.NotPublishable,
        "This guide is not complete enough to publish. It needs a title, a category and at least one section with content.");

    public static readonly Error AlreadyPublished = Error.Conflict(
        GuideErrorCodes.AlreadyPublished,
        "This guide is already published.");

    public static readonly Error NotPublished = Error.Conflict(
        GuideErrorCodes.NotPublished,
        "This guide has not been published.");

    public static readonly Error NotArchived = Error.Conflict(
        GuideErrorCodes.NotArchived,
        "This guide is not archived.");

    public static readonly Error ArchivedImmutable = Error.Conflict(
        GuideErrorCodes.ArchivedImmutable,
        "An archived guide cannot be edited. Restore it first.");

    public static readonly Error NotDeleted = Error.Conflict(
        GuideErrorCodes.NotDeleted,
        "This guide has not been deleted.");

    public static readonly Error ScheduleInPast = Error.Validation(
        GuideErrorCodes.ScheduleInPast,
        "A publication can only be scheduled for a future time.");

    public static readonly Error NotScheduled = Error.Conflict(
        GuideErrorCodes.NotScheduled,
        "This guide has no scheduled publication to cancel.");

    public static readonly Error NotDraft = Error.Conflict(
        GuideErrorCodes.NotDraft,
        "This is only possible while the guide is a draft.");

    // ── Content validation ──────────────────────────────────────────────────────────────────

    public static readonly Error TitleInvalid = Error.Validation(
        GuideErrorCodes.TitleInvalid,
        "A guide needs a title between 3 and 200 characters.");

    public static readonly Error SlugInvalid = Error.Validation(
        GuideErrorCodes.SlugInvalid,
        "A web address may contain only lower-case letters, digits and single hyphens.");

    public static readonly Error SlugTaken = Error.Conflict(
        GuideErrorCodes.SlugTaken,
        "That web address is already in use by another guide.");

    public static readonly Error SummaryTooLong = Error.Validation(
        GuideErrorCodes.SummaryTooLong,
        "That summary is too long.");

    public static readonly Error LanguageInvalid = Error.Validation(
        GuideErrorCodes.LanguageInvalid,
        "That is not a language code we recognise.");

    public static readonly Error CountryInvalid = Error.Validation(
        GuideErrorCodes.CountryInvalid,
        "That is not a country code we recognise.");

    public static readonly Error SeoInvalid = Error.Validation(
        GuideErrorCodes.SeoInvalid,
        "Those SEO settings are not valid.");

    public static readonly Error SectionLimit = Error.Validation(
        GuideErrorCodes.SectionLimit,
        "That is more sections than one guide can hold.");

    public static readonly Error SectionInvalid = Error.Validation(
        GuideErrorCodes.SectionInvalid,
        "A section needs a title of up to 200 characters.");

    public static readonly Error BlockLimit = Error.Validation(
        GuideErrorCodes.BlockLimit,
        "That is more blocks than one section can hold.");

    public static readonly Error BlockInvalid = Error.Validation(
        GuideErrorCodes.BlockInvalid,
        "That block is not valid for its kind.");

    public static readonly Error StructureEmpty = Error.Validation(
        GuideErrorCodes.StructureEmpty,
        "A guide needs at least one section.");

    public static readonly Error TagLimit = Error.Validation(
        GuideErrorCodes.TagLimit,
        "That is more tags than one guide can carry.");

    public static readonly Error TagInvalid = Error.Validation(
        GuideErrorCodes.TagInvalid,
        "A tag may contain only lower-case letters, digits and single hyphens, up to 40 characters.");

    public static readonly Error MediaLimit = Error.Conflict(
        GuideErrorCodes.MediaLimit,
        "This guide has reached its maximum number of media attachments.");

    public static readonly Error MediaInvalid = Error.Validation(
        GuideErrorCodes.MediaInvalid,
        "That media reference is not valid.");

    public static readonly Error ProviderReferenceLimit = Error.Conflict(
        GuideErrorCodes.ProviderReferenceLimit,
        "This guide has reached its maximum number of provider references.");

    public static readonly Error ProviderReferenceInvalid = Error.Validation(
        GuideErrorCodes.ProviderReferenceInvalid,
        "That provider reference is not valid.");

    public static readonly Error ReadingProfileInvalid = Error.Validation(
        GuideErrorCodes.ReadingProfileInvalid,
        "That reading profile is not valid.");

    public static readonly Error ModerationNoteInvalid = Error.Validation(
        GuideErrorCodes.ModerationNoteInvalid,
        "A moderation note needs content of up to 2000 characters.");

    public static readonly Error RevisionNotesInvalid = Error.Validation(
        GuideErrorCodes.RevisionNotesInvalid,
        "Those revision notes are too long.");

    public static readonly Error SnapshotInvalid = Error.Validation(
        GuideErrorCodes.SnapshotInvalid,
        "That snapshot is empty or not valid.");

    // ── Categories ──────────────────────────────────────────────────────────────────────────

    public static readonly Error CategoryInactive = Error.Validation(
        GuideErrorCodes.CategoryInactive,
        "That category is no longer available. Choose another.");

    public static readonly Error CategoryKeyInvalid = Error.Validation(
        GuideErrorCodes.CategoryKeyInvalid,
        "A category key may contain only lower-case letters, digits and single hyphens.");

    public static readonly Error CategoryKeyTaken = Error.Conflict(
        GuideErrorCodes.CategoryKeyTaken,
        "A category with that key already exists.");

    // ── Verification ────────────────────────────────────────────────────────────────────────

    public static readonly Error VerificationUnavailable = Error.ServiceUnavailable(
        GuideErrorCodes.VerificationUnavailable,
        "Verification is not available on this deployment.");

    public static readonly Error VerificationAlreadyPending = Error.Conflict(
        GuideErrorCodes.VerificationAlreadyPending,
        "This guide's verification is already being reviewed.");

    public static readonly Error VerificationNotPending = Error.Conflict(
        GuideErrorCodes.VerificationNotPending,
        "This guide has no verification under review.");

    public static readonly Error VerificationRefused = Error.Conflict(
        GuideErrorCodes.VerificationRefused,
        "Verification could not be submitted for this guide.");

    // ── Engagement ──────────────────────────────────────────────────────────────────────────

    public static readonly Error VoteUnchanged = Error.Conflict(
        GuideErrorCodes.VoteUnchanged,
        "Your vote already says that.");

    public static readonly Error BookmarkFailed = Error.Unexpected(
        GuideErrorCodes.BookmarkFailed,
        "We could not save that just now.");

    public static readonly Error ReportFailed = Error.Unexpected(
        GuideErrorCodes.ReportFailed,
        "We could not submit that report just now.");

    public static readonly Error SelfVote = Error.Forbidden(
        GuideErrorCodes.SelfVote,
        "You cannot vote on your own guide.");

    // ── Suggestions ─────────────────────────────────────────────────────────────────────────

    public static readonly Error SuggestionClosed = Error.Conflict(
        GuideErrorCodes.SuggestionClosed,
        "That suggestion has already been decided.");

    public static readonly Error SuggestionNotAuthor = Error.Forbidden(
        GuideErrorCodes.SuggestionNotAuthor,
        "That is not your suggestion.");

    public static readonly Error SuggestionSelf = Error.Forbidden(
        GuideErrorCodes.SuggestionSelf,
        "You cannot suggest an improvement to your own guide. Edit it instead.");

    // ── Localization ────────────────────────────────────────────────────────────────────────

    public static readonly Error TranslationExists = Error.Conflict(
        GuideErrorCodes.TranslationExists,
        "A translation into that language already exists.");

    public static readonly Error TranslationSelf = Error.Validation(
        GuideErrorCodes.TranslationSelf,
        "A guide cannot be a translation of itself.");

    // ── Rate limiting ───────────────────────────────────────────────────────────────────────

    public static readonly Error TooManyGuides = Error.Conflict(
        GuideErrorCodes.TooManyGuides,
        "You have reached the maximum number of guides one member may author.");
}
