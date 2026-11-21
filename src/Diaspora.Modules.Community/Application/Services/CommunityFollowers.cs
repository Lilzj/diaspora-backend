using Diaspora.Contracts.Community.V1;
using Diaspora.Modules.Community.Domain.Follows;
using Diaspora.Modules.Community.Integration;

namespace Diaspora.Modules.Community.Application.Services;

/// <summary>
/// The follow graph, for another module.
/// <para>
/// Deliberately thin, and the whole of its value is which assembly holds the reference: B18
/// injected <c>IFollowRepository</c> until 21 November 2026, so a module that owns creators held
/// B6's repository — the type that can also follow, unfollow and mute. Three methods of it are a
/// read; the rest are not B18's to have.
/// </para>
/// </summary>
public sealed class CommunityFollowers : ICommunityFollowers
{
    private readonly IFollowRepository _follows;

    public CommunityFollowers(IFollowRepository follows) => _follows = follows;

    public Task<int> CountFollowersAsync(
        FollowTargetType targetType, Guid targetId, CancellationToken cancellationToken = default) =>
        _follows.CountFollowersAsync(targetType, targetId, cancellationToken);

    public Task<IReadOnlyList<Guid>> ListFollowerPageAsync(
        FollowTargetType targetType,
        Guid targetId,
        int skip,
        int take,
        CancellationToken cancellationToken = default) =>
        _follows.ListFollowerPageAsync(targetType, targetId, skip, take, cancellationToken);

    public Task<IReadOnlyList<Guid>> ListUnmutedFollowerPageAsync(
        FollowTargetType targetType,
        Guid targetId,
        int skip,
        int take,
        CancellationToken cancellationToken = default) =>
        _follows.ListUnmutedFollowerPageAsync(targetType, targetId, skip, take, cancellationToken);
}
