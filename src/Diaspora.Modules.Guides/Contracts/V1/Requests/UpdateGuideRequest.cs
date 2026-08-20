namespace Diaspora.Modules.Guides.Contracts.V1.Requests;

/// <summary>
/// The full-document editor save: metadata and, when supplied, the whole structure.
/// <para>
/// Wholesale rather than per-block, because that is how the editor works — a person writes, the
/// client saves the document. <see cref="BaseRevision"/> is the optimistic concurrency token:
/// send back the revision the document was loaded at and a stale save comes back as a conflict
/// the client can merge from, rather than silently overwriting a colleague's edit.
/// </para>
/// </summary>
public sealed record UpdateGuideRequest
{
    public required string Title { get; init; }

    public string? Summary { get; init; }

    /// <summary>A <c>GuideVisibility</c> name. Omitted, the visibility is unchanged from Public.</summary>
    public string? Visibility { get; init; }

    /// <summary>A <c>GuideDifficulty</c> name.</summary>
    public string? Difficulty { get; init; }

    /// <summary>How long the described process takes, when the author says. Minutes.</summary>
    public int? EstimatedCompletionMinutes { get; init; }

    // ── Location context ────────────────────────────────────────────────────────────────────

    public required string CountryCode { get; init; }

    public string? RegionName { get; init; }

    public string? CityName { get; init; }

    public Guid? AreaId { get; init; }

    // ── SEO, all optional in spirit ─────────────────────────────────────────────────────────

    public string? MetaTitle { get; init; }

    public string? MetaDescription { get; init; }

    /// <summary>Absolute https, or nothing.</summary>
    public string? CanonicalUrl { get; init; }

    /// <summary>Comma-joined keywords.</summary>
    public string? Keywords { get; init; }

    // ── Structure ───────────────────────────────────────────────────────────────────────────

    /// <summary>The whole document, when the save includes content. Null edits metadata only.</summary>
    public IReadOnlyList<GuideSectionInput>? Sections { get; init; }

    /// <summary>The revision the editor loaded. Null opts out of the conflict check.</summary>
    public int? BaseRevision { get; init; }
}
