namespace Diaspora.Modules.Marketplace.Contracts.V1.Requests;

/// <summary>
/// A full replacement of everything a buyer reads.
/// <para>
/// <b>A replacement rather than a patch, and the trade is deliberate.</b> A patch protocol needs a
/// way to say "this field is absent" as distinct from "set this field to null", which in JSON means
/// either a wrapper type on every property or a separate field mask — and both put the burden on
/// every client for a screen that in practice always sends the whole form back.
/// </para>
/// <para>
/// <see cref="BaseRevision"/> is what makes that safe. The client sends back the revision it
/// loaded, and a save based on a stale one is refused with a conflict it can merge from rather
/// than silently overwriting somebody else's edit.
/// </para>
/// </summary>
public sealed record UpdateListingRequest
{
    public required string Title { get; init; }

    public required string Description { get; init; }

    public string? Summary { get; init; }

    public string? Condition { get; init; }

    public decimal? Price { get; init; }

    public string? Currency { get; init; }

    public bool IsNegotiable { get; init; }

    public decimal? MinimumOffer { get; init; }

    public required string CountryCode { get; init; }

    public string? RegionName { get; init; }

    public string? CityName { get; init; }

    public string? Neighbourhood { get; init; }

    public Guid? AreaId { get; init; }

    public bool OffersPickup { get; init; }

    public bool OffersDelivery { get; init; }

    public bool OffersShipping { get; init; }

    public bool IsDigital { get; init; }

    public int? DeliveryRadiusKm { get; init; }

    public string? FulfillmentNotes { get; init; }

    public string? Brand { get; init; }

    public string? Model { get; init; }

    public string? Sku { get; init; }

    public int? Year { get; init; }

    public string? ContactPreference { get; init; }

    public string? Visibility { get; init; }

    /// <summary>The revision the editor loaded. Null opts out of the conflict check.</summary>
    public int? BaseRevision { get; init; }
}
