namespace Diaspora.Modules.Jobs.Domain.Candidates;

/// <summary>
/// One portfolio item as the profile editor sends it. Needs a link or an upload; an item that
/// is neither points at nothing and is rejected.
/// </summary>
public sealed record CandidatePortfolioItemDraft
{
    public Guid ItemId { get; init; }

    public required string? Label { get; init; }

    /// <summary>An absolute http(s) link.</summary>
    public string? Url { get; init; }

    /// <summary>A B5 reference to an uploaded sample.</summary>
    public Guid? MediaId { get; init; }

    public string? Description { get; init; }
}
