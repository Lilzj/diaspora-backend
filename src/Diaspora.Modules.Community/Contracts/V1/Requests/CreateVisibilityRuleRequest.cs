namespace Diaspora.Modules.Community.Contracts.V1.Requests;

/// <summary>A named, reusable audience.</summary>
public sealed record CreateVisibilityRuleRequest
{
    public required string Name { get; init; }

    /// <summary>The widest this rule can reach. Clauses narrow from here and never widen.</summary>
    public required string BaseLevel { get; init; }

    public IReadOnlyList<VisibilityClauseRequest>? Clauses { get; init; }
}
