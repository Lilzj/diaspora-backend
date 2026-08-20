namespace Diaspora.Modules.Guides.Contracts.V1.Requests;

/// <summary>Rewrites a learning path's own copy. The key and the country never change.</summary>
public sealed record UpdateLearningPathRequest
{
    public required string Title { get; init; }

    public string? Description { get; init; }

    public string? Language { get; init; }

    /// <summary>The revision the client was looking at.</summary>
    public int? Revision { get; init; }
}
