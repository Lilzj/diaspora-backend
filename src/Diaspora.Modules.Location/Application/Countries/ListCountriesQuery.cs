using Diaspora.Application.Abstractions;
using Diaspora.Modules.Location.Contracts.V1.Responses;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Location.Application.Countries;

/// <summary>
/// The country catalogue, filtered and paged.
/// <para>
/// Supersedes the 195-country array bundled into the mobile app. Paged because it is a
/// platform list endpoint and every one of them is, but the page size is generous enough that
/// a client wanting the whole thing for an offline picker gets it in four requests rather
/// than twenty.
/// </para>
/// </summary>
[Authorize(Permission = LocationPermissions.ReadCatalog)]
public sealed record ListCountriesQuery : IQuery<PagedResult<CountrySummaryResponse>>
{
    public PaginationRequest Pagination { get; init; } = new();

    /// <summary>Matches a country name or its ISO code.</summary>
    public string? Term { get; init; }

    /// <summary>Restricts to countries the platform actually operates in.</summary>
    public bool? SupportedOnly { get; init; }

    /// <summary>Groups the picker by continent, when a client wants that.</summary>
    public string? Continent { get; init; }
}
