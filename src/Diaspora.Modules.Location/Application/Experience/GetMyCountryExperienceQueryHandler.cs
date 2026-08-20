using Diaspora.Application.Abstractions;
using Diaspora.Modules.Location.Application.Mapping;
using Diaspora.Modules.Location.Contracts.V1.Responses;
using Diaspora.Modules.Location.Domain.Errors;
using Diaspora.Modules.Location.Integration;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Location.Application.Experience;

/// <summary>Serves the caller's residence timeline.</summary>
public sealed class GetMyCountryExperienceQueryHandler
    : IQueryHandler<GetMyCountryExperienceQuery, CountryExperienceTimelineResponse>
{
    private readonly IResidencyDirectory _residencies;
    private readonly ICurrentUser _currentUser;

    public GetMyCountryExperienceQueryHandler(
        IResidencyDirectory residencies, ICurrentUser currentUser)
    {
        _residencies = residencies;
        _currentUser = currentUser;
    }

    public async Task<Result<CountryExperienceTimelineResponse>> Handle(
        GetMyCountryExperienceQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;

        if (userId is null)
        {
            return Result.Failure<CountryExperienceTimelineResponse>(
                LocationErrors.NotAuthenticated);
        }

        // Through the same directory other modules use, rather than through the repositories
        // directly. The claim-plus-proof join belongs in one place, and this endpoint has no
        // more right to reimplement it than a consumer does.
        var summaries = await _residencies.GetResidenciesAsync(userId, cancellationToken);

        var experiences = summaries.Select(ExperienceMapper.ToResponse).ToList();

        return Result.Success(ExperienceMapper.ToTimeline(experiences));
    }
}
