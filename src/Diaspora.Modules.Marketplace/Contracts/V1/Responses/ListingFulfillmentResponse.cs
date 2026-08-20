namespace Diaspora.Modules.Marketplace.Contracts.V1.Responses;

/// <summary>How the buyer gets the item.</summary>
public sealed record ListingFulfillmentResponse
{
    public required bool OffersPickup { get; init; }

    public required bool OffersDelivery { get; init; }

    public required bool OffersShipping { get; init; }

    /// <summary>Nothing physical changes hands — a service, or a digital good.</summary>
    public required bool IsDigital { get; init; }

    /// <summary>How far the seller will deliver. Null when they do not.</summary>
    public int? DeliveryRadiusKm { get; init; }

    /// <summary>"Weekends only", "collection from the shop", "postage at cost".</summary>
    public string? Notes { get; init; }
}
