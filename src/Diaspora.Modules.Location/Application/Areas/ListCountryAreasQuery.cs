using Diaspora.Application.Abstractions;
using Diaspora.Modules.Location.Contracts.V1.Responses;
using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Location.Application.Areas;

/// <summary>
/// Every node at one level within a country — the cities in the UK, the regions in France.
/// <para>
/// One query behind three routes (<c>/regions</c>, <c>/cities</c>, <c>/neighborhoods</c>)
/// because they differ only in the level they ask for. Three handlers would be three places
/// to fix the same paging bug.
/// </para>
/// </summary>
[RequirePermission(LocationPermissions.ReadCatalog)]
public sealed record ListCountryAreasQuery : IQuery<PagedResult<AreaSummaryResponse>>
{
    public required string CountryCode { get; init; }

    public required AdministrativeLevel Level { get; init; }

    /// <summary>Narrows by name, for a searchable picker.</summary>
    public string? Term { get; init; }

    public PaginationRequest Pagination { get; init; } = new();
}
