using Diaspora.Application.Abstractions;
using Diaspora.Modules.Recommendations.Contracts.V1.Responses;

namespace Diaspora.Modules.Recommendations.Application.Profiles;

/// <summary>
/// What the platform believes about the caller.
/// <para>
/// The transparency surface of the module. Takes no identifier — a member can read their own profile
/// and nobody else's, which is a property of the query's shape rather than of a check inside it.
/// </para>
/// </summary>
[RequirePermission(RecommendationPermissions.ProfileRead)]
public sealed record GetPersonalizationProfileQuery : IQuery<PersonalizationProfileResponse>;
