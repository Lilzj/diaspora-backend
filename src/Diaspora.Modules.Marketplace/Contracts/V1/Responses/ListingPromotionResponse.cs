namespace Diaspora.Modules.Marketplace.Contracts.V1.Responses;

/// <summary>A promotion on a listing.</summary>
public sealed record ListingPromotionResponse
{
    public required Guid PromotionId { get; init; }

    public required Guid ListingId { get; init; }

    /// <summary><c>Featured</c>, <c>Bump</c>, <c>Spotlight</c> or <c>CategoryHighlight</c>.</summary>
    public required string Kind { get; init; }

    public required DateTimeOffset StartsAt { get; init; }

    public required DateTimeOffset EndsAt { get; init; }

    /// <summary>Whether it is running right now.</summary>
    public required bool IsActive { get; init; }

    public required bool IsCancelled { get; init; }

    /// <summary>A receipt identifier from whatever granted it. Unused today.</summary>
    public string? ExternalReference { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}
