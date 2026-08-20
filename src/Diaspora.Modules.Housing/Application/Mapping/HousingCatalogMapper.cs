using Diaspora.Modules.Housing.Contracts.V1.Responses;
using Diaspora.Modules.Housing.Domain.Catalog;

namespace Diaspora.Modules.Housing.Application.Mapping;

/// <summary>Turns catalogue entries into wire responses.</summary>
public static class HousingCatalogMapper
{
    public static AccommodationTypeResponse ToResponse(AccommodationType type)
    {
        ArgumentNullException.ThrowIfNull(type);

        return new AccommodationTypeResponse
        {
            Id = type.Id.Value,
            Key = type.Key,
            Name = type.Name,
            Description = type.Description,
            IconKey = type.IconKey,
            IsShared = type.IsShared,
            IsTemporary = type.IsTemporary,
            Order = type.Order,
            IsActive = type.IsActive
        };
    }

    public static HousingAmenityCatalogResponse ToResponse(HousingAmenity amenity)
    {
        ArgumentNullException.ThrowIfNull(amenity);

        return new HousingAmenityCatalogResponse
        {
            Id = amenity.Id.Value,
            Key = amenity.Key,
            Name = amenity.Name,
            Group = amenity.Group,
            IconKey = amenity.IconKey,
            IsAccessibilityFeature = amenity.IsAccessibilityFeature,
            IsFilterable = amenity.IsFilterable,
            Order = amenity.Order,
            IsActive = amenity.IsActive
        };
    }
}
