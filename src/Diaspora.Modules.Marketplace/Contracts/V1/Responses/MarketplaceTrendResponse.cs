namespace Diaspora.Modules.Marketplace.Contracts.V1.Responses;

/// <summary>One row of a trend — a category or a country, and what happened in it.</summary>
public sealed record MarketplaceTrendResponse
{
    /// <summary>The category key or the country code, depending on which trend was asked for.</summary>
    public required string Key { get; init; }

    /// <summary>The display name, resolved for a category. The code itself for a country.</summary>
    public string? Label { get; init; }

    public required int Listings { get; init; }

    public required long Views { get; init; }

    public required long Offers { get; init; }

    public required long Reservations { get; init; }

    public required long Sales { get; init; }

    /// <summary>Sales over views. Null when nobody looked.</summary>
    public double? ConversionRate { get; init; }
}
