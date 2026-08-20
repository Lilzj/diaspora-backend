namespace Diaspora.Modules.Community.Contracts.V1.Responses;

/// <summary>A named audience.</summary>
public sealed record VisibilityRuleResponse
{
    public required Guid RuleId { get; init; }

    public required string Name { get; init; }

    public required string BaseLevel { get; init; }

    public required IReadOnlyList<VisibilityClauseResponse> Clauses { get; init; }

    public required bool IsShared { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}
