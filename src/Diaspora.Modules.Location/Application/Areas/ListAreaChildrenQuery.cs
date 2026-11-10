using Diaspora.Application.Abstractions;
using Diaspora.Modules.Location.Contracts.V1.Responses;
using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Location.Application.Areas;

/// <summary>
/// The direct children of a node — hierarchy browsing, one rung at a time.
/// <para>
/// The frontend has no screen for this today, and it is here anyway: a member picking a place
/// in a country the catalogue knows a thousand nodes in cannot be given a flat list, and
/// browsing down is the only interaction that scales.
/// </para>
/// </summary>
[RequirePermission(LocationPermissions.ReadCatalog)]
public sealed record ListAreaChildrenQuery : IQuery<PagedResult<AreaSummaryResponse>>
{
    public required Guid AreaId { get; init; }

    /// <summary>Restricts to one rung, when a caller wants only the cities beneath a region.</summary>
    public AdministrativeLevel? Level { get; init; }

    public PaginationRequest Pagination { get; init; } = new();
}
