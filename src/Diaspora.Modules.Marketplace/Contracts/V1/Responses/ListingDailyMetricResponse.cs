namespace Diaspora.Modules.Marketplace.Contracts.V1.Responses;

/// <summary>One day of a listing's measured life.</summary>
public sealed record ListingDailyMetricResponse
{
    public required DateOnly Day { get; init; }

    public required int Views { get; init; }

    public required int UniqueViews { get; init; }

    public required int Saves { get; init; }

    public required int Offers { get; init; }

    public required int Reservations { get; init; }

    public required int Sales { get; init; }

    public required int Impressions { get; init; }
}
