namespace Diaspora.Modules.Housing.Contracts.V1.Responses;

/// <summary>One day of a property measured activity. What a sparkline renders.</summary>
public sealed record HousingDailyMetricResponse
{
    public required DateOnly Day { get; init; }

    public required long Views { get; init; }

    public required long UniqueViews { get; init; }

    public required long Saves { get; init; }

    public required long ViewingRequests { get; init; }

    public required long Reservations { get; init; }
}
