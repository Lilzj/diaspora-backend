using Diaspora.Application.Abstractions;
using Diaspora.Modules.Location.Application.Mapping;
using Diaspora.Modules.Location.Contracts.V1.Responses;
using Diaspora.Modules.Location.Domain.Areas;
using Diaspora.Modules.Location.Domain.Countries;
using Diaspora.Modules.Location.Domain.Errors;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Location.Application.Areas;

/// <summary>Serves one hierarchy node.</summary>
public sealed class GetAreaQueryHandler : IQueryHandler<GetAreaQuery, AreaDetailResponse>
{
    private readonly IAdministrativeAreaRepository _areas;
    private readonly ICountryRepository _countries;

    public GetAreaQueryHandler(
        IAdministrativeAreaRepository areas, ICountryRepository countries)
    {
        _areas = areas;
        _countries = countries;
    }

    public async Task<Result<AreaDetailResponse>> Handle(
        GetAreaQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var area = await _areas.GetByIdAsync(AreaId.From(request.AreaId), cancellationToken);

        if (area is null)
        {
            return Result.Failure<AreaDetailResponse>(LocationErrors.AreaNotFound);
        }

        var country = await _countries.GetByCodeAsync(area.CountryCode, cancellationToken);

        if (country is null)
        {
            // A node whose country is missing is a broken catalogue, not a missing area. The
            // distinct error keeps that visible instead of reporting the node as absent.
            return Result.Failure<AreaDetailResponse>(
                LocationErrors.CountryNotFound(area.CountryCode.Value));
        }

        var ancestors = await _areas.GetAncestorsAsync(area, cancellationToken);

        return Result.Success(LocationMapper.ToDetail(area, ancestors, country));
    }
}
