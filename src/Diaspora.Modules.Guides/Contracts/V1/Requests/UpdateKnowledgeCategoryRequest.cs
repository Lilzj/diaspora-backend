namespace Diaspora.Modules.Guides.Contracts.V1.Requests;

/// <summary>
/// Rewrites a navigation row. The key is absent because it cannot change.
/// <para>
/// <b>Every collection here is a replacement, not a patch.</b> A curation screen holds the intended
/// list and submits it; a partial update would need a merge on the server that is a second, subtly
/// different implementation of the one the screen already performed. The consequence is that
/// omitting a collection clears it, which is why <see cref="Revision"/> matters: a client working
/// from stale state finds out rather than silently deleting somebody's translations.
/// </para>
/// </summary>
public sealed record UpdateKnowledgeCategoryRequest
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    public string? Icon { get; init; }

    public int Order { get; init; }

    public IReadOnlyList<string> SourceCategoryKeys { get; init; } = [];

    public IReadOnlyList<string> ProviderCategoryKeys { get; init; } = [];

    public IReadOnlyList<KnowledgeLocalizationInput> Localizations { get; init; } = [];

    /// <summary>
    /// The revision the client was looking at.
    /// <para>
    /// Optional, and omitting it means "I have not read this, apply anyway" — which is right for a
    /// script and wrong for an editor. A curation UI always sends it, because two people arranging
    /// one catalogue during a country launch is the ordinary case here rather than the exceptional
    /// one.
    /// </para>
    /// </summary>
    public int? Revision { get; init; }
}
