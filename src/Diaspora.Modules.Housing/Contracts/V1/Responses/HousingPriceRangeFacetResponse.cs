namespace Diaspora.Modules.Housing.Contracts.V1.Responses;

/// <summary>The rent range present in a result set, for one currency and one period.</summary>
public sealed record HousingPriceRangeFacetResponse
{
    public required string CurrencyCode { get; init; }

    public required string Period { get; init; }

    public required decimal Minimum { get; init; }

    public required decimal Maximum { get; init; }

    public required int Count { get; init; }
}
