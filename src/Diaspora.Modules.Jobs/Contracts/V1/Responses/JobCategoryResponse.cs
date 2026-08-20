namespace Diaspora.Modules.Jobs.Contracts.V1.Responses;

/// <summary>One employment domain in the catalogue.</summary>
public sealed record JobCategoryResponse
{
    public required Guid CategoryId { get; init; }

    /// <summary>The stable handle a posting stores.</summary>
    public required string Key { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public string? ParentKey { get; init; }

    /// <summary>An icon name the client resolves. The platform ships no images.</summary>
    public string? IconKey { get; init; }

    public required int Order { get; init; }

    public required bool IsActive { get; init; }

    /// <summary>Open postings under it. What the browse tiles print.</summary>
    public required int PostingCount { get; init; }
}
