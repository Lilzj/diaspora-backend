using Diaspora.Application.Abstractions;
using Diaspora.Application.Identity.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Domain.Identity.Errors;
using Diaspora.Domain.Identity.Repositories;
using Diaspora.Domain.Identity.ValueObjects;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Application.Identity.Commands.Profile.ChangeAvatar;

public sealed class ChangeAvatarCommandHandler : ICommandHandler<ChangeAvatarCommand, AvatarChange>
{
    private readonly IUserRepository _users;
    private readonly ICurrentIdentityContext _currentIdentity;
    private readonly IAvatarUrlResolver _avatars;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public ChangeAvatarCommandHandler(
        IUserRepository users,
        ICurrentIdentityContext currentIdentity,
        IAvatarUrlResolver avatars,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _users = users;
        _currentIdentity = currentIdentity;
        _avatars = avatars;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<AvatarChange>> Handle(
        ChangeAvatarCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userId = _currentIdentity.UserId;

        if (userId is null)
        {
            return Result.Failure<AvatarChange>(UserErrors.InvalidCredentials);
        }

        var user = await _users.GetByIdAsync(userId, cancellationToken);

        if (user is null)
        {
            return Result.Failure<AvatarChange>(UserErrors.NotFound);
        }

        Guid? previous = AvatarKeys.TryGetMediaId(user.Avatar?.StorageKey, out var previousId)
            ? previousId
            : null;

        var avatar = request.MediaId is { } mediaId
            ? AvatarReference.Create(AvatarKeys.ForMedia(mediaId))
            : null;

        var set = user.SetAvatar(avatar, _clock.UtcNow);

        if (set.IsFailure)
        {
            return Result.Failure<AvatarChange>(set.Error);
        }

        _users.Update(user);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new AvatarChange(
            request.MediaId,
            _avatars.Resolve(user.Avatar?.StorageKey),
            previous == request.MediaId ? null : previous));
    }
}
