namespace Diaspora.Modules.Providers.Contracts.V1.Responses;

/// <summary>
/// A time-bounded packaging of a service: a promotion, a package, a seasonal menu.
/// <para>
/// Returned alongside the service's own price rather than replacing it, so a client can render
/// "£45 (was £60)" — which needs both numbers and is the whole reason offerings are separate
/// from prices.
/// </para>
/// </summary>
public sealed record ServiceOfferingResponse
{
    public required Guid OfferingId { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public required PricingResponse Pricing { get; init; }

    /// <summary>Null means it is already running.</summary>
    public DateTimeOffset? StartsAt { get; init; }

    /// <summary>Null means until further notice.</summary>
    public DateTimeOffset? EndsAt { get; init; }

    /// <summary>Whether this offering's price is the one a member should be quoted right now.</summary>
    public required bool IsActive { get; init; }
}
