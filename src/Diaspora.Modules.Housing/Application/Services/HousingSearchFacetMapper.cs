using Diaspora.Modules.Housing.Contracts.V1.Responses;
using Diaspora.Modules.Housing.Domain.Catalog;
using Diaspora.Modules.Housing.Extensibility.Search;

namespace Diaspora.Modules.Housing.Application.Services;

/// <summary>
/// Turns an engine's facet counts into the wire response, dropping anything the catalogue does not
/// offer as a filter.
/// <para>
/// <b>The filtering is the reason this is a service rather than a static mapper.</b> An engine
/// counts whatever it finds, including amenities a deployment has marked unfilterable or retired;
/// rendering those would offer a member a checkbox that narrows to nothing, or one the catalogue
/// deliberately hid. Deciding requires the catalogue, so the decision lives where the catalogue can
/// be read.
/// </para>
/// </summary>
public sealed class HousingSearchFacetMapper
{
    private readonly IHousingAmenityRepository _amenities;

    public HousingSearchFacetMapper(IHousingAmenityRepository amenities) =>
        _amenities = amenities;

    public async Task<HousingSearchFacetsResponse> ToResponseAsync(
        HousingSearchFacets facets, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(facets);

        var filterable = (await _amenities.GetAllAsync(
                includeInactive: false, cancellationToken))
            .Where(amenity => amenity.IsFilterable)
            .Select(amenity => amenity.Key)
            .ToHashSet(StringComparer.Ordinal);

        return new HousingSearchFacetsResponse
        {
            AccommodationTypes = facets.AccommodationTypes,
            Amenities = facets.Amenities
                .Where(entry => filterable.Contains(entry.Key))
                .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal),
            Bedrooms = facets.Bedrooms,
            Neighborhoods = facets.Neighborhoods,
            LeaseTypes = facets.LeaseTypes,
            FurnishedStatuses = facets.FurnishedStatuses,
            PriceRanges =
            [
                .. facets.PriceRanges.Values.Select(range =>
                    new HousingPriceRangeFacetResponse
                    {
                        CurrencyCode = range.CurrencyCode,
                        Period = range.Period.ToString(),
                        Minimum = range.Minimum,
                        Maximum = range.Maximum,
                        Count = range.Count
                    })
            ],
            VerifiedCount = facets.VerifiedCount,
            AccessibleCount = facets.AccessibleCount,
            ShortStayCount = facets.ShortStayCount,
            RoommateWantedCount = facets.RoommateWantedCount
        };
    }
}
