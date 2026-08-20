namespace Diaspora.Modules.Housing.Contracts.V1.Requests;

/// <summary>Replaces the house rules wholesale.</summary>
public sealed record SetHouseRulesRequest
{
    public required IReadOnlyList<HouseRuleRequest> Rules { get; init; }

    public int? BaseRevision { get; init; }
}
