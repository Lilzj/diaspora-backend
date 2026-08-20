using Diaspora.Application.Abstractions;
using Diaspora.Modules.Recommendations.Application.Mapping;
using Diaspora.Modules.Recommendations.Application.Services;
using Diaspora.Modules.Recommendations.Contracts.V1.Responses;
using Diaspora.Modules.Recommendations.Domain.Profiles;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Recommendations.Application.Profiles;

/// <summary>
/// Reads the caller's profile.
/// <para>
/// <b>Returns an empty profile rather than a 404 when the member has none.</b> A settings screen
/// asking "what do you know about me" and receiving a not-found would have to render an error for
/// what is actually a complete and reassuring answer: nothing yet.
/// </para>
/// </summary>
public sealed class GetPersonalizationProfileQueryHandler
    : IQueryHandler<GetPersonalizationProfileQuery, PersonalizationProfileResponse>
{
    private readonly IPersonalizationProfileRepository _profiles;
    private readonly RecommendationAuthorizationService _authorization;

    public GetPersonalizationProfileQueryHandler(
        IPersonalizationProfileRepository profiles,
        RecommendationAuthorizationService authorization)
    {
        _profiles = profiles;
        _authorization = authorization;
    }

    public async Task<Result<PersonalizationProfileResponse>> Handle(
        GetPersonalizationProfileQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<PersonalizationProfileResponse>(caller.Error);
        }

        var profile = await _profiles.FindAsync(caller.Value, cancellationToken);

        // Nothing yet is an answer, not an error. See the class summary.
        if (profile is null)
        {
            return Result.Success(new PersonalizationProfileResponse
            {
                Completeness = 0,
                IsColdStart = true,
                SignalCount = 0,
                IsVerified = false,
                HasCompletedOnboarding = false,
                Interests = [],
                Locations = [],
                Activity = []
            });
        }

        return Result.Success(RecommendationMapper.ToResponse(profile));
    }
}
