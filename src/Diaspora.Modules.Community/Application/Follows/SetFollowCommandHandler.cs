using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Community.Application.Services;
using Diaspora.Modules.Community.Contracts.V1.Responses;
using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Errors;
using Diaspora.Modules.Community.Domain.Follows;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Community.Application.Follows;

/// <summary>
/// Follows or unfollows.
/// <para>
/// One command for both directions because the client's control is a toggle, and two endpoints
/// for a toggle is how a double tap produces a follow and an unfollow in an order nobody can
/// predict.
/// </para>
/// </summary>
public sealed class SetFollowCommandHandler : ICommandHandler<SetFollowCommand, FollowResponse>
{
    private readonly IFollowRepository _follows;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public SetFollowCommandHandler(
        IFollowRepository follows,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _follows = follows;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<FollowResponse>> Handle(
        SetFollowCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userId = _currentUser.UserId;

        if (userId is null)
        {
            return Result.Failure<FollowResponse>(CommunityErrors.NotAuthenticated);
        }

        var targetType = CommunityWireNames.ParseOr(request.TargetType, FollowTargetType.Member);

        var existing = await _follows.FindAsync(
            userId, targetType, request.TargetId, cancellationToken);

        if (!request.Follow)
        {
            if (existing is null)
            {
                return Result.Failure<FollowResponse>(CommunityErrors.FollowNotFound);
            }

            existing.RaiseRemoved();
            _follows.Remove(existing);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success(new FollowResponse
            {
                FollowId = existing.Id.Value,
                FollowerId = userId.Value,
                FolloweeId = request.TargetId,
                TargetType = CommunityWireNames.ToWire(targetType),
                IsMuted = false,
                FollowerCount = await _follows.CountFollowersAsync(
                    targetType, request.TargetId, cancellationToken),
                CreatedAt = existing.FollowedAt
            });
        }

        if (existing is not null)
        {
            existing.SetMuted(request.Muted);
        }
        else
        {
            var created = Follow.Create(userId, targetType, request.TargetId, _clock.UtcNow);

            if (created.IsFailure)
            {
                return Result.Failure<FollowResponse>(created.Error);
            }

            existing = created.Value;
            existing.SetMuted(request.Muted);

            await _follows.AddAsync(existing, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new FollowResponse
        {
            FollowId = existing.Id.Value,
            FollowerId = userId.Value,
            FolloweeId = request.TargetId,
            TargetType = CommunityWireNames.ToWire(targetType),
            IsMuted = existing.IsMuted,
            FollowerCount = await _follows.CountFollowersAsync(
                targetType, request.TargetId, cancellationToken),
            CreatedAt = existing.FollowedAt
        });
    }
}
