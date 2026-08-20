using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Contracts.V1.Responses;

namespace Diaspora.Modules.Housing.Application.Catalog;

/// <summary>The amenity catalogue, grouped as a listing form renders it.</summary>
public sealed record ListHousingAmenitiesQuery
    : IQuery<IReadOnlyList<HousingAmenityCatalogResponse>>
{
    public bool IncludeInactive { get; init; }
}
