namespace Diaspora.Modules.Community.Contracts.V1.Responses;

/// <summary>One constraint in a named audience.</summary>
public sealed record VisibilityClauseResponse
{
    public required string Kind { get; init; }

    public required string Value { get; init; }
}
