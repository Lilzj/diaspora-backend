namespace Diaspora.Modules.Providers.Contracts.V1.Requests;

/// <summary>Adds a time-bounded packaging of a service: a promotion, a package, a seasonal menu.</summary>
public sealed record ServiceOfferingRequest
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    public required PricingRequest Pricing { get; init; }

    /// <summary>Null means it starts immediately.</summary>
    public DateTimeOffset? StartsAt { get; init; }

    /// <summary>Null means until further notice.</summary>
    public DateTimeOffset? EndsAt { get; init; }
}
