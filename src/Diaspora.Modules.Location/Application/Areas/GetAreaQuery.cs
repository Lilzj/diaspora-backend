using Diaspora.Application.Abstractions;
using Diaspora.Modules.Location.Contracts.V1.Responses;

namespace Diaspora.Modules.Location.Application.Areas;

/// <summary>One hierarchy node with its breadcrumb.</summary>
[Authorize(Permission = LocationPermissions.ReadCatalog)]
public sealed record GetAreaQuery : IQuery<AreaDetailResponse>
{
    public required Guid AreaId { get; init; }
}
