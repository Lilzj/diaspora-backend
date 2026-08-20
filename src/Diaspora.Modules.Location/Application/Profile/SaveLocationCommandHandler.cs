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

/// <summary>Saves a labelled place, defaulting to wherever the member currently is.</summary>
public sealed class SaveLocationCommandHandler
    : ICommandHandler<SaveLocationCommand, SavedLocationResponse>
{
    private readonly LocationProfileProvisioner _profiles;
    private readonly LocationSnapshotFactory _snapshots;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public SaveLocationCommandHandler(
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

    public async Task<Result<SavedLocationResponse>> Handle(
        SaveLocationCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userId = _currentUser.UserId;

        if (userId is null)
        {
            return Result.Failure<SavedLocationResponse>(LocationErrors.NotAuthenticated);
        }

        var profile = await _profiles.GetOrCreateAsync(userId, cancellationToken);

        LocationSnapshot? snapshot;

        if (request.Location is null)
        {
            // "Save active" — the button in the selector. Saving a location the server already
            // holds should not require the client to send it back.
            snapshot = profile.ActiveLocation;

            if (snapshot is null)
            {
                return Result.Failure<SavedLocationResponse>(LocationErrors.ProfileNotFound);
            }
        }
        else
        {
            var built = await _snapshots.CreateAsync(request.Location, cancellationToken);

            if (built.IsFailure)
            {
                return Result.Failure<SavedLocationResponse>(built.Error);
            }

            snapshot = built.Value;
        }

        var saved = profile.SaveLocation(request.Label, snapshot, _clock.UtcNow);

        if (saved.IsFailure)
        {
            return Result.Failure<SavedLocationResponse>(saved.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(ProfileMapper.ToResponse(
            saved.Value, profile.Preferences.CoordinatePrecision));
    }
}
