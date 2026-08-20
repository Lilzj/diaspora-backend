namespace Diaspora.Modules.Housing.Contracts.V1.Responses;

/// <summary>One thing the landlord asks of whoever lives there.</summary>
public sealed record HouseRuleResponse
{
    public required Guid RuleId { get; init; }

    public required string Text { get; init; }

    public required int Order { get; init; }
}
