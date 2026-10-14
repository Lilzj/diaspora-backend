using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Application.Mapping;
using Diaspora.Modules.Housing.Application.Services;
using Diaspora.Modules.Housing.Contracts.V1.Responses;
using Diaspora.Modules.Housing.Domain.Neighborhoods;
using Diaspora.Modules.Housing.Domain.Policies;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Housing.Application.Neighborhoods;

/// <summary>
/// The neighbourhood directory.
/// <para>
/// The reusable discovery surface the brief asks for. Narrowing by area goes through B4 so that a
/// caller naming a city gets every neighbourhood beneath it - the materialised path is stored on
/// the profile precisely so that is an indexed prefix match rather than a hierarchy walk.
/// </para>
/// </summary>
public sealed class SearchNeighborhoodsQueryHandler
    : IQueryHandler<SearchNeighborhoodsQuery, IReadOnlyList<NeighborhoodSummaryResponse>>
{
    private readonly INeighborhoodProfileRepository _profiles;
    private readonly HousingGeographicScopeAdapter _geography;
    private readonly HousingAuthorizationService _authorization;

    public SearchNeighborhoodsQueryHandler(
        INeighborhoodProfileRepository profiles,
        HousingGeographicScopeAdapter geography,
        HousingAuthorizationService authorization)
    {
        _profiles = profiles;
        _geography = geography;
        _authorization = authorization;
    }

    public async Task<Result<IReadOnlyList<NeighborhoodSummaryResponse>>> Handle(
        SearchNeighborhoodsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var scope = await _geography.ResolveForNeighborhoodsAsync(
            request.City,
            request.CountryCode,
            request.AreaId,
            _authorization.CallerId,
            cancellationToken);

        if (scope.IsFailure)
        {
            return Result.Failure<IReadOnlyList<NeighborhoodSummaryResponse>>(scope.Error);
        }

        var filter = new NeighborhoodQueryFilter
        {
            CountryCode = scope.Value.CountryCode?.Value ?? request.CountryCode,
            AreaPathPrefix = scope.Value.Path?.Value,
            CityName = scope.Value.CityName ?? request.City,
            Text = request.Query,
            WithListingsOnly = request.WithListingsOnly,
            OrderByPopularity = request.ByPopularity,
            Limit = request.Limit ?? HousingLimits.DefaultPageSize
        };

        var page = await _profiles.QueryAsync(
            filter, request.Cursor, filter.EffectiveLimit, cancellationToken);

        return Result.Success<IReadOnlyList<NeighborhoodSummaryResponse>>(
            [.. page.Items.Select(NeighborhoodMapper.ToSummaryResponse)]);
    }
}
