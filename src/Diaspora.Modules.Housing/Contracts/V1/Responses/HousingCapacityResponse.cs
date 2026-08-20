namespace Diaspora.Modules.Housing.Contracts.V1.Responses;

/// <summary>How much property there is, and how many people it is for.</summary>
public sealed record HousingCapacityResponse
{
    /// <summary>Zero for a studio, which is a real answer rather than a missing one.</summary>
    public required int Bedrooms { get; init; }

    /// <summary>Decimal, so a half-bathroom survives.</summary>
    public required decimal Bathrooms { get; init; }

    public int? MaximumOccupants { get; init; }

    /// <summary>Metric only. A client converts; storing two units would store two numbers that disagree.</summary>
    public int? FloorAreaSquareMetres { get; init; }

    public required bool IsStudio { get; init; }
}
