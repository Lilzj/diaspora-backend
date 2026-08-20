using Diaspora.Application.Abstractions;
using Diaspora.Modules.Location.Contracts.V1.Responses;

namespace Diaspora.Modules.Location.Application.Countries;

/// <summary>
/// One country in full, with the counts contributed by every vertical.
/// <para>
/// Backs the country page. The counts arrive from <c>ILocationResourceCounter</c>
/// contributions, which is how the page can report how many guides and providers a country has
/// without Location knowing what a guide is.
/// </para>
/// </summary>
[Authorize(Permission = LocationPermissions.ReadCatalog)]
public sealed record GetCountryQuery : IQuery<CountryDetailResponse>
{
    public required string CountryCode { get; init; }

    /// <summary>
    /// Whether to ask every vertical for its count. Off by default: the counts cost a fan-out
    /// with a timeout, and a client resolving a country to render its name in a chip does not
    /// need them.
    /// </summary>
    public bool IncludeCounts { get; init; }
}
