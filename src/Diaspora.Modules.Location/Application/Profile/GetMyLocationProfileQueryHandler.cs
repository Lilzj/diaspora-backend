using Diaspora.Application.Abstractions;
using Diaspora.Modules.Location.Application.Mapping;
using Diaspora.Modules.Location.Application.Services;
using Diaspora.Modules.Location.Contracts.V1.Responses;
using Diaspora.Modules.Location.Domain.Errors;
using Diaspora.Modules.Location.Domain.Profiles;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Location.Application.Profile;

/// <summary>Serves the caller's geographic context.</summary>
public sealed class GetMyLocationProfileQueryHandler
    : IQueryHandler<GetMyLocationProfileQuery, LocationProfileResponse>
{
    private readonly LocationProfileProvisioner _profiles;
    private readonly ICurrentUser _currentUser;

    public GetMyLocationProfileQueryHandler(
        LocationProfileProvisioner profiles, ICurrentUser currentUser)
    {
        _profiles = profiles;
        _currentUser = currentUser;
    }

    public async Task<Result<LocationProfileResponse>> Handle(
        GetMyLocationProfileQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;

        if (userId is null)
        {
            return Result.Failure<LocationProfileResponse>(LocationErrors.NotAuthenticated);
        }

        // A member who has never set a location gets the defaults rather than a 404. The
        // client's store has defaults too, and an error here would make the first render of
        // every location screen an error state.
        var profile = await _profiles.FindAsync(userId, cancellationToken)
                      ?? LocationProfile.CreateFor(userId);

        return Result.Success(ProfileMapper.ToResponse(profile));
    }
}
