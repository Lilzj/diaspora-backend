using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Location.Application.Services;
using Diaspora.Modules.Location.Domain.Errors;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Location.Application.Profile;

/// <summary>Removes a labelled place from the caller's profile.</summary>
public sealed class RemoveSavedLocationCommandHandler
    : ICommandHandler<RemoveSavedLocationCommand>
{
    private readonly LocationProfileProvisioner _profiles;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public RemoveSavedLocationCommandHandler(
        LocationProfileProvisioner profiles, ICurrentUser currentUser, IUnitOfWork unitOfWork)
    {
        _profiles = profiles;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(
        RemoveSavedLocationCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userId = _currentUser.UserId;

        if (userId is null)
        {
            return Result.Failure(LocationErrors.NotAuthenticated);
        }

        var profile = await _profiles.FindAsync(userId, cancellationToken);

        if (profile is null)
        {
            return Result.Failure(LocationErrors.SavedLocationNotFound);
        }

        var removed = profile.RemoveSavedLocation(request.Label);

        if (removed.IsFailure)
        {
            return removed;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
