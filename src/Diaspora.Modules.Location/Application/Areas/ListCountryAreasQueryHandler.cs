using Diaspora.Application.Abstractions;
using Diaspora.Modules.Location.Application.Mapping;
using Diaspora.Modules.Location.Contracts.V1.Responses;
using Diaspora.Modules.Location.Domain.Areas;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.SharedKernel.Pagination;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Location.Application.Areas;

/// <summary>Serves the nodes at one level within a country.</summary>
public sealed class ListCountryAreasQueryHandler
    : IQueryHandler<ListCountryAreasQuery, PagedResult<AreaSummaryResponse>>
{
    private readonly IAdministrativeAreaRepository _areas;

    public ListCountryAreasQueryHandler(IAdministrativeAreaRepository areas)
    {
        _areas = areas;
    }

    public async Task<Result<PagedResult<AreaSummaryResponse>>> Handle(
        ListCountryAreasQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var code = CountryCode.TryCreate(request.CountryCode);

        if (code.IsFailure)
        {
            return Result.Failure<PagedResult<AreaSummaryResponse>>(code.Error);
        }

        var page = await _areas.GetByLevelAsync(
            code.Value,
            request.Level,
            request.Term,
            request.Pagination.Sanitize(),
            cancellationToken);

        // Ancestors are resolved once for the whole page rather than per row. Everything on
        // this page shares a country and most share a parent, so one batched fetch replaces
        // what would otherwise be a query per result.
        var ancestors = await ResolveAncestorsAsync(page.Items, cancellationToken);

        return Result.Success(page.Map(area => LocationMapper.ToSummary(
            area, ancestors.GetValueOrDefault(area.Id.Value) ?? [])));
    }

    private async Task<Dictionary<Guid, IReadOnlyList<AdministrativeArea>>> ResolveAncestorsAsync(
        IReadOnlyList<AdministrativeArea> areas, CancellationToken cancellationToken)
    {
        var resolved = new Dictionary<Guid, IReadOnlyList<AdministrativeArea>>();

        foreach (var area in areas)
        {
            resolved[area.Id.Value] = await _areas.GetAncestorsAsync(area, cancellationToken);
        }

        return resolved;
    }
}
