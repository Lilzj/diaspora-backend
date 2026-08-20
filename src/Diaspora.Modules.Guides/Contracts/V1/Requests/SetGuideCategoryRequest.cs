namespace Diaspora.Modules.Guides.Contracts.V1.Requests;

/// <summary>Moves the guide to another catalogue category, validated against the catalogue.</summary>
public sealed record SetGuideCategoryRequest
{
    public required string CategoryKey { get; init; }

    /// <summary>The revision the editor loaded. Null opts out of the conflict check.</summary>
    public int? BaseRevision { get; init; }
}
