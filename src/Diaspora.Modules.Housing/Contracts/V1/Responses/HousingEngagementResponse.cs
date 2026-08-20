namespace Diaspora.Modules.Housing.Contracts.V1.Responses;

/// <summary>How much attention a property has had.</summary>
public sealed record HousingEngagementResponse
{
    public required long ViewCount { get; init; }

    public required long UniqueViewCount { get; init; }

    public required long SaveCount { get; init; }

    public required long ViewingRequestCount { get; init; }

    public required long ReservationCount { get; init; }

    /// <summary>
    /// The share of viewers who asked to see it. Null until there is enough traffic for the
    /// fraction to mean anything.
    /// </summary>
    public double? ViewingConversionRate { get; init; }
}
