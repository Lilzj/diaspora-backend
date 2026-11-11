using Diaspora.Application.Abstractions;
using Diaspora.Modules.Location.Contracts.V1.Responses;

namespace Diaspora.Modules.Location.Application.Experience;

/// <summary>
/// The caller's residence history, with Trust's verification state joined in.
/// <para>
/// Backs the trust centre's experience timeline and the profile header. The verified count it
/// carries is Trust's answer projected, never Location's own.
/// </para>
/// </summary>
[RequirePermission(LocationPermissions.ReadExperience)]
public sealed record GetMyCountryExperienceQuery : IQuery<CountryExperienceTimelineResponse>;
