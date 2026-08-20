using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Location.Application.Mapping;
using Diaspora.Modules.Location.Application.Services;
using Diaspora.Modules.Location.Contracts.V1.Responses;
using Diaspora.Modules.Location.Domain.Errors;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Location.Application.Profile;

/// <summary>Toggles travel mode, preserving the previous destination when none is supplied.</summary>
public sealed class SetTravelModeCommandHandler
    : ICommandHandler<SetTravelModeCommand, LocationProfileResponse>
{
    private readonly LocationProfileProvisioner _profiles;
    private readonly LocationSnapshotFactory _snapshots;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public SetTravelModeCommandHandler(
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
        SetTravelModeCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userId = _currentUser.UserId;

        if (userId is null)
        {
            return Result.Failure<LocationProfileResponse>(LocationErrors.NotAuthenticated);
        }

        LocationSnapshot? destination = null;

        if (request.Destination is not null)
        {
            var built = await _snapshots.CreateAsync(request.Destination, cancellationToken);

            if (built.IsFailure)
            {
                return Result.Failure<LocationProfileResponse>(built.Error);
            }

            destination = built.Value;
        }

        var profile = await _profiles.GetOrCreateAsync(userId, cancellationToken);

        profile.SetTravelMode(request.Enabled, destination, _clock.UtcNow);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(ProfileMapper.ToResponse(profile));
    }
}
