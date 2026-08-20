namespace Diaspora.Modules.Marketplace.Contracts.V1.Responses;

/// <summary>
/// Where the item is.
/// <para>
/// No coordinates, and that is a privacy decision rather than an omission. A listing is at
/// somebody's home; publishing a point would publish where they live to every stranger who opens
/// the page. Distance is computed by Location against the buyer's own scope, and the place is
/// rendered as far as the seller chose to be specific.
/// </para>
/// </summary>
public sealed record ListingLocationResponse
{
    /// <summary>ISO 3166-1 alpha-2, upper case.</summary>
    public required string CountryCode { get; init; }

    public string? RegionName { get; init; }

    public string? CityName { get; init; }

    /// <summary>The seller's own words for the area. Never a Location entity.</summary>
    public string? Neighbourhood { get; init; }

    /// <summary>Location's area identifier, when the seller picked one.</summary>
    public Guid? AreaId { get; init; }

    /// <summary>The one-line place a card renders, as specific as the seller made it.</summary>
    public required string DisplayName { get; init; }
}
