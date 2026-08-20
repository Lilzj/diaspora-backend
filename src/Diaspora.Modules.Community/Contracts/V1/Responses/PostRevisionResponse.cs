namespace Diaspora.Modules.Community.Contracts.V1.Responses;

/// <summary>One earlier version of a post. Serves the client's version-history screen.</summary>
public sealed record PostRevisionResponse
{
    public required Guid RevisionId { get; init; }

    public required Guid PostId { get; init; }

    public required int VersionNumber { get; init; }

    public string? Title { get; init; }

    public required string Content { get; init; }

    /// <summary>Author-supplied, and a list because the client renders bullet points.</summary>
    public required IReadOnlyList<string> ChangeSummary { get; init; }

    public required Guid CreatedBy { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}
