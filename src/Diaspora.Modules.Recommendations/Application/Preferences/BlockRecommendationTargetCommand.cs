using Diaspora.Application.Abstractions;
using Diaspora.Modules.Recommendations.Contracts.V1.Responses;

namespace Diaspora.Modules.Recommendations.Application.Preferences;

/// <summary>
/// Never show the caller this again.
/// <para>
/// <b>Deliberately separate from a dismissal, and the difference is permanence.</b> A dismissal is
/// "not now" and expires; a block does not. One command that took a duration would put the
/// distinction in a parameter somebody could get wrong, and getting it wrong in one direction means
/// re-showing a member the seller they blocked.
/// </para>
/// <para>
/// <see cref="Remove"/> is on the same command rather than in a second one, because blocking and
/// unblocking are one toggle on one screen and splitting them would be two endpoints for one
/// control.
/// </para>
/// </summary>
[Authorize(Permission = RecommendationPermissions.PreferencesManage)]
[Transactional(Reason = "The block and the preference record commit together.")]
[Auditable(Action = "recommendations.preferences.block")]
public sealed record BlockRecommendationTargetCommand : ICommand<PersonalizationPreferencesResponse>
{
    public required string TargetType { get; init; }

    public required Guid TargetId { get; init; }

    /// <summary>Unblocks instead. See the class summary.</summary>
    public bool Remove { get; init; }
}
