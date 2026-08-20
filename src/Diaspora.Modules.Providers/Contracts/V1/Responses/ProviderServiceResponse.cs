namespace Diaspora.Modules.Providers.Contracts.V1.Responses;

/// <summary>
/// One thing a provider offers.
/// <para>
/// The client's <c>services: [{ name, priceRange }]</c>, which its provider screen renders under
/// "Services &amp; Menu", plus everything two fields could not carry: a category of its own, a
/// duration, a delivery mode, a photograph, and a price that need not be a range.
/// </para>
/// <para>
/// <see cref="EffectivePricing"/> is what a client should render as the headline number. It is
/// the live offering's price when one is running and the service's own otherwise, so a client
/// never has to work out which promotion applies.
/// </para>
/// </summary>
public sealed record ProviderServiceResponse
{
    public required Guid ServiceId { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    /// <summary>A category of the service itself, when it differs from the provider's.</summary>
    public ProviderCategoryResponse? Category { get; init; }

    /// <summary>The service's own list price.</summary>
    public required PricingResponse Pricing { get; init; }

    /// <summary>The price to quote right now — the live offering's, or the list price.</summary>
    public required PricingResponse EffectivePricing { get; init; }

    /// <summary>The <c>ServiceDeliveryMode</c> name.</summary>
    public required string DeliveryMode { get; init; }

    public int? DurationMinutes { get; init; }

    /// <summary>Whether the provider is currently taking this on.</summary>
    public required bool IsAvailable { get; init; }

    public required bool IsFeatured { get; init; }

    /// <summary>A Media identifier. Never a URL.</summary>
    public Guid? ImageMediaId { get; init; }

    public required int Position { get; init; }

    public required IReadOnlyList<ServiceOfferingResponse> Offerings { get; init; }
}
