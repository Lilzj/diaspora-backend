using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Contracts.V1.Responses;

namespace Diaspora.Modules.Housing.Application.Neighborhoods;

/// <summary>
/// One neighbourhood profile, by Location area or by slug.
/// <para>
/// Unauthenticated. Somebody deciding whether to move to a city should be able to read what living
/// in its neighbourhoods is like before they join anything - which is most of the reason the module
/// exists.
/// </para>
/// </summary>
public sealed record GetNeighborhoodProfileQuery : IQuery<NeighborhoodProfileResponse>
{
    /// <summary>A Location area identifier, or a neighbourhood slug.</summary>
    public required string IdOrSlug { get; init; }

    /// <summary>Disambiguates a slug. There is a Springfield in twelve states.</summary>
    public string? CountryCode { get; init; }
}
