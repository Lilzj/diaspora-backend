namespace Diaspora.Modules.Providers.Contracts.V1.Requests;

/// <summary>Adds or edits one thing a provider offers.</summary>
public sealed record ProviderServiceRequest
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    /// <summary>
    /// A category for the service itself, when it differs from the provider's. What lets a
    /// member searching for catering find a restaurant that caters.
    /// </summary>
    public string? Category { get; init; }

    public PricingRequest? Pricing { get; init; }

    /// <summary>The <c>ServiceDeliveryMode</c> name. Defaults to the provider's.</summary>
    public string? DeliveryMode { get; init; }

    /// <summary>Typical duration. What a future booking module will schedule against.</summary>
    public int? DurationMinutes { get; init; }

    /// <summary>A Media identifier for a photograph of the dish, the style, the room.</summary>
    public Guid? ImageMediaId { get; init; }

    public bool IsAvailable { get; init; } = true;

    public bool IsFeatured { get; init; }
}
