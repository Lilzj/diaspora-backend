using Diaspora.Application.Abstractions;
using Diaspora.Modules.Location.Contracts.V1.Responses;

namespace Diaspora.Modules.Location.Application.Areas;

/// <summary>
/// The neighbourhood hub screen's payload.
/// <para>
/// Resolves by slug or by identifier. The slug half is not a convenience: the frontend's
/// <c>NeighborhoodCard</c> already builds <c>/neighborhood/peckham</c> links and its hub
/// screen already fetches them, so a backend that only accepted identifiers would break
/// navigation that works today.
/// </para>
/// </summary>
[RequirePermission(LocationPermissions.ReadCatalog)]
public sealed record GetNeighborhoodHubQuery : IQuery<NeighborhoodHubResponse>
{
    /// <summary>A slug (<c>peckham</c>) or a GUID. Both resolve.</summary>
    public required string IdOrSlug { get; init; }

    /// <summary>Disambiguates a slug that exists in more than one country.</summary>
    public string? CountryCode { get; init; }
}
