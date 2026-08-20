using Diaspora.Application.Abstractions;
using Diaspora.Modules.Recommendations.Contracts.V1.Responses;

namespace Diaspora.Modules.Recommendations.Application.Preferences;

/// <summary>What the caller has asked for. Returns defaults when they have stated nothing.</summary>
[Authorize(Permission = RecommendationPermissions.PreferencesRead)]
public sealed record GetPersonalizationPreferencesQuery
    : IQuery<PersonalizationPreferencesResponse>;
