using Diaspora.Modules.Guides.Domain.Enums;

namespace Diaspora.Modules.Guides.Extensibility.Search;

/// <summary>
/// The question a search engine is asked. Filters the engine cannot serve it may ignore — the
/// repository re-applies visibility, so an engine that over-returns costs a wasted row, never
/// a leaked draft.
/// </summary>
public sealed record GuideSearchRequest
{
    /// <summary>The free-text term. Null browses rather than searches.</summary>
    public string? Text { get; init; }

    /// <summary>Upper-case ISO 3166-1 alpha-2.</summary>
    public string? CountryCode { get; init; }

    public string? CategoryKey { get; init; }

    /// <summary>A normalized tag slug.</summary>
    public string? Tag { get; init; }

    /// <summary>Lower-case ISO 639-1.</summary>
    public string? Language { get; init; }

    public Guid? AuthorId { get; init; }

    public bool VerifiedOnly { get; init; }

    public bool FeaturedOnly { get; init; }

    public GuideSortOrder Sort { get; init; } = GuideSortOrder.Newest;

    /// <summary>Opaque, engine-owned. From a previous outcome's <c>NextCursor</c>.</summary>
    public string? Cursor { get; init; }

    public int Limit { get; init; } = 20;
}
