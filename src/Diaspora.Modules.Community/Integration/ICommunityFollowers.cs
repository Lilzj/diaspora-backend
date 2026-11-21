using Diaspora.Contracts.Community.V1;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Community.Integration;

/// <summary>
/// Who follows something, for a module that needs an audience rather than a feed.
/// <para>
/// <b>Distinct from <see cref="ICommunitySocialService"/>, which counts reactions on a piece of
/// content.</b> This one is about the follow graph: how many people follow a member, an
/// organisation or a community, and who they are in a stable order. B18 asks before it estimates
/// an automation's audience and again before it broadcasts to one.
/// </para>
/// <para>
/// <b>Counted from the graph, never from a cached counter.</b> A profile's stored follower number
/// is reconciled on a sweep and is right most of the time; a figure shown to somebody about to
/// notify people has to be right now.
/// </para>
/// </summary>
public interface ICommunityFollowers
{
    /// <summary>How many follow this target.</summary>
    Task<int> CountFollowersAsync(
        FollowTargetType targetType, Guid targetId, CancellationToken cancellationToken = default);

    /// <summary>
    /// A page of follower identifiers, <b>oldest follow first</b>.
    /// <para>
    /// The ordering is the point. A descending walk shifts under a follow gained mid-broadcast, so
    /// somebody is skipped and somebody is notified twice; ascending by when they followed is
    /// stable against anything arriving while the page is being read.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<Guid>> ListFollowerPageAsync(
        FollowTargetType targetType,
        Guid targetId,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The same page without the muted edges.
    /// <para>
    /// For telling a target's followers something rather than counting them: a member who muted
    /// the target asked not to hear from it, and a broadcast that ignored that would be the one
    /// message a mute cannot stop.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<Guid>> ListUnmutedFollowerPageAsync(
        FollowTargetType targetType,
        Guid targetId,
        int skip,
        int take,
        CancellationToken cancellationToken = default);
}
