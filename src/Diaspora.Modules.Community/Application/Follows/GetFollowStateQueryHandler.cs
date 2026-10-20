using Diaspora.Application.Abstractions;
using Diaspora.Modules.Community.Application.Services;
using Diaspora.Modules.Community.Contracts.V1.Responses;
using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Errors;
using Diaspora.Modules.Community.Domain.Follows;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Community.Application.Follows;

/// <summary>
/// Reads the caller's follow edge to one target and the target's follower count. A muted follow is
/// still a follow: muting quiets the feed, it does not unfollow.
/// </summary>
public sealed class GetFollowStateQueryHandler
    : IQueryHandler<GetFollowStateQuery, FollowStateResponse>
{
    private readonly IFollowRepository _follows;
    private readonly ICurrentUser _currentUser;

    public GetFollowStateQueryHandler(IFollowRepository follows, ICurrentUser currentUser)
    {
        _follows = follows;
        _currentUser = currentUser;
    }

    public async Task<Result<FollowStateResponse>> Handle(
        GetFollowStateQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userId = _currentUser.UserId;

        if (userId is null)
        {
            return Result.Failure<FollowStateResponse>(CommunityErrors.NotAuthenticated);
        }

        // The same default the follow route applies, so the two cannot disagree about what an
        // unnamed target type means.
        var targetType = CommunityWireNames.ParseOr(request.TargetType, FollowTargetType.Member);

        var existing = await _follows.FindAsync(
            userId, targetType, request.TargetId, cancellationToken);

        return Result.Success(new FollowStateResponse
        {
            IsFollowing = existing is not null,
            FollowerCount = await _follows.CountFollowersAsync(
                targetType, request.TargetId, cancellationToken)
        });
    }
}
