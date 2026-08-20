using Diaspora.Application.Abstractions;
using Diaspora.Modules.Recommendations.Contracts.V1.Responses;

namespace Diaspora.Modules.Recommendations.Application.Profiles;

/// <summary>
/// Recomputes the caller's profile from their whole retained history.
/// <para>
/// <b>Rate-limited, unlike the refresh, because it is expensive and because it is the button a
/// member presses when they think the platform has got them wrong.</b> A cooldown that says "we just
/// did this, give it a moment" is a better answer than letting somebody run a full history scan
/// every second while they wait for the feed to change.
/// </para>
/// <para>
/// <b>Keeps everything the member stated and discards every inference.</b> That distinction is
/// enforced by the aggregate rather than by this command — see
/// <c>PersonalizationProfile.BeginRebuild</c> — so a future rebuild path written by somebody else
/// still cannot discard a stated preference.
/// </para>
/// </summary>
[Authorize(Permission = RecommendationPermissions.ProfileManage)]
[Transactional(Reason = "A rebuild empties and repopulates the profile; a partial one is worse than none.")]
[Auditable(Action = "recommendations.profile.rebuild")]
public sealed record RebuildProfileCommand : ICommand<PersonalizationProfileResponse>;
