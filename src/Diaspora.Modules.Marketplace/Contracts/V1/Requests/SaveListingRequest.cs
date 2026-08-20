namespace Diaspora.Modules.Marketplace.Contracts.V1.Requests;

/// <summary>Adds a listing to the member's watchlist.</summary>
public sealed record SaveListingRequest
{
    /// <summary>The member's own private note — "check the charger", "ask about delivery".</summary>
    public string? Note { get; init; }

    /// <summary>
    /// Whether to be told when the price moves. The one watchlist feature that costs the platform
    /// anything and the one buyers ask for most.
    /// </summary>
    public bool NotifyOnPriceChange { get; init; }
}
