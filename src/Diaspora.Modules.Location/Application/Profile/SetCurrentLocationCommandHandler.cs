using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Location.Application.Mapping;
using Diaspora.Modules.Location.Application.Services;
using Diaspora.Modules.Location.Contracts.V1.Responses;
using Diaspora.Modules.Location.Domain.Errors;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Location.Application.Profile;

/// <summary>Moves the caller and records the place in their recents.</summary>
public sealed class SetCurrentLocationCommandHandler
    : ICommandHandler<SetCurrentLocationCommand, LocationProfileResponse>
{
    private readonly LocationProfileProvisioner _profiles;
    private readonly LocationSnapshotFactory _snapshots;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public SetCurrentLocationCommandHandler(
        LocationProfileProvisioner profiles,
        LocationSnapshotFactory snapshots,
        ICurrentUser currentUser,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _profiles = profiles;
        _snapshots = snapshots;
        _currentUser = currentUser;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<LocationProfileResponse>> Handle(
        SetCurrentLocationCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userId = _currentUser.UserId;

        if (userId is null)
        {
            return Result.Failure<LocationProfileResponse>(LocationErrors.NotAuthenticated);
        }

        var snapshot = await _snapshots.CreateAsync(request.Location, cancellationToken);

        if (snapshot.IsFailure)
        {
            return Result.Failure<LocationProfileResponse>(snapshot.Error);
        }

        var profile = await _profiles.GetOrCreateAsync(userId, cancellationToken);

        var moved = profile.SetCurrentLocation(snapshot.Value, _clock.UtcNow);

        if (moved.IsFailure)
        {
            return Result.Failure<LocationProfileResponse>(moved.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(ProfileMapper.ToResponse(profile));
    }
}
