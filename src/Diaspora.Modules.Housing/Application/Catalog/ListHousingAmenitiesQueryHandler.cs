using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Application.Mapping;
using Diaspora.Modules.Housing.Contracts.V1.Responses;
using Diaspora.Modules.Housing.Domain.Catalog;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Housing.Application.Catalog;

/// <summary>Reads the amenity catalogue.</summary>
public sealed class ListHousingAmenitiesQueryHandler
    : IQueryHandler<ListHousingAmenitiesQuery, IReadOnlyList<HousingAmenityCatalogResponse>>
{
    private readonly IHousingAmenityRepository _amenities;
    private readonly Services.HousingAuthorizationService _authorization;

    public ListHousingAmenitiesQueryHandler(
        IHousingAmenityRepository amenities,
        Services.HousingAuthorizationService authorization)
    {
        _amenities = amenities;
        _authorization = authorization;
    }

    public async Task<Result<IReadOnlyList<HousingAmenityCatalogResponse>>> Handle(
        ListHousingAmenitiesQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var includeInactive = request.IncludeInactive && _authorization.IsStaff;

        var amenities = await _amenities.GetAllAsync(includeInactive, cancellationToken);

        return Result.Success<IReadOnlyList<HousingAmenityCatalogResponse>>(
            [.. amenities.Select(HousingCatalogMapper.ToResponse)]);
    }
}
