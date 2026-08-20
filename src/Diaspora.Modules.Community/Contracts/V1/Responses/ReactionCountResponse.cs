namespace Diaspora.Modules.Community.Contracts.V1.Responses;

/// <summary>One reaction kind's count on one item.</summary>
public sealed record ReactionCountResponse
{
    public required string ReactionKey { get; init; }

    public required int Count { get; init; }

    public required bool ReactedByViewer { get; init; }
}
