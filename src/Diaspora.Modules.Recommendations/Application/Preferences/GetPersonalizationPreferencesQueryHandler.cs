using Diaspora.Application.Abstractions;
using Diaspora.Modules.Recommendations.Application.Mapping;
using Diaspora.Modules.Recommendations.Application.Services;
using Diaspora.Modules.Recommendations.Contracts.V1.Responses;
using Diaspora.Modules.Recommendations.Domain.Preferences;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Recommendations.Application.Preferences;

/// <summary>
/// Reads the caller's preferences, or the defaults.
/// <para>
/// <b>Deliberately does not create a row.</b> Most members never open the settings screen, and
/// creating a preference record to answer a read would put one write per member per first visit on
/// a path that is otherwise free — and would make every row in the table stop meaning "somebody made
/// a decision".
/// </para>
/// </summary>
public sealed class GetPersonalizationPreferencesQueryHandler
    : IQueryHandler<GetPersonalizationPreferencesQuery, PersonalizationPreferencesResponse>
{
    private readonly IPersonalizationPreferenceRepository _preferences;
    private readonly RecommendationAuthorizationService _authorization;

    public GetPersonalizationPreferencesQueryHandler(
        IPersonalizationPreferenceRepository preferences,
        RecommendationAuthorizationService authorization)
    {
        _preferences = preferences;
        _authorization = authorization;
    }

    public async Task<Result<PersonalizationPreferencesResponse>> Handle(
        GetPersonalizationPreferencesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<PersonalizationPreferencesResponse>(caller.Error);
        }

        var preference = await _preferences.FindAsync(caller.Value, cancellationToken);

        // Null maps to the defaults rather than to a not-found. See the class summary.
        return Result.Success(RecommendationMapper.ToResponse(preference));
    }
}
