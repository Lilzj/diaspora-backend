namespace Diaspora.Modules.Housing.Contracts.V1.Requests;

/// <summary>One thing the landlord asks of whoever lives there.</summary>
public sealed record HouseRuleRequest
{
    public required string Text { get; init; }

    /// <summary>Where it appears in the list. Renumbered from zero on save.</summary>
    public int Order { get; init; }
}
