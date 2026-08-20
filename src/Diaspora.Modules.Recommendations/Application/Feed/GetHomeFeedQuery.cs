using Diaspora.Application.Abstractions;
using Diaspora.Modules.Recommendations.Contracts.V1.Responses;

namespace Diaspora.Modules.Recommendations.Application.Feed;

/// <summary>
/// The caller's home feed.
/// <para>
/// <b>Takes no user identifier, which is the security model of every member-facing query in this
/// module.</b> The caller comes from the token; reading somebody else's feed is unexpressible rather
/// than merely forbidden.
/// </para>
/// </summary>
[Authorize(Permission = RecommendationPermissions.FeedRead)]
public sealed record GetHomeFeedQuery : IQuery<HomeFeedResponse>
{
    /// <summary>
    /// Overrides the member's country — the country-page case.
    /// <para>
    /// Applied to the candidate query rather than persisted, so a member who looks at Canada once
    /// does not become permanently Canadian.
    /// </para>
    /// </summary>
    public string? CountryCode { get; init; }
}
