using Diaspora.Application.Abstractions;
using Diaspora.Modules.Location.Contracts.V1.Responses;

namespace Diaspora.Modules.Location.Application.Experience;

/// <summary>
/// Another member's country experience, as a public projection.
/// <para>
/// Backs the contributor profile and the country page's expert list. It returns the same DTO
/// as the private query with three fields blanked — the row identifier, and the two
/// self-assessed familiarity levels, which are a member's own note to themselves rather than
/// something to display beside their name.
/// </para>
/// </summary>
[RequirePermission(LocationPermissions.ReadPublic)]
public sealed record GetPublicCountryExperienceQuery : IQuery<CountryExperienceTimelineResponse>
{
    public required Guid UserId { get; init; }
}
