namespace Diaspora.Modules.Guides.Contracts.V1.Responses;

/// <summary>One section's fate between two versions.</summary>
public sealed record GuideSectionDiffResponse
{
    public required Guid SectionId { get; init; }

    /// <summary>
    /// What happened: <c>added</c>, <c>removed</c>, <c>retitled</c> or <c>content-changed</c>.
    /// A section both retitled and rewritten reports <c>content-changed</c> with both titles set.
    /// </summary>
    public required string Change { get; init; }

    /// <summary>The title in the older version. Null for an added section.</summary>
    public string? FromTitle { get; init; }

    /// <summary>The title in the newer version. Null for a removed section.</summary>
    public string? ToTitle { get; init; }

    /// <summary>Whether any block content differs. Always true for added and removed.</summary>
    public required bool ContentChanged { get; init; }
}
