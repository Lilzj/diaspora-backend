namespace Diaspora.Modules.Guides;

/// <summary>
/// The stable wire-level error codes the Country Knowledge Directory emits.
/// <para>
/// Prefixed <c>knowledge.</c> rather than <c>guides.</c>, even though the Guides module owns and
/// emits them. The prefix names the surface a client is talking to, and a client calling
/// <c>/api/v1/knowledge</c> that receives <c>guides.not-found</c> has to know B8.1 lives inside B8
/// to make sense of it — which is precisely the implementation detail a wire contract should not
/// leak. Where a failure genuinely is about a guide, the code stays <c>guides.</c>: a curator
/// featuring a guide that does not exist gets B8's answer, because that is B8's fact.
/// </para>
/// <para>
/// These strings are frozen. Change a description freely; never change a code. New failures get
/// new codes.
/// </para>
/// </summary>
public static class KnowledgeErrorCodes
{
    // Existence
    public const string CategoryNotFound = "knowledge.category.not-found";
    public const string DirectoryNotFound = "knowledge.directory.not-found";
    public const string LearningPathNotFound = "knowledge.path.not-found";
    public const string StepNotFound = "knowledge.path.step.not-found";

    // Keys and identity
    public const string CategoryKeyInvalid = "knowledge.category.key-invalid";
    public const string CategoryKeyTaken = "knowledge.category.key-taken";
    public const string CategoryNameInvalid = "knowledge.category.name-invalid";
    public const string PathKeyInvalid = "knowledge.path.key-invalid";
    public const string PathKeyTaken = "knowledge.path.key-taken";
    public const string CountryInvalid = "knowledge.country.invalid";
    public const string LocaleInvalid = "knowledge.locale.invalid";
    public const string DirectoryExists = "knowledge.directory.exists";

    // Lifecycle
    public const string CategoryArchived = "knowledge.category.archived";
    public const string CategoryNotArchived = "knowledge.category.not-archived";
    public const string CategoryInUse = "knowledge.category.in-use";
    public const string DirectoryNotPublished = "knowledge.directory.not-published";
    public const string DirectoryAlreadyPublished = "knowledge.directory.already-published";
    public const string PathNotPublishable = "knowledge.path.not-publishable";
    public const string PathAlreadyPublished = "knowledge.path.already-published";
    public const string PathArchived = "knowledge.path.archived";

    // Curation
    public const string GuideNotDiscoverable = "knowledge.guide.not-discoverable";
    public const string GuideCountryMismatch = "knowledge.guide.country-mismatch";
    public const string AlreadyFeatured = "knowledge.featured.already";
    public const string NotFeatured = "knowledge.featured.absent";
    public const string FeaturedLimit = "knowledge.featured.limit";
    public const string EntryLimit = "knowledge.directory.entry-limit";
    public const string EntryDuplicate = "knowledge.directory.entry-duplicate";
    public const string StepLimit = "knowledge.path.step-limit";
    public const string StepInvalid = "knowledge.path.step-invalid";
    public const string RecommendationLimit = "knowledge.recommendations.limit";
    public const string RecommendationSelf = "knowledge.recommendations.self";
    public const string SourceLimit = "knowledge.category.source-limit";
    public const string LocalizationLimit = "knowledge.localizations.limit";

    // Concurrency and authorization
    public const string RevisionConflict = "knowledge.revision-conflict";
    public const string NotAuthenticated = "knowledge.not-authenticated";
    public const string NotCurator = "knowledge.not-curator";

    // Discovery
    public const string SearchUnavailable = "knowledge.search.unavailable";
    public const string SearchTermInvalid = "knowledge.search.term-invalid";
}
