using Diaspora.Application.Abstractions;
using Diaspora.Modules.Location.Application.Mapping;
using Diaspora.Modules.Location.Contracts.V1.Responses;
using Diaspora.Modules.Location.Domain.Areas;
using Diaspora.Modules.Location.Domain.Errors;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Location.Application.Areas;

/// <summary>Serves the direct children of a node.</summary>
public sealed class ListAreaChildrenQueryHandler
    : IQueryHandler<ListAreaChildrenQuery, PagedResult<AreaSummaryResponse>>
{
    private readonly IAdministrativeAreaRepository _areas;

    public ListAreaChildrenQueryHandler(IAdministrativeAreaRepository areas)
    {
        _areas = areas;
    }

    public async Task<Result<PagedResult<AreaSummaryResponse>>> Handle(
        ListAreaChildrenQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var parent = await _areas.GetByIdAsync(AreaId.From(request.AreaId), cancellationToken);

        if (parent is null)
        {
            return Result.Failure<PagedResult<AreaSummaryResponse>>(LocationErrors.AreaNotFound);
        }

        var page = await _areas.GetChildrenAsync(
            parent.CountryId,
            parent.Id,
            request.Level,
            request.Pagination.Sanitize(),
            cancellationToken);

        // The parent's own breadcrumb serves every child: a child's ancestors are the parent's
        // ancestors plus the parent. One fetch for the page rather than one per row.
        var parentAncestors = await _areas.GetAncestorsAsync(parent, cancellationToken);
        var childAncestors = new List<AdministrativeArea>(parentAncestors) { parent };

        return Result.Success(page.Map(child => LocationMapper.ToSummary(child, childAncestors)));
    }
}
