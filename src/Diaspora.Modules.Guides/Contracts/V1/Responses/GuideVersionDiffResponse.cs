namespace Diaspora.Modules.Guides.Contracts.V1.Responses;

/// <summary>
/// What changed between two published versions, at section granularity. Sections are matched
/// by their stable identifiers, which is what keeps "renamed" and "replaced" distinguishable.
/// </summary>
public sealed record GuideVersionDiffResponse
{
    public required Guid GuideId { get; init; }

    public required int FromVersion { get; init; }

    public required int ToVersion { get; init; }

    /// <summary>Every section that differs, in the target version's order. Unchanged sections are omitted.</summary>
    public IReadOnlyList<GuideSectionDiffResponse> Sections { get; init; } = [];
}
