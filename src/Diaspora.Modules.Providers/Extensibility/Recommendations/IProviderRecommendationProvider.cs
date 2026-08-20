using Diaspora.Modules.Providers.Domain.Identifiers;

namespace Diaspora.Modules.Providers.Extensibility.Recommendations;

/// <summary>
/// How a module suggests providers a member might want.
/// <para>
/// <b>The seam the brief's "Do not implement complex recommendation algorithms" makes
/// necessary.</b> The client already has a "Similar Providers" section on its provider screen,
/// and it fills it with <c>mockListings.filter(l =&gt; l.listingId !== current).slice(0, 2)</c>
/// — literally the first two other providers. Something has to answer that today, and it must
/// not be a recommender B7 has no business owning.
/// </para>
/// <para>
/// B7 ships <c>SimilarCategoryRecommendations</c>: same category, nearby, well rated. It is a
/// heuristic, it is honest about being one, and it is right often enough for a "you might also
/// like" strip. B17 registers something better and this file does not change.
/// </para>
/// <para>
/// <b>Returns identifiers, not providers.</b> A recommender that returned aggregates would
/// bypass the visibility filter and could surface a private or suspended provider on a public
/// screen. B7 loads and filters what comes back, so a recommender cannot leak.
/// </para>
/// </summary>
public interface IProviderRecommendationProvider
{
    /// <summary>The module contributing.</summary>
    string Module { get; }

    /// <summary>Lower runs first, and the first non-empty result wins. B7's heuristic is 1000.</summary>
    int Order => 100;

    /// <summary>
    /// Suggests providers, best first.
    /// <para>
    /// An empty list hands on to the next provider — so a recommender with no model for a
    /// category, or with a cold cache, degrades to the heuristic instead of leaving the strip
    /// blank.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<ProviderId>> RecommendAsync(
        RecommendationRequest request, CancellationToken cancellationToken = default);
}
