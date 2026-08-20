using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Domain.Errors;

/// <summary>
/// Every failure the Country Knowledge Directory can return, as constructed
/// <see cref="Error"/> values.
/// <para>
/// Kept apart from <see cref="GuideErrors"/> rather than folded into it, and the reason is
/// ownership rather than tidiness: B8 answers questions about guides, B8.1 answers questions
/// about how guides are organised, and a single file holding both would make it impossible to
/// see at a glance which failures a consumer of the discovery layer can actually receive.
/// </para>
/// <para>
/// The <see cref="ErrorType"/> on each decides the HTTP status through <c>PlatformResults</c>;
/// no file in this module names a number. <see cref="RevisionConflict"/> in particular must be a
/// conflict — it is the signal a curation UI uses to offer a reload rather than an error page,
/// and two editors arranging the same country at once is the ordinary case, not the exceptional
/// one.
/// </para>
/// </summary>
public static class KnowledgeErrors
{
    // ── Existence ───────────────────────────────────────────────────────────────────────────

    public static readonly Error CategoryNotFound = Error.NotFound(
        KnowledgeErrorCodes.CategoryNotFound,
        "That knowledge category does not exist.");

    public static readonly Error DirectoryNotFound = Error.NotFound(
        KnowledgeErrorCodes.DirectoryNotFound,
        "There is no knowledge directory for that country yet.");

    public static readonly Error LearningPathNotFound = Error.NotFound(
        KnowledgeErrorCodes.LearningPathNotFound,
        "That learning path does not exist.");

    public static readonly Error StepNotFound = Error.NotFound(
        KnowledgeErrorCodes.StepNotFound,
        "That step is not part of this learning path.");

    // ── Keys and identity ───────────────────────────────────────────────────────────────────

    public static readonly Error CategoryKeyInvalid = Error.Validation(
        KnowledgeErrorCodes.CategoryKeyInvalid,
        "A category key may contain only lower-case letters, digits and single hyphens.");

    public static readonly Error CategoryKeyTaken = Error.Conflict(
        KnowledgeErrorCodes.CategoryKeyTaken,
        "A knowledge category with that key already exists.");

    public static readonly Error CategoryNameInvalid = Error.Validation(
        KnowledgeErrorCodes.CategoryNameInvalid,
        "A knowledge category needs a name of up to 120 characters.");

    public static readonly Error PathKeyInvalid = Error.Validation(
        KnowledgeErrorCodes.PathKeyInvalid,
        "A learning path key may contain only lower-case letters, digits and single hyphens.");

    public static readonly Error PathKeyTaken = Error.Conflict(
        KnowledgeErrorCodes.PathKeyTaken,
        "A learning path with that key already exists for this country.");

    public static readonly Error CountryInvalid = Error.Validation(
        KnowledgeErrorCodes.CountryInvalid,
        "That is not a country code we recognise.");

    public static readonly Error LocaleInvalid = Error.Validation(
        KnowledgeErrorCodes.LocaleInvalid,
        "That is not a language tag we recognise.");

    public static readonly Error DirectoryExists = Error.Conflict(
        KnowledgeErrorCodes.DirectoryExists,
        "This country already has a knowledge directory.");

    // ── Lifecycle ───────────────────────────────────────────────────────────────────────────

    public static readonly Error CategoryArchived = Error.Conflict(
        KnowledgeErrorCodes.CategoryArchived,
        "That knowledge category is archived. Restore it before using it.");

    public static readonly Error CategoryNotArchived = Error.Conflict(
        KnowledgeErrorCodes.CategoryNotArchived,
        "That knowledge category is not archived.");

    public static readonly Error CategoryInUse = Error.Conflict(
        KnowledgeErrorCodes.CategoryInUse,
        "That category is still placed on a published country directory. Remove it there first.");

    public static readonly Error DirectoryNotPublished = Error.Conflict(
        KnowledgeErrorCodes.DirectoryNotPublished,
        "This country directory has not been published.");

    public static readonly Error DirectoryAlreadyPublished = Error.Conflict(
        KnowledgeErrorCodes.DirectoryAlreadyPublished,
        "This country directory is already published.");

    public static readonly Error PathNotPublishable = Error.Conflict(
        KnowledgeErrorCodes.PathNotPublishable,
        "A learning path needs a title and at least one step before it can be published.");

    public static readonly Error PathAlreadyPublished = Error.Conflict(
        KnowledgeErrorCodes.PathAlreadyPublished,
        "This learning path is already published.");

    public static readonly Error PathArchived = Error.Conflict(
        KnowledgeErrorCodes.PathArchived,
        "An archived learning path cannot be rearranged. Restore it first.");

    // ── Curation ────────────────────────────────────────────────────────────────────────────

    public static readonly Error GuideNotDiscoverable = Error.Validation(
        KnowledgeErrorCodes.GuideNotDiscoverable,
        "That guide is not published, so it cannot be curated onto a directory.");

    public static readonly Error GuideCountryMismatch = Error.Validation(
        KnowledgeErrorCodes.GuideCountryMismatch,
        "That guide is about a different country.");

    public static readonly Error AlreadyFeatured = Error.Conflict(
        KnowledgeErrorCodes.AlreadyFeatured,
        "That guide is already featured on this directory.");

    public static readonly Error NotFeatured = Error.Conflict(
        KnowledgeErrorCodes.NotFeatured,
        "That guide is not featured on this directory.");

    public static readonly Error FeaturedLimit = Error.Conflict(
        KnowledgeErrorCodes.FeaturedLimit,
        "This directory has reached the maximum number of featured guides.");

    public static readonly Error EntryLimit = Error.Validation(
        KnowledgeErrorCodes.EntryLimit,
        "That is more categories than one country directory can list.");

    public static readonly Error EntryDuplicate = Error.Validation(
        KnowledgeErrorCodes.EntryDuplicate,
        "A category can appear on a country directory only once.");

    public static readonly Error StepLimit = Error.Validation(
        KnowledgeErrorCodes.StepLimit,
        "That is more steps than one learning path can hold.");

    public static readonly Error StepInvalid = Error.Validation(
        KnowledgeErrorCodes.StepInvalid,
        "A step needs a title, and must point at a guide or a knowledge category.");

    public static readonly Error RecommendationLimit = Error.Validation(
        KnowledgeErrorCodes.RecommendationLimit,
        "That is more next reads than one guide can recommend.");

    public static readonly Error RecommendationSelf = Error.Validation(
        KnowledgeErrorCodes.RecommendationSelf,
        "A guide cannot recommend itself as the next read.");

    public static readonly Error SourceLimit = Error.Validation(
        KnowledgeErrorCodes.SourceLimit,
        "That is more source categories than one knowledge category can draw from.");

    public static readonly Error LocalizationLimit = Error.Validation(
        KnowledgeErrorCodes.LocalizationLimit,
        "That is more translations than one entry can carry.");

    // ── Concurrency and authorization ───────────────────────────────────────────────────────

    public static readonly Error RevisionConflict = Error.Conflict(
        KnowledgeErrorCodes.RevisionConflict,
        "Somebody else changed this while you were editing it. Reload and try again.");

    public static readonly Error NotAuthenticated = Error.Unauthorized(
        KnowledgeErrorCodes.NotAuthenticated,
        "You must be signed in to do this.");

    public static readonly Error NotCurator = Error.Forbidden(
        KnowledgeErrorCodes.NotCurator,
        "Only knowledge curators can do this.");

    // ── Discovery ───────────────────────────────────────────────────────────────────────────

    public static readonly Error SearchUnavailable = Error.ServiceUnavailable(
        KnowledgeErrorCodes.SearchUnavailable,
        "Knowledge search is not available on this deployment.");

    public static readonly Error SearchTermInvalid = Error.Validation(
        KnowledgeErrorCodes.SearchTermInvalid,
        "That search phrase is too long.");
}
