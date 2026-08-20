using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Recommendations.Extensibility.Search;

/// <summary>
/// What this module contributes to somebody else's search.
/// <para>
/// <b>The whole of the search integration, and it is deliberately this small.</b> The brief asks for
/// boost signals, personalized ranking signals and trending signals, and warns that a future
/// OpenSearch integration must require infrastructure changes only. Both are satisfied by refusing
/// to return results: a search engine that consumes boosts stays the owner of matching, and swapping
/// its implementation never touches this module.
/// </para>
/// </summary>
public interface ISearchSignalProvider
{
    /// <summary>
    /// Personalization boosts for one member.
    /// <para>
    /// Returns nothing when the member has turned personalization off, which is what makes the
    /// opt-out reach a surface this module does not own.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<SearchBoostSignal>> GetBoostSignalsAsync(
        UserId userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// What is moving right now, regardless of who is asking.
    /// <para>
    /// Not personalised, and therefore cacheable across every member in a country — which is what
    /// makes a trending row on a search screen affordable.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<SearchBoostSignal>> GetTrendingSignalsAsync(
        string? countryCode,
        RecommendationTargetType? targetType,
        CancellationToken cancellationToken = default);
}
