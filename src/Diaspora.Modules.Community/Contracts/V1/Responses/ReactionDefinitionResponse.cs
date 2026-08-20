namespace Diaspora.Modules.Community.Contracts.V1.Responses;

/// <summary>One reaction kind the platform offers. Read by a client to build its picker.</summary>
public sealed record ReactionDefinitionResponse
{
    public required string Key { get; init; }

    public required string Label { get; init; }

    public string? Glyph { get; init; }

    public required string Sentiment { get; init; }

    /// <summary>Content types this kind is offered on. Empty means every type.</summary>
    public required IReadOnlyList<string> AppliesTo { get; init; }

    public required int Position { get; init; }
}
