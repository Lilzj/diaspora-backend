namespace Diaspora.Modules.Community.Contracts.V1.Requests;

/// <summary>One constraint in a custom audience.</summary>
public sealed record VisibilityClauseRequest
{
    /// <summary><c>country</c>, <c>area</c>, <c>follower</c>, <c>verified</c>, <c>tier</c>.</summary>
    public required string Kind { get; init; }

    public required string Value { get; init; }
}
