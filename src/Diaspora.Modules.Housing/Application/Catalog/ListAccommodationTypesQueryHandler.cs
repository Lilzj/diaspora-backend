using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Application.Mapping;
using Diaspora.Modules.Housing.Contracts.V1.Responses;
using Diaspora.Modules.Housing.Domain.Catalog;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Housing.Application.Catalog;

/// <summary>Reads the accommodation-type catalogue.</summary>
public sealed class ListAccommodationTypesQueryHandler
    : IQueryHandler<ListAccommodationTypesQuery, IReadOnlyList<AccommodationTypeResponse>>
{
    private readonly IAccommodationTypeRepository _types;
    private readonly Services.HousingAuthorizationService _authorization;

    public ListAccommodationTypesQueryHandler(
        IAccommodationTypeRepository types,
        Services.HousingAuthorizationService authorization)
    {
        _types = types;
        _authorization = authorization;
    }

    public async Task<Result<IReadOnlyList<AccommodationTypeResponse>>> Handle(
        ListAccommodationTypesQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Retired entries are for a management screen. Asking for them without staff access is
        // narrowed rather than refused: a stale client should get a usable picker.
        var includeInactive = request.IncludeInactive && _authorization.IsStaff;

        var types = await _types.GetAllAsync(includeInactive, cancellationToken);

        return Result.Success<IReadOnlyList<AccommodationTypeResponse>>(
            [.. types.Select(HousingCatalogMapper.ToResponse)]);
    }
}
