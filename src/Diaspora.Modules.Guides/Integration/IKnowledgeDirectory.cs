using Diaspora.Modules.Guides.Domain.Identifiers;

namespace Diaspora.Modules.Guides.Integration;

/// <summary>
/// The canonical way every other module reaches country-specific knowledge.
/// <para>
/// <b>This interface is the phase.</b> B11 and B12 each arrived at the same requirement
/// independently — surface the guides that apply to a country, in a sensible order, without
/// reimplementing what "sensible" means — and the answer was going to be a fourth, fifth and sixth
/// private implementation of guide discovery, each with its own idea of whether an unverified guide
/// counts, each with its own bug in the fallback when a country has nothing. There is one now, and
/// it is here.
/// </para>
/// <para>
/// <b>Deliberately narrower than the query surface.</b> A consumer needs to know what a country
/// covers, which guides answer a subject, and where onboarding starts. It does not need to arrange a
/// directory, feature a guide or read an unpublished one, and handing it the repositories would give
/// it all three. Everything here answers from published, reader-visible state only — a consumer
/// cannot see a draft directory even if it asks.
/// </para>
/// <para>
/// <b>What it deliberately does not offer.</b> There is no method that takes a member and returns
/// what <em>they</em> should read: personalized onboarding is out of scope for this phase, and a
/// signature that accepted a user identifier would be an invitation to build it behind this
/// interface where no consumer could see it happening.
/// </para>
/// </summary>
public interface IKnowledgeDirectory
{
    /// <summary>
    /// What the platform knows about a country, or null when it has no published directory.
    /// <para>
    /// Null rather than an empty summary, because the two mean different things to a consumer: no
    /// directory means "do not offer a knowledge section for this country at all", and an empty one
    /// would mean "offer it, and show nothing".
    /// </para>
    /// </summary>
    Task<CountryKnowledgeSummary?> FindCountryAsync(
        string countryCode, string? locale = null, CancellationToken cancellationToken = default);

    /// <summary>Every country with a published directory. What a country picker renders.</summary>
    Task<IReadOnlyList<CountryKnowledgeSummary>> ListCountriesAsync(
        string? locale = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// The navigation a country offers, in curated order, with guide counts.
    /// <para>
    /// The counts are what make this useful to a consumer rather than merely available: a Housing
    /// screen deciding whether to show a "moving here" link wants to know there is something behind
    /// it.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<KnowledgeCategoryDescriptor>> ListCategoriesAsync(
        string countryCode, string? locale = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// The guides that answer one subject for one country, best first.
    /// <para>
    /// <b>The method that replaces every private guide lookup on the platform.</b> A Jobs screen
    /// asks for <c>employment</c>, a Housing screen for <c>housing</c>, a Marketplace screen for
    /// <c>safety</c> — and every one of them gets curated ordering, the country's own arrangement
    /// and the same visibility rules, without knowing that any of those exist.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<GuideSummary>> ListCategoryGuidesAsync(
        string countryCode,
        string knowledgeCategoryKey,
        int limit = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// What a curator put at the top for a country, across every subject.
    /// <para>
    /// The answer to "what should somebody arriving here read first", which is the question every
    /// vertical wanted and none of them could answer alone.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<GuideSummary>> ListFeaturedGuidesAsync(
        string countryCode, int limit = 10, CancellationToken cancellationToken = default);

    /// <summary>Published onboarding routes for a country. Titles and lengths, never steps.</summary>
    Task<IReadOnlyList<LearningPathDescriptor>> ListLearningPathsAsync(
        string countryCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// What a curator says to read after a guide.
    /// <para>
    /// Offered to consumers because the guide detail screen is not the only place somebody finishes
    /// reading: a Housing listing that embedded a tenancy guide should be able to offer the next one
    /// without inventing its own idea of what follows.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<GuideSummary>> ListRecommendedReadsAsync(
        GuideId sourceGuideId, int limit = 5, CancellationToken cancellationToken = default);
}
