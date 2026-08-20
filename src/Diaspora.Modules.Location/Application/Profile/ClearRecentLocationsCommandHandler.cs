using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Location.Application.Services;
using Diaspora.Modules.Location.Domain.Errors;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Location.Application.Profile;

/// <summary>Clears the caller's recents.</summary>
public sealed class ClearRecentLocationsCommandHandler
    : ICommandHandler<ClearRecentLocationsCommand>
{
    private readonly LocationProfileProvisioner _profiles;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public ClearRecentLocationsCommandHandler(
        LocationProfileProvisioner profiles, ICurrentUser currentUser, IUnitOfWork unitOfWork)
    {
        _profiles = profiles;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(
        ClearRecentLocationsCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;

        if (userId is null)
        {
            return Result.Failure(LocationErrors.NotAuthenticated);
        }

        var profile = await _profiles.FindAsync(userId, cancellationToken);

        // Nothing to clear is success, not failure. A member tapping "Clear all" on an empty
        // list has got what they asked for.
        if (profile is null)
        {
            return Result.Success();
        }

        profile.ClearRecentLocations();

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
