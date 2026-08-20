namespace Diaspora.Modules.Marketplace.Contracts.V1.Responses;

/// <summary>What the thing is, as a manufacturer would say it. Every field optional.</summary>
public sealed record ProductIdentityResponse
{
    public string? Brand { get; init; }

    public string? Model { get; init; }

    /// <summary>
    /// The seller's own stock reference. Rendered on the seller's own view only — a shop's
    /// internal code means nothing to a buyer and occasionally means something to a competitor.
    /// </summary>
    public string? Sku { get; init; }

    public int? Year { get; init; }
}
