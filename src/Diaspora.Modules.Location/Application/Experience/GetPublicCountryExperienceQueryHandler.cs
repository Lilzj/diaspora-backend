using Diaspora.Application.Abstractions;
using Diaspora.Modules.Location.Application.Mapping;
using Diaspora.Modules.Location.Contracts.V1.Responses;
using Diaspora.Modules.Location.Integration;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Location.Application.Experience;

/// <summary>Serves another member's public residence history.</summary>
public sealed class GetPublicCountryExperienceQueryHandler
    : IQueryHandler<GetPublicCountryExperienceQuery, CountryExperienceTimelineResponse>
{
    private readonly IResidencyDirectory _residencies;

    public GetPublicCountryExperienceQueryHandler(IResidencyDirectory residencies)
    {
        _residencies = residencies;
    }

    public async Task<Result<CountryExperienceTimelineResponse>> Handle(
        GetPublicCountryExperienceQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // An empty timeline rather than a 404 for a member with no declared history. Whether a
        // user id exists is Identity's to disclose, not Location's, and answering 404 here
        // would turn this endpoint into an account-enumeration oracle.
        var summaries = await _residencies.GetResidenciesAsync(
            UserId.From(request.UserId), cancellationToken);

        var experiences = summaries.Select(ExperienceMapper.ToResponse).ToList();

        return Result.Success(ExperienceMapper.ToTimeline(experiences));
    }
}
