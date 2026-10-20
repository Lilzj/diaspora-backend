using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Community.Domain.Follows;

/// <summary>Reads and writes the follow graph.</summary>
public interface IFollowRepository
{
    Task<Follow?> FindAsync(
        UserId followerId,
        FollowTargetType targetType,
        Guid targetId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Every target a member follows, of one kind. The following feed's first query, and the
    /// reason the edge carries a mute flag: a muted edge is excluded here and nowhere else.
    /// </summary>
    Task<IReadOnlyList<Guid>> ListTargetsAsync(
        UserId followerId,
        FollowTargetType targetType,
        bool includeMuted,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> ListFollowersAsync(
        FollowTargetType targetType,
        Guid targetId,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// One page of a target's followers, oldest first — B23.x Wave 12.
    /// <para>
    /// <b>Distinct from <see cref="ListFollowersAsync"/>, and the ordering is why.</b> That one is
    /// newest-first and capped, which is what a profile's "followers" list wants and is exactly wrong
    /// for walking a whole audience: a follower gained between two pages shifts every later page by
    /// one under a descending sort, so somebody is skipped and somebody is sent to twice. Ascending by
    /// the moment they followed is stable, because the past does not change.
    /// </para>
    /// <para>
    /// Added for the automation platform's follower broadcast, which pages through an audience under a
    /// ceiling. Nothing else uses it, and it returns identifiers rather than edges because a broadcast
    /// needs nothing else about the relationship.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<Guid>> ListFollowerPageAsync(
        FollowTargetType targetType,
        Guid targetId,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// <see cref="ListFollowerPageAsync"/> without the muted edges. For telling a target's followers
    /// something: a member who muted the target asked not to hear from it.
    /// </summary>
    Task<IReadOnlyList<Guid>> ListUnmutedFollowerPageAsync(
        FollowTargetType targetType,
        Guid targetId,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    Task<int> CountFollowersAsync(
        FollowTargetType targetType, Guid targetId, CancellationToken cancellationToken = default);

    Task<int> CountFollowingAsync(
        UserId followerId, CancellationToken cancellationToken = default);

    Task<IReadOnlySet<Guid>> FilterFollowedAsync(
        UserId followerId,
        FollowTargetType targetType,
        IReadOnlyCollection<Guid> targetIds,
        CancellationToken cancellationToken = default);

    Task AddAsync(Follow follow, CancellationToken cancellationToken = default);

    void Remove(Follow follow);
}
