using Diaspora.Application.Abstractions;
using Diaspora.Modules.Recommendations.Contracts.V1.Responses;

namespace Diaspora.Modules.Recommendations.Application.Profiles;

/// <summary>
/// Folds the caller's pending signals in now.
/// <para>
/// <b>Cheap, and deliberately not rate-limited.</b> An incremental fold reads only what the sweep has
/// not processed, so a member tapping refresh repeatedly does almost nothing after the first. That
/// is the difference between this and a rebuild, which is why they are separate commands rather than
/// one with a flag.
/// </para>
/// </summary>
[Authorize(Permission = RecommendationPermissions.ProfileManage)]
[Transactional(Reason = "The fold, the processed stamps and the profile commit together.")]
[Auditable(Action = "recommendations.profile.refresh")]
public sealed record RefreshProfileCommand : ICommand<PersonalizationProfileResponse>;
