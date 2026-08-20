namespace Diaspora.Modules.Guides.Contracts.V1.Requests;

/// <summary>
/// Replaces the tag set wholesale — that is how the client's chip editor saves. Free text in;
/// the aggregate normalizes each entry to a slug and deduplicates.
/// </summary>
public sealed record SetGuideTagsRequest
{
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>The revision the editor loaded. Null opts out of the conflict check.</summary>
    public int? BaseRevision { get; init; }
}
