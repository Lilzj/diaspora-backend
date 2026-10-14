namespace Diaspora.Modules.Guides;

/// <summary>
/// The stable wire-level error codes the Guides module emits.
/// <para>
/// These strings are frozen contract: clients switch on them, support searches logs by them,
/// and translations key off them. Change the description on an <c>Error</c> freely; never
/// change a code. New failures get new codes.
/// </para>
/// </summary>
public static class GuideErrorCodes
{
    // Existence & visibility
    public const string NotFound = "guides.not-found";
    public const string VersionNotFound = "guides.version.not-found";
    public const string CategoryNotFound = "guides.category.not-found";
    public const string SuggestionNotFound = "guides.suggestion.not-found";
    public const string DraftNotVisible = "guides.draft.not-visible";
    public const string Hidden = "guides.hidden";
    public const string Deleted = "guides.deleted";

    // Authorship & authorization
    public const string NotAuthenticated = "guides.not-authenticated";
    public const string NotAuthor = "guides.not-author";
    public const string NotModerator = "guides.not-moderator";

    // Concurrency & lifecycle
    public const string RevisionConflict = "guides.revision-conflict";
    public const string NotPublishable = "guides.not-publishable";
    public const string AlreadyPublished = "guides.already-published";
    public const string NotPublished = "guides.not-published";
    public const string NotArchived = "guides.not-archived";
    public const string ArchivedImmutable = "guides.archived-immutable";
    public const string NotDeleted = "guides.not-deleted";
    public const string ScheduleInPast = "guides.schedule.in-past";
    public const string NotScheduled = "guides.not-scheduled";
    public const string NotDraft = "guides.not-draft";

    // Content validation
    public const string TitleInvalid = "guides.title.invalid";
    public const string SlugInvalid = "guides.slug.invalid";
    public const string SlugTaken = "guides.slug.taken";
    public const string SummaryTooLong = "guides.summary.too-long";
    public const string LanguageInvalid = "guides.language.invalid";
    public const string CountryInvalid = "guides.country.invalid";
    public const string CoordinatesInvalid = "guides.coordinates.invalid";
    public const string SeoInvalid = "guides.seo.invalid";
    public const string SectionLimit = "guides.sections.limit";
    public const string SectionInvalid = "guides.section.invalid";
    public const string BlockLimit = "guides.blocks.limit";
    public const string BlockInvalid = "guides.block.invalid";
    public const string StructureEmpty = "guides.structure.empty";
    public const string TagLimit = "guides.tags.limit";
    public const string TagInvalid = "guides.tag.invalid";
    public const string MediaLimit = "guides.media.limit";
    public const string MediaInvalid = "guides.media.invalid";
    public const string ProviderReferenceLimit = "guides.provider-references.limit";
    public const string ProviderReferenceInvalid = "guides.provider-reference.invalid";
    public const string ReadingProfileInvalid = "guides.reading-profile.invalid";
    public const string ModerationNoteInvalid = "guides.moderation-note.invalid";
    public const string RevisionNotesInvalid = "guides.revision-notes.invalid";
    public const string SnapshotInvalid = "guides.snapshot.invalid";

    // Categories
    public const string CategoryInactive = "guides.category.inactive";
    public const string CategoryKeyInvalid = "guides.category.key-invalid";
    public const string CategoryKeyTaken = "guides.category.key-taken";

    // Verification
    public const string VerificationUnavailable = "guides.verification.unavailable";
    public const string VerificationAlreadyPending = "guides.verification.already-pending";
    public const string VerificationNotPending = "guides.verification.not-pending";
    public const string VerificationRefused = "guides.verification.refused";

    // Engagement
    public const string VoteUnchanged = "guides.vote.unchanged";
    public const string BookmarkFailed = "guides.bookmark.failed";
    public const string ReportFailed = "guides.report.failed";
    public const string SelfVote = "guides.vote.self";

    // Suggestions
    public const string SuggestionClosed = "guides.suggestion.closed";
    public const string SuggestionNotAuthor = "guides.suggestion.not-author";
    public const string SuggestionSelf = "guides.suggestion.self";

    // Localization
    public const string TranslationExists = "guides.translation.exists";
    public const string TranslationSelf = "guides.translation.self";

    // Rate limiting
    public const string TooManyGuides = "guides.too-many";
}
