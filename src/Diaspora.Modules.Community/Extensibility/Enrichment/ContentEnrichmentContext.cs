using Diaspora.Modules.Community.Domain.Posts;

namespace Diaspora.Modules.Community.Extensibility.Enrichment;

/// <summary>
/// The post an enricher is being given a chance to add to, and when.
/// <para>
/// The post itself, mutable, rather than a copy — an enricher that unfurls a link has to attach
/// the preview to something. The safety comes from where this runs: after the aggregate's own
/// invariants have been satisfied and inside the same transaction, so an enricher cannot create
/// a post that would not have been valid without it.
/// </para>
/// </summary>
public sealed record ContentEnrichmentContext
{
    public required CommunityPost Post { get; init; }

    /// <summary>True on first publication, false on an edit. Most enrichers only want the first.</summary>
    public required bool IsNewContent { get; init; }

    public required DateTimeOffset Now { get; init; }
}
